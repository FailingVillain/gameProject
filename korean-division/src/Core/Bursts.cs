namespace KoreanDivision.Core;

/// <summary>
/// 경기관총 점사 (사용자 2026-10-19): lmg 는 burstMin~burstMax 발씩 쏘고 burstPauseMin~Max 초 쉰 뒤 다시 쏜다.
/// 점사 안에서는 평소 cooldown. 점사 길이 · 휴지는 유닛 Id · 틱 해시 (Roll) 로 정한다 — 결정적.
/// 휴지 = 마지막 발 뒤 Cooldown 에 더함 (별도 타이머 없음), 상태는 BurstLeft 하나 (저장 · 체크섬). 중기관총 (hmg) 은 burst 값이 없어 지속 사격.
/// 표적을 놓으면 점사를 끊고 휴지, 새 표적은 새 점사 (Engage 의 AimTarget 변경).
/// </summary>
public sealed partial class World
{
    float BurstPause(Unit u, WeaponDef w) => w.BurstPauseMin + (w.BurstPauseMax - w.BurstPauseMin) * Roll(u.Id, 7002);

    /// <summary>사격 직후 호출 (Cooldown 설정 뒤): 점사 시작이면 길이를 굴리고, 마지막 발이면 휴지를 더한다.</summary>
    void BurstAfterShot(Unit u, WeaponDef w)
    {
        if (!w.Bursty) return;
        if (u.BurstLeft <= 0)
        {
            int lo = Math.Max(1, w.BurstMin), hi = Math.Max(lo, w.BurstMax);
            u.BurstLeft = lo + Math.Min(hi - lo, (int)(Roll(u.Id, 7001) * (hi - lo + 1)));
        }
        if (--u.BurstLeft == 0) u.Cooldown += BurstPause(u, w);
    }

    void BurstTargetLost(Unit u)
    {
        u.BurstLeft = 0;
        if (u.Weapon is { Bursty: true } w) u.Cooldown = Math.Max(u.Cooldown, BurstPause(u, w));
    }
}
