using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>
/// 교전 테스트 설정: 유닛만 스폰하고 두 세력이 시작부터 적대 상태로 싸운다 (건물 · 경제 · 인구 상한 없음, 한쪽이 전멸하면 끝).
/// 리플레이 머리말에 그대로 기록되어 (ReplayFile.Battle) 같은 배치를 재현한다. 스폰은 명령이 아니라 이 설정에서 결정적으로 만든다.
/// </summary>
public sealed class BattleSetup
{
    /// <summary>한 세력이 가질 수 있는 최대 유닛 수.</summary>
    public const int MaxPerSide = 2000;

    /// <summary>세력 1 (왼쪽) 구성: "종류:수,종류:수" (예 "infantry:1500,tank_ft_male:300").</summary>
    public string SideA { get; set; } = "infantry:100";
    /// <summary>세력 2 (오른쪽) 구성.</summary>
    public string SideB { get; set; } = "infantry:100";
    /// <summary>flat | terrain | city (지도 파일 battle_&lt;이름&gt;).</summary>
    public string Map { get; set; } = "flat";
    /// <summary>양쪽 최전선 사이 거리 (타일).</summary>
    public int Distance { get; set; } = 60;   // 2026-10-19 사거리 개편으로 40 → 60
    /// <summary>true 면 두 세력 모두 AI (사람은 관전).</summary>
    public bool AiVsAi { get; set; }
    /// <summary>각 세력을 조작하는 AI 이름 (사람이 맡은 세력 1 은 무시).</summary>
    public string AiA { get; set; } = AiRegistry.Default;   // 기본 전술 AI (옛 리플레이는 "battle" 이 기록돼 있다)
    public string AiB { get; set; } = AiRegistry.Default;
    /// <summary>true 면 교전 테스트에서도 탄약을 쓴다 (충전 시설 없음 → 바닥나면 사격 불가). 기본 false = 무한 탄약. World.BattleAmmo.</summary>
    public bool Ammo { get; set; }

    public const int MinDistance = 16, MaxDistance = 120;

    [System.Text.Json.Serialization.JsonIgnore] public string MapId => "battle_" + Map;

    public static readonly string[] Maps = { "flat", "terrain", "city" };

    /// <summary>"종류:수,종류:수" → 목록 (balance.json 순서, 같은 종류는 합침, 수 0 은 뺌). 알 수 없는 종류 · 잘못된 수는 ArgumentException.</summary>
    public static List<(string Unit, int Count)> Parse(string? text, Defs defs)
    {
        var sum = new Dictionary<string, int>();
        foreach (var part in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':', StringSplitOptions.TrimEntries);
            string key = kv[0].ToLowerInvariant();
            if (!defs.Units.ContainsKey(key)) throw new ArgumentException($"알 수 없는 유닛 '{kv[0]}' (가능: {string.Join(", ", defs.Units.Keys)})");
            if (kv.Length != 2 || !int.TryParse(kv[1], out int n) || n < 0) throw new ArgumentException($"'{part}' 은(는) 종류:수 형식이 아님");
            sum[key] = sum.GetValueOrDefault(key) + n;
        }
        return defs.Units.Keys.Where(sum.ContainsKey).Where(k => sum[k] > 0).Select(k => (k, sum[k])).ToList();
    }

    public static string Format(IEnumerable<(string Unit, int Count)> list) => string.Join(",", list.Where(e => e.Count > 0).Select(e => $"{e.Unit}:{e.Count}"));

    /// <summary>검증: 알 수 없는 맵 · 유닛, 한쪽이 비었거나 2000 초과면 ArgumentException.</summary>
    public void Validate(Defs defs)
    {
        if (!Maps.Contains(Map)) throw new ArgumentException($"알 수 없는 교전 맵 '{Map}' (가능: {string.Join(", ", Maps)})");
        foreach (var (name, side) in new[] { ("A", SideA), ("B", SideB) })
        {
            int total = Parse(side, defs).Sum(e => e.Count);
            if (total <= 0) throw new ArgumentException($"{name} 세력에 유닛이 없음");
            if (total > MaxPerSide) throw new ArgumentException($"{name} 세력 {total}기 > 상한 {MaxPerSide}");
        }
        if (Distance < MinDistance || Distance > MaxDistance) throw new ArgumentException($"시작 거리는 {MinDistance}~{MaxDistance}");
    }

    public IReadOnlyList<(string Unit, int Count)> CompositionOf(int faction, Defs defs) => Parse(faction == 1 ? SideA : SideB, defs);

    /// <summary>세력 1 · 2 만 있는 교전 월드를 만든다 (건물 없음). 같은 설정 + 시드 = 같은 배치.</summary>
    public World CreateWorld(Defs defs, int seed)
    {
        Validate(defs);
        var map = MapFile.Load(MapId, seed);
        var world = World.CreateBattleWorld(map, defs, this);
        MapFile.ApplyFactions(world);
        for (int f = 1; f <= 2; f++) Spawn(world, f, Parse(f == 1 ? SideA : SideB, defs), seed);
        world.Fog.Update();
        world.Nav.Sync();
        return world;
    }

    /// <summary>
    /// 진형: 최전선(맵 중앙에서 Distance/2) 뒤로 종류별 열 묶음을 차례로 쌓는다. 앞에서부터 기갑 → 정예 → 정규 → 정찰 → 징집 → 대전차 → 공병.
    /// 각 묶음은 같은 폭(세로)으로 펼쳐 모든 부대(가로 줄)에 여러 종류가 섞인다.
    /// </summary>
    static void Spawn(World world, int faction, List<(string Unit, int Count)> comp, int seed)
    {
        var defs = world.Defs;
        var map = world.Map;
        float dir = faction == 1 ? -1 : 1;   // 뒤쪽 방향 (세력 1 은 왼쪽 → 뒤가 -x)
        float frontX = map.Width / 2f + dir * world.Battle!.Distance / 2f;
        float cy = map.Height / 2f;
        float height = MathF.Min(map.Height - 10, 100f);
        var anti = AiDefs.For(defs).AntiArmor;
        int Rank(UnitDef d) => d.IsArmored ? 0 : d.IsSupplyWagon ? 5 : d == anti ? 4 : 1;
        var ordered = comp.Select(e => (Def: defs.Units[e.Unit], e.Count))
            .OrderBy(e => Rank(e.Def)).ThenByDescending(e => e.Def.Hp).ThenBy(e => e.Def.Key, StringComparer.Ordinal).ToList();
        float offset = 0;
        int serial = 0;
        foreach (var (def, count) in ordered)
        {
            float s = MathF.Max(0.95f, def.Radius * 2 + 0.3f);
            int maxPerCol = Math.Max(1, (int)(height / s));
            int cols = (count + maxPerCol - 1) / maxPerCol;
            int perCol = (count + cols - 1) / cols;
            for (int i = 0; i < count; i++)
            {
                int col = i / perCol, row = i % perCol;
                int inCol = Math.Min(perCol, count - col * perCol);
                float x = frontX + dir * (offset + col * s + s / 2);
                float y = cy + (row - (inCol - 1) / 2f) * s;
                ulong h = Mix((ulong)seed * 0x9E3779B97F4A7C15UL + (ulong)(faction * 1000003 + serial++));
                x += ((h & 1023) / 1023f - 0.5f) * 0.3f;
                y += (((h >> 10) & 1023) / 1023f - 0.5f) * 0.3f;
                var u = world.SpawnUnit(def, faction, new Vector2(x, y));
                u.Facing = faction == 1 ? 0 : MathF.PI;
            }
            offset += cols * s + 0.8f;
        }
    }

    static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}

public sealed partial class World
{
    /// <summary>교전 테스트 월드인가: 건물 없음 · 유지비 없음 · 인구 제한 없음, 유닛이 전멸한 쪽이 패배.</summary>
    public bool BattleMode { get; private set; }
    /// <summary>교전 테스트 설정 (리플레이 머리말에 기록).</summary>
    public BattleSetup? Battle { get; private set; }

    /// <summary>세력 1 · 2 가 시작부터 전쟁 중인 빈 교전 월드 (점령 시설만 지도대로).</summary>
    internal static World CreateBattleWorld(GameMap map, Defs defs, BattleSetup setup)
    {
        var world = new World(map, defs, 2);
        world.BattleMode = true;
        world.Battle = setup;
        world.BattleAmmo = setup.Ammo;
        foreach (var (x, y, owner, kind) in map.ResourceSites)
            world.PlaceBuilding(defs.Buildings[kind], owner, x, y);
        return world;
    }

    /// <summary>교전 승패: 유닛이 하나도 없는 세력이 패배. 둘 다 없으면 무승부.</summary>
    void UpdateBattleVictory()
    {
        int a = 0, b = 0;
        foreach (var u in Units)
            if (u.Hp > 0 && !u.Surrendered) { if (u.Owner == 1) a++; else if (u.Owner == 2) b++; }
        if (a > 0 && b > 0) return;
        for (int i = 1; i <= 2; i++)
            if ((i == 1 ? a : b) == 0 && !Factions[i].Defeated)
                Factions[i].Defeated = true;
        if (a > 0) { Winner = 1; Winners.Add(1); }
        else if (b > 0) { Winner = 2; Winners.Add(2); }
        else Winner = -1;
    }
}
