namespace KoreanDivision.Core;

/// <summary>
/// 캠페인 저장 슬롯 9칸 + 자동 저장 2개 (T19a, 사용자 2026-10-22 · 설계 §0 §3).
/// - 진행 중 캠페인 = 기존 <c>campaign.json</c> (옛 단일 저장 그대로 = 호환). 슬롯 · 자동 저장은 같은 형식의 사본 파일.
/// - 수동 슬롯: <c>slot1.json</c> ~ <c>slot9.json</c> (저장 · 불러오기 · 덮어쓰기).
/// - 자동 저장: <c>auto_preop.json</c> (작전 시작 직전 = 재도전 지점), <c>auto_settle.json</c> (전과 정산 직후).
/// - 모든 쓰기는 <see cref="WriteAtomic"/>: 임시 파일에 쓰고 바꿔치기, 실패하면 임시 파일을 지워 반쪽 파일을 남기지 않는다.
/// 표시 · 이월 전용 (시뮬레이션 상태 · 스냅샷 · 체크섬과 무관).
/// </summary>
public static class CampaignSaves
{
    public const int SlotCount = 9;
    public enum Auto { PreOp, Settle }

    public static string SlotPath(int slot) => Path.Combine(CampaignState.Folder, $"slot{slot}.json");
    public static string AutoPath(Auto a) => Path.Combine(CampaignState.Folder, a == Auto.PreOp ? "auto_preop.json" : "auto_settle.json");

    /// <summary>[테스트 · 수동 확인] 다음 N 번의 쓰기를 실패시킨다 (임시 파일을 반쯤 쓴 뒤 IOException). 환경변수 KD_SAVE_FAIL=N 으로도 켠다.</summary>
    public static int InjectFailures = int.TryParse(Environment.GetEnvironmentVariable("KD_SAVE_FAIL"), out int n) ? Math.Max(0, n) : 0;

    /// <summary>원자적 쓰기: path.tmp 에 쓰고 path 로 바꿔치기. 실패 시 path 는 이전 내용 그대로, .tmp 는 지운다. 성공 여부.</summary>
    public static bool WriteAtomic(string path, string text)
    {
        string tmp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (InjectFailures > 0)
            {
                InjectFailures--;
                File.WriteAllText(tmp, text[..(text.Length / 2)]);   // 반쪽 임시 파일을 남긴 채 실패하는 상황 재현
                throw new IOException("저장 실패 주입 (KD_SAVE_FAIL)");
            }
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"캠페인 저장 실패 ({Path.GetFileName(path)}): {e.Message}");
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }

    /// <summary>슬롯 저장. 이미 있는 칸이 깨졌거나 더 새 버전이면(Error) 덮어쓰지 않는다 — 원본 보존.</summary>
    public static bool SaveSlot(CampaignState s, int slot) => slot is >= 1 and <= SlotCount && Describe(SlotPath(slot)).Error == null && WriteAtomic(SlotPath(slot), s.ToJson());
    public static bool SaveAuto(CampaignState s, Auto a) => WriteAtomic(AutoPath(a), s.ToJson());

    /// <summary>진행 저장 + 자동 저장 한 쌍 (작전 직전 · 정산 직후). 둘 다 성공해야 true (실패 시 호출자가 알림 + 다시 시도).</summary>
    public static bool SaveWithAuto(CampaignState s, Auto a) => s.Save() && SaveAuto(s, a);

    /// <summary>슬롯 · 자동 저장 파일 읽기 (없으면 null + error null, 깨졌거나 새 버전이면 null + 한국어 사유). 진행 저장(campaign.json)은 건드리지 않는다.</summary>
    public static CampaignState? Read(string path, out string? error)
    {
        error = null;
        if (!File.Exists(path)) return null;
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { error = string.Format(SnapMsg.CantRead, e.Message); return null; }
        return CampaignState.Parse(text, out error);
    }

    /// <summary>슬롯 목록 표시용 요약 (파일 시각은 화면 표시 전용).</summary>
    public readonly record struct Info(bool Exists, string? Error, string NextName, int Completed, float Supply, DateTime SavedAt);

    public static Info Describe(string path)
    {
        var s = Read(path, out string? err);
        if (s == null) return new Info(err != null, err, "", 0, 0, err != null ? SafeTime(path) : default);
        return new Info(true, null, s.NextName, s.Completed.Count, s.Supply, SafeTime(path));
    }

    static DateTime SafeTime(string path)
    {
        try { return File.GetLastWriteTime(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return default; }
    }
}
