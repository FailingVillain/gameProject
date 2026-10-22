using System.Numerics;
using KoreanDivision.Core;

namespace KoreanDivision.UI;

public enum AlertKind { UnderAttack = 0, SiteContested = 1 }

/// <summary>
/// 교전 알림 이력 (T52a). UI 전용 — 시뮬 상태는 읽기만 하고(World.LastAttacked · Building.Contested) 저장 · 체크섬 · 리플레이에 들어가지 않는다.
/// 최대 8건. 같은 종류 + 16칸 이내 + 8초 이내 = 한 건으로 합침(횟수만 증가). 효과음 · 음성은 전체 8초 간격, 공지는 종류별 8초 (기존 동작 보존).
/// Feed 는 시간을 인자로 받는 순수 함수라 벽시계 없이 시험된다.
/// </summary>
public sealed class AlertLog
{
    public const int MaxEntries = 8;
    public const float MergeTiles = 16f;
    public const double MergeSeconds = 8, SoundInterval = 8, NoticeInterval = 8;
    public const int ZoneTiles = 16;   // 구역 격자 한 칸 (표시용)

    public sealed class Entry
    {
        public AlertKind Kind;
        public Vector2 Pos;
        public double FirstTime, LastTime;
        public int Count = 1;
    }

    public readonly struct Result
    {
        public readonly Entry Entry; public readonly bool Added, Notice, Sound;
        public Result(Entry e, bool added, bool notice, bool sound) { Entry = e; Added = added; Notice = notice; Sound = sound; }
    }

    readonly List<Entry> entries = new();   // 오래된 것 -> 최근
    double lastSound = double.NegativeInfinity;
    readonly double[] lastNotice = { double.NegativeInfinity, double.NegativeInfinity };

    public IReadOnlyList<Entry> Entries => entries;
    public Entry? Latest => entries.Count == 0 ? null : entries[^1];

    /// <summary>알림 한 건을 넣는다. 합쳐지면 기존 항목을 갱신(위치는 최근 것), 아니면 추가(8건 넘으면 가장 오래된 것 제거).</summary>
    public Result Feed(AlertKind kind, Vector2 pos, double now)
    {
        Entry? hit = null;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.Kind == kind && now - e.LastTime <= MergeSeconds && Vector2.Distance(e.Pos, pos) <= MergeTiles) { hit = e; break; }
        }
        bool added = hit == null;
        if (hit == null)
        {
            hit = new Entry { Kind = kind, Pos = pos, FirstTime = now, LastTime = now };
            entries.Add(hit);
            if (entries.Count > MaxEntries) entries.RemoveAt(0);
        }
        else
        {
            hit.Pos = pos; hit.LastTime = now; hit.Count++;
            entries.Remove(hit); entries.Add(hit);   // 최근 순서 유지
        }
        int k = (int)kind;
        bool notice = now - lastNotice[k] > NoticeInterval; if (notice) lastNotice[k] = now;
        bool sound = now - lastSound > SoundInterval; if (sound) lastSound = now;
        return new Result(hit, added, notice, sound);
    }

    // ---------------------------------------------------------------- 시뮬 상태 폴링 (읽기만)

    World? seenWorld;
    long lastAttackTick = -1;
    readonly Dictionary<int, bool> contested = new();
    bool seeded;

    public void Clear()
    {
        entries.Clear(); contested.Clear(); seeded = false; lastAttackTick = -1;
        lastSound = double.NegativeInfinity; lastNotice[0] = lastNotice[1] = double.NegativeInfinity;
    }

    /// <summary>me 세력 기준으로 새 알림을 모아 Feed 한다. 월드가 바뀌면(불러오기) 이력을 비운다.</summary>
    public List<Result> Poll(World w, int me, double now)
    {
        var res = new List<Result>();
        if (!ReferenceEquals(seenWorld, w)) { Clear(); seenWorld = w; }
        if (w.LastAttacked.TryGetValue(me, out var atk) && atk.Tick != lastAttackTick)
        {
            lastAttackTick = atk.Tick;
            res.Add(Feed(AlertKind.UnderAttack, atk.Pos, now));
        }
        foreach (var b in w.Buildings)   // 리스트 순서 (딕셔너리 열거 아님)
        {
            if (!b.Def.Capturable) continue;
            bool was = contested.TryGetValue(b.Id, out var c) && c;
            contested[b.Id] = b.Contested;
            bool mine = b.Owner == me || (b.Owner > 0 && me > 0 && w.Allied(me, b.Owner));
            if (seeded && mine && b.Contested && !was) res.Add(Feed(AlertKind.SiteContested, b.Pos, now));
        }
        seeded = true;
        return res;
    }

    // ---------------------------------------------------------------- 표시용

    /// <summary>구역 표기: 가로 A.. + 세로 1.. (16칸 격자).</summary>
    public static string Zone(Vector2 pos)
    {
        int cx = Math.Clamp((int)(pos.X / ZoneTiles), 0, 25), cy = Math.Max(0, (int)(pos.Y / ZoneTiles));
        return $"{(char)('A' + cx)}{cy + 1}";
    }

    /// <summary>경과 표기: 10초 미만 = 방금, 1분 미만 = N초 전, 그 외 N분 전.</summary>
    public static string Ago(double age) =>
        age < 10 ? S.AlertJustNow : age < 60 ? string.Format(S.AlertSecAgo, (int)age) : string.Format(S.AlertMinAgo, (int)(age / 60));

    public static string KindName(AlertKind k) => k == AlertKind.UnderAttack ? S.AlertKindAttack : S.AlertKindSite;

    /// <summary>띠 한 줄 글: "방금 · 공격 · C4 (x3)".</summary>
    public static string Line(Entry e, double now)
    {
        string s = $"{Ago(now - e.LastTime)} · {KindName(e.Kind)} · {Zone(e.Pos)}";
        return e.Count > 1 ? s + $" x{e.Count}" : s;
    }
}
