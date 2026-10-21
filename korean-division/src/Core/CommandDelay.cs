using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 지휘 계통 지연 (2026-10-19 승인, balance.json "command"): 이동 · 공격 · 돌격 명령을 받으면 출발 지연 (0~3틱, Combat3 ReactDelayFor) 에 더해
/// 지휘 계통에 따라 지연된다 — 장교 지휘 범위 안 +0 · 분대에 공식 부사관 (하사 이상) 생존 +ncoMin~Max · 분대는 있으나 부사관 없음 (임시 분대장만) +actingMin~Max ·
/// 분대 없음 · 혼자 남은 분대 +isolatedMin~Max (초). 유닛 · 명령 틱 해시로 결정적. 후퇴 (이동 명령) 는 ×retreatMul. 보병만 (infantryOnly) — 차량 · 장교 자신은 +0.
/// 표시 · 음성은 즉시 (LastOrderTick).
/// </summary>
public sealed class CommandDelayDef
{
    public float NcoMin { get; set; } = 0.14f;
    public float NcoMax { get; set; } = 0.28f;
    public float ActingMin { get; set; } = 0.35f;
    public float ActingMax { get; set; } = 0.7f;
    public float IsolatedMin { get; set; } = 0.7f;
    public float IsolatedMax { get; set; } = 1.05f;
    public float RetreatMul { get; set; } = 0.5f;
    public bool InfantryOnly { get; set; } = true;
    /// <summary>N37 플레이어 명령 지연 곡선 (2026-10-19 승인): 선택 인원 PlayerSelSizes[i] 에서 배수 PlayerSelMul[i], 사이는 선형, 끝 밖은 끝값.
    /// 반응 지연 (0~2틱) · 지휘 계통 지연 둘 다에 곱한다. AI 명령 (Sel 0) 은 1.</summary>
    public int[] PlayerSelSizes { get; set; } = { 1, 12, 40, 150, 500 };
    public float[] PlayerSelMul { get; set; } = { 0.25f, 0.3f, 0.5f, 0.8f, 1f };
    /// <summary>N34 대형 반응: 대형 · 방향 변경 (중대 이동 명령 · 빈자리 재배치 · 대열 정비 · 전개) 에 병사마다 0~formReactMaxSec 초 (틱 반올림) 뒤 반응. 중대 명령에서는 기본 출발 지연 (reactDelayMaxTicks) 을 대신한다 (겹치지 않음).</summary>
    public float FormReactMaxSec { get; set; } = 0.05f;
}

/// <summary>지휘 계통 단계 (지연이 작은 순).</summary>
public enum ChainTier { Officer = 0, Nco = 1, Acting = 2, Isolated = 3, None = 4 }

public sealed partial class World
{
    /// <summary>[작업값 — 저장 안 함] 이번 틱 명령 처리에 쓰는 살아 있는 장교 목록 (틱마다 Units 에서 새로 모음 — RebuildHash 의 officers 는 명령 처리 시점에 지난 틱 값이라 불러온 월드와 어긋날 수 있다).</summary>
    readonly List<Unit> cmdOfficers = new();
    long cmdOfficersTick = long.MinValue;

    void RefreshCmdOfficers()
    {
        if (cmdOfficersTick == Tick) return;
        cmdOfficersTick = Tick;
        cmdOfficers.Clear();
        foreach (var u in Units) if (u.Alive && u.Def.CommandRadius > 0) cmdOfficers.Add(u);
    }

    /// <summary>이 유닛의 지휘 계통 단계 (명령 지연 기준).</summary>
    public ChainTier ChainTierOf(Unit u)
    {
        if (u.IsOfficer || (Defs.Command.InfantryOnly && !u.Def.IsInfantry)) return ChainTier.None;
        RefreshCmdOfficers();
        foreach (var o in cmdOfficers)
        {
            if (ReferenceEquals(o, u) || o.Owner != u.Owner || !o.Alive) continue;
            float r = o.Def.CommandRadius;
            if (Vector2.DistanceSquared(o.Pos, u.Pos) <= r * r) return ChainTier.Officer;
        }
        if (u.Squad is not { } s || s.Members.Count < 2) return ChainTier.Isolated;
        foreach (var m in s.Members) if (m.Alive && m.IsNco) return ChainTier.Nco;
        return ChainTier.Acting;
    }

    /// <summary>N37: 플레이어 명령의 선택 인원 → 지연 배수 (balance.json command.playerSelSizes · playerSelMul, 구간 선형). sel ≤ 0 (AI) = 1.</summary>
    public float PlayerSelDelayMul(int sel)
    {
        var d = Defs.Command;
        var xs = d.PlayerSelSizes; var ys = d.PlayerSelMul;
        if (sel <= 0 || xs == null || ys == null || xs.Length == 0 || xs.Length != ys.Length) return 1f;
        if (sel <= xs[0]) return ys[0];
        for (int i = 1; i < xs.Length; i++)
            if (sel <= xs[i]) return ys[i - 1] + (ys[i] - ys[i - 1]) * (sel - xs[i - 1]) / (float)Math.Max(1, xs[i] - xs[i - 1]);
        return ys[^1];
    }

    /// <summary>N34 대형 반응 지연 (틱): 0~round(formReactMaxSec / SimDt), 유닛 Id · 틱 해시로 결정적.</summary>
    internal int FormReactTicks(Unit u)
    {
        int max = Math.Max(0, (int)MathF.Round(Defs.Command.FormReactMaxSec / Config.SimDt));
        return Math.Min(max, (int)(Roll(u.Id, 0x464F524D) * (max + 1)));
    }

    /// <summary>N34: 대형 재배치 (깃발 슬롯) 를 받은 병사 — 이미 더 긴 지연이 있으면 그대로.</summary>
    void FormReact(Unit u) => u.ReactDelay = Math.Max(u.ReactDelay, FormReactTicks(u));

    /// <summary>지휘 계통 지연 (틱): 단계별 범위 안 결정적 해시 (유닛 Id · 명령 틱), 후퇴 (이동 명령, 돌격 아님) ×retreatMul, 플레이어 선택 인원 배수 ×mul (N37).</summary>
    int ChainDelayTicks(Unit u, float mul = 1f)
    {
        var d = Defs.Command;
        var tier = ChainTierOf(u);
        float lo, hi;
        switch (tier)
        {
            case ChainTier.Nco: lo = d.NcoMin; hi = d.NcoMax; break;
            case ChainTier.Acting: lo = d.ActingMin; hi = d.ActingMax; break;
            case ChainTier.Isolated: lo = d.IsolatedMin; hi = d.IsolatedMax; break;
            default: return 0;
        }
        float sec = (lo + (hi - lo) * Roll(u.Id, 0x43484E)) * ChainDelayMul(u);   // 중대 지휘관 경험치로 단축 (CommandRanks.cs)
        if (u.Order == UnitOrder.Move && !u.Charging) sec *= d.RetreatMul;
        sec *= mul;
        return Math.Max(0, (int)MathF.Round(sec / Config.SimDt));
    }
}
