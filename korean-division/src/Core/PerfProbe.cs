namespace KoreanDivision.Core;

/// <summary>
/// [측정 전용] 선택 계측 슬롯 (T50d). 시뮬 상태가 아님 - 체크섬 · 저장에 없음. On=false(기본)면 호출 지점 비용은 분기 1회.
/// 지금은 호출 지점이 없다 (WorldCombat.cs · FogOfWar.cs 가 병합 라인에서 바뀌는 중). 병합 뒤 필요하면
///   long t0 = PerfProbe.Begin(); ... PerfProbe.End(PerfProbe.FogStamp, t0);
/// 형태로 추가한다 (카드 T50d 의 '호출 지점 후보' 참조). 구간 합 FindTarget(World.Prof[11]) · Fog(Prof[8]) 은 기존 World.Prof 훅이 이미 잰다.
/// </summary>
public static class PerfProbe
{
    public const int FindTargetCalls = 0, FindTargetUnitsIter = 1, FogDecay = 2, FogStamp = 3, FogStampCount = 4, FogRemember = 5, Slots = 8;
    public static readonly string[] Names = { "findTargetCalls", "findTargetUnitsIter", "fogDecayTicks", "fogStampTicks", "fogStampCount", "fogRememberTicks", "", "" };
    public static bool On;
    public static readonly long[] V = new long[Slots];
    public static long Begin() => On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
    public static void End(int slot, long t0) { if (On) V[slot] += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
    public static void Count(int slot, long n = 1) { if (On) V[slot] += n; }
    public static void Reset() => Array.Clear(V);
}
