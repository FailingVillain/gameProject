using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

/// <summary>저장 목록의 한 줄: 머리말을 읽었으면 Header, 못 읽었으면 Error (깨짐 · 다른 형식 — 화면에는 '읽을 수 없음'으로 표시하고 불러오지 않는다).</summary>
public sealed record SaveEntry(string Path, string Name, DateTime Modified, SnapshotHeader? Header, string? Error, string? Note = null);

/// <summary>
/// 저장 폴더 (실행 파일 옆 saves/) 관리: 이름 → 경로, 목록 (머리말만 읽음 — 큰 저장도 빠름), 자동 저장 이름, 불러오기 옵션 만들기.
/// 저장 / 불러오기 본체는 Core/Snapshot — 여기는 파일 이름 · 폴더만 다룬다.
/// </summary>
public static class SaveManager
{
    /// <summary>테스트 · 퍼즈가 진짜 saves/ 를 어지럽히지 않게 바꿔 쓴다.</summary>
    public static string? FolderOverride;
    public static string Folder => FolderOverride ?? Path.Combine(AppContext.BaseDirectory, "saves");

    public const string QuickName = "quicksave";
    public const int AutosaveKeep = 3;
    /// <summary>큰 교전 직전 자동 저장 슬롯 (한 개, 덮어씀) · 조용한 시간 20초 · 최소 한 번에 6발 · 저장 간격 2분 (게임 시간 틱).</summary>
    public const string BattleName = "autosave_battle";
    public const long BattleLullTicks = 20 * 60, BattleSaveGap = 2 * 60 * 60;
    public const int BattleShotMin = 6;
    public struct BattleState
    {
        public long LastShotSeen, LastSave; public bool Armed;
        public static BattleState Initial => new() { LastShotSeen = -1_000_000, LastSave = -1_000_000, Armed = true };
    }
    /// <summary>전투 자동 저장 판정 (순수): 이 틱에 저장해야 하면 true. 저장 성공 여부와 무관하게 재무장 · 간격 상태를 갱신하므로 실패해도 매 프레임 반복하지 않는다.</summary>
    public static bool BattleSaveDue(ref BattleState s, long tick, int shotCount)
    {
        if (shotCount == 0) { if (tick - s.LastShotSeen >= BattleLullTicks) s.Armed = true; return false; }
        s.LastShotSeen = tick;
        if (!s.Armed || shotCount < BattleShotMin) return false;
        s.Armed = false;
        if (tick - s.LastSave < BattleSaveGap) return false;
        s.LastSave = tick;
        return true;
    }
    /// <summary>자동 저장 간격 (게임 시간, 틱). 10분. 퍼즈가 짧게 바꾼다.</summary>
    public static long AutosaveTicks = 10 * 60 * 60;

    /// <summary>파일 이름으로 쓸 수 있게: 영문 · 숫자 · - _ . 만 (그 외는 _). 비면 "save". 길이 40 제한.</summary>
    public static string Sanitize(string? raw)
    {
        var sb = new StringBuilder();
        foreach (char c in (raw ?? "").Trim())
        {
            if (sb.Length >= 40) break;
            sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c is '-' or '_' or '.') ? c : '_');
        }
        string s = sb.ToString().Trim('.');
        if (s.Length == 0) return "save";
        return PathGuard.IsReservedName(s) ? "_" + s : s;   // T51a: CON · NUL 같은 윈도우 예약 이름 거름
    }

    public static string PathOf(string name) => Path.Combine(Folder, Sanitize(name) + Snapshot.Extension);

    /// <summary>다음 자동 저장 이름: 10분마다 autosave_1 → _2 → _3 → _1 ... (가장 오래된 것을 덮어써 항상 최근 3개가 남는다).</summary>
    public static string AutosaveName(long tick)
    {
        long slot = Math.Max(1, tick / AutosaveTicks);
        return $"autosave_{(slot - 1) % AutosaveKeep + 1}";
    }

    public static string TimestampName() => "save_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

    public static List<SaveEntry> List()
    {
        var list = new List<SaveEntry>();
        string dir = Folder;
        if (!Directory.Exists(dir)) return list;
        IEnumerable<string> files;
        try { files = Directory.GetFiles(dir, "*" + Snapshot.Extension); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return list; }
        foreach (var f in files)
        {
            string name = Path.GetFileNameWithoutExtension(f);
            DateTime mod;
            try { mod = File.GetLastWriteTime(f); } catch (IOException) { mod = DateTime.MinValue; }
            try { list.Add(new SaveEntry(f, name, mod, Snapshot.ReadHeaderFile(f), null)); }
            catch (SnapshotException e) { list.Add(new SaveEntry(f, name, mod, null, e.Message, e.Note)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { list.Add(new SaveEntry(f, name, mod, null, e.Message)); }
        }
        list.Sort((a, b) => b.Modified.CompareTo(a.Modified));
        return list;
    }

    /// <summary>가장 최근 저장 (읽을 수 있는 것만). 없으면 null.</summary>
    public static SaveEntry? Newest() => List().FirstOrDefault(e => e.Header != null);

    public static bool Delete(string path)
    {
        try { File.Delete(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>불러온 저장으로 게임을 시작할 옵션 (재시작 · 메뉴 복귀에 필요한 설정은 머리말에서 채운다).</summary>
    public static GameOptions OptionsFor(LoadedSnapshot l, bool fromMenu)
    {
        var h = l.Header;
        return new GameOptions
        {
            Loaded = l,
            FromMenu = fromMenu,
            Spectate = h.Spectate,
            Faction = Math.Max(1, h.HumanFaction),
            Ai = h.Ai,
            NoFog = !h.Fog,
            MapId = h.Mission != null ? null : h.MapId,
            MissionId = h.Mission,   // 미션 저장: 다시 하기 = 미션 처음부터 (배치 단계)
            MapWidth = h.MapWidth,
            MapHeight = h.MapHeight,
            Players = Math.Max(2, h.Factions.Count),
            Seed = h.Seed,
            Battle = h.Battle,
        };
    }

    /// <summary>플레이 시간 문자열 (틱 → m:ss / h:mm:ss).</summary>
    public static string Clock(long tick) => Game.Clock(tick);
}
