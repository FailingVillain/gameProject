namespace KoreanDivision.Core;

/// <summary>
/// N57 착검 · 칼 뽑기 (사용자 2026-10-19): 착검 안 된 병사가 돌격을 시작하면 잠깐 제자리에서 착검 (소총 1~1.5초, 노병 ×0.7 · 신병 ×1.3)
/// · 칼 뽑기 (대도 · 군도 · 단검 0.3~0.5초, 기병 군도 0.5초) 뒤 달린다. 한 번 꽂으면 유지 (BayonetFixed).
/// 멈춤은 명령 반응 지연 (ReactDelay) 이 끝난 뒤 따로 센다 → N37 선택 규모 지연 단축이 줄이지 않는다.
/// balance.json combat.bayonetFixOrderedOnly = true (기본) 이면 돌격 명령에만 멈추고, 자동 · 행진 · 역돌격은 멈춤 없이 착검된 것으로 친다.
/// 저장 CMB2 (WriteCombat3 끝, FormatVersion 36) · 체크섬 HashCombat3.
/// </summary>
public sealed partial class Unit
{
    /// <summary>착검됨 (칼 병과 = 칼 뽑음). 한 번 true 면 전투 끝까지 유지.</summary>
    [SnapExt] internal bool BayonetFixed;
    /// <summary>착검 · 칼 뽑기 남은 틱 (0 = 안 하는 중). 이 동안 돌격 병사는 제자리.</summary>
    [SnapExt] internal int FixLeft;
}

public sealed partial class World
{
    /// <summary>착검 · 칼 뽑기 시간 (틱). 병사마다 결정적 해시로 [최소, 최대] 사이.</summary>
    internal int FixTicks(Unit u)
    {
        var c = Defs.CombatRules;
        float sec;
        if (u.Def.Mounted) sec = c.CavalryDrawSec;
        else
        {
            bool bayonet = u.Def.Melee == "bayonet";
            var r = bayonet ? c.BayonetFixSec : c.BladeDrawSec;
            float lo = r.Length > 0 ? r[0] : 0f, hi = r.Length > 1 ? r[1] : lo;
            sec = lo + (hi - lo) * PairNoise(u.Id, 0xBA70, Map.Seed);   // 병사마다 고정
            if (bayonet && !u.IsOfficer)
                sec *= u.Rank >= c.CloseVeteranRank ? c.FixVeteranMul : u.Rank < c.CloseRegularRank ? c.FixRecruitMul : 1f;
        }
        return Math.Max(0, (int)MathF.Round(sec / Config.SimDt));
    }

    /// <summary>돌격 시작 (ChargeState 를 0 → 1·2·3 으로 바꾼 직후). ordered = 돌격 명령 (ExecCharge).</summary>
    internal void BeginFix(Unit u, bool ordered)
    {
        if (!u.CanCharge || u.BayonetFixed || u.FixLeft > 0) return;
        if (u.Def.BayonetPreFixed) { u.BayonetFixed = true; return; }   // 일본군: 처음부터 착검 (progbar)
        if (!ordered && Defs.CombatRules.BayonetFixOrderedOnly) { u.BayonetFixed = true; return; }
        int t = FixTicks(u);
        if (t <= 0) { u.BayonetFixed = true; return; }
        u.FixLeft = t;
        if (u.Def.Melee == "bayonet" && !u.Def.Mounted) Stats.BayonetFixes++; else Stats.BladeDraws++;
    }

    /// <summary>착검 중이면 한 틱 세고 true (이 틱은 제자리 — 표적 쪽으로 몸만). 돌격이 끝났으면 취소.</summary>
    bool StepFix(Unit u, float dt)
    {
        if (u.FixLeft <= 0) return false;
        if (!u.Charging || !u.Alive) { u.FixLeft = 0; return false; }
        bool wait = false;   // 장교: 칼은 먼저 뽑아도 중대 병사 착검이 끝날 때까지 함께 기다린다 (돌격 대열 앞으로 혼자 나가지 않음)
        if (u.FixLeft == 1 && u.IsOfficer && u.Company is { } co)
            foreach (var m in co.Members) if (!m.IsOfficer && m.Alive && m.FixLeft > 0) { wait = true; break; }
        if (!wait && --u.FixLeft == 0) u.BayonetFixed = true;
        if ((u.Target?.Pos ?? u.Destination) is System.Numerics.Vector2 look) FaceToward(u, look, dt);
        return true;
    }
}
