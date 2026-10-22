namespace KoreanDivision.Core;

/// <summary>
/// 게임 상태 체크섬. 같은 입력(시드 · 명령)이면 항상 같은 값 — 리플레이 검증 · 멀티플레이 동기화 확인용.
/// 실수 값은 비트 그대로 섞는다 (반올림 비교가 아니라 완전 일치).
/// </summary>
public sealed partial class World
{
    partial void HashCombat2(ref Hasher h);
    partial void HashTrains(ref Hasher h);   // 열차 (Train.cs, T30a) — 열차 없으면 아무것도 섞지 않음

    public ulong ComputeChecksum()
    {
        var h = new Hasher();
        h.Add(Tick);
        h.Add(Map.Seed);
        h.Add(Map.Version);
        h.Add((long)Map.TerrainHash);   // 지형 · 고지 (지형 · 높이)
        h.Add(Winner);
        foreach (var f in Factions)
        {
            h.Add(f.Id);
            h.Add(f.Defeated ? 1 : 0);
            h.Add(f.Revealed ? 1 : 0);
            h.Add(f.RevealAll ? 1 : 0);
            h.Add(f.NoHqTime);
            h.Add(f.Team);
            if (f.IsAi) h.Add(0x4149);   // AI 세력 (Surrender.cs 가 읽음; FV 60 · 리플레이 머리말 AiFactions) — 사람 세력은 섞지 않아 AI 없는 골든은 그대로
        }
        HashCombatSettings(ref h);
        HashWeather(ref h);   // 날씨 · 시간대 (Weather.cs)
        HashSupply(ref h);   // 탄약 회복 · 보급 마차 · 엄폐 · 참호 (Supply.cs · Cover.cs)
        foreach (var u in Units)
        {
            h.Add(u.Id);
            h.Add(u.Owner);
            h.Add(u.Pos.X);
            h.Add(u.Pos.Y);
            h.Add(u.Hp);
            h.Add((int)u.Order);
            h.Add(u.Target?.Id ?? 0);
            h.Add(u.Cooldown);
            h.Add(u.Facing);
            h.Add(u.ReactDelay);
            h.Add(u.LastShotTick);
            h.Add(u.Weapon?.Key ?? "");
            h.Add(u.AimTarget);
            h.Add(u.AimLeft);
            h.Add(u.HoldLeft);
            h.Add(u.EngagedTick);
        }
        HashCombat2(ref h);   // 포탑 회전 · 은폐 · 경계 (Combat2.cs)
        HashOrganisation(ref h);   // 중대 · 대대 · 깃발 (Organisation.cs)
        HashMission(ref h);   // 미션 목표 · 사건 · 증원 (Mission/WorldMission.cs)
        HashTrains(ref h);
        foreach (var b in Buildings)
        {
            h.Add(b.Id);
            h.Add(b.Owner);
            h.Add(b.Hp);
            h.Add(b.CaptureFaction);
            h.Add(b.CaptureProgress);
            h.Add(b.Contested ? 1 : 0);
        }
        return h.Value;
    }

    /// <summary>FNV-1a 64비트.</summary>
    struct Hasher
    {
        ulong v;
        public Hasher() => v = 14695981039346656037UL;
        public ulong Value => v;

        public void Add(long x)
        {
            for (int i = 0; i < 8; i++)
            {
                v ^= (byte)(x >> (i * 8));
                v *= 1099511628211UL;
            }
        }

        public void Add(int x) => Add((long)x);
        public void Add(float x) => Add((long)(uint)BitConverter.SingleToInt32Bits(x == 0 ? 0f : x));   // -0 정규화

        /// <summary>문자열은 내용으로 (string.GetHashCode 는 실행마다 달라서 쓰면 안 됨).</summary>
        public void Add(string s)
        {
            foreach (char c in s) Add((int)c);
        }
    }
}
