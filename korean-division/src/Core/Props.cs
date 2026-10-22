using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

// F6 엄폐물 소품 층 (assets/data/props.json): 바위 · 나무 같은 낱개 소품. 엄폐 값 (사격자 쪽에 서 있는 병사의 명중 배수) ·
// 시선 부분 차단 (사격선이 지나가면 명중 x (1 - losBlock)) · 발자국 (blocksMove = 그 칸 통행 불가, 지도 고정) · 포탄에 부서짐 (hp 0 이하 -> destroyed 변종: 나무는 쓰러짐, 바위는 금감).
// 배치 = 지도 생성기 (GameMapProps.cs) · 미션 json "props" / "propScatter" · 나중에 편집기. 결정적: 해시 난수 + 사칙연산만.

public sealed class PropDef
{
    [JsonIgnore] public string Key { get; internal set; } = "";
    /// <summary>엄폐: 사격자 쪽 소품 뒤에 선 병사가 맞을 확률 배수 (작을수록 좋음).</summary>
    public float Cover { get; set; } = 1f;
    /// <summary>사격선이 소품 곁을 지날 때 막는 비율 (0~1).</summary>
    public float LosBlock { get; set; }
    /// <summary>발자국 반지름 (칸).</summary>
    public float Radius { get; set; } = 0.3f;
    /// <summary>true = 놓인 칸 통행 불가 (지도에 고정 - 금간 바위도 계속 막는다).</summary>
    public bool BlocksMove { get; set; }
    /// <summary>포탄 피해 한도. 0 = 안 부서짐.</summary>
    public float Hp { get; set; }
    /// <summary>부서진 뒤 변종 키 ("" = 없음).</summary>
    public string Destroyed { get; set; } = "";
}

/// <summary>propsV2 지형별 가중치 한 줄 (종류, 가중치 - 0 = 안 뽑음).</summary>
public sealed class PropWeight
{
    public string Type { get; set; } = "";
    public int W { get; set; }
}

public sealed class PropScatterRules
{
    public int Cell { get; set; } = 12;
    public int ClusterPct { get; set; } = 55;
    public int ForestEdgePct { get; set; } = 85;
    public int FieldPct { get; set; } = 30;
    public int ClusterMin { get; set; } = 2;
    public int ClusterMax { get; set; } = 5;
    public float ClusterRadius { get; set; } = 3f;
    public float Spacing { get; set; } = 1.1f;
    public int StartClear { get; set; } = 12;
    public int RockPct { get; set; } = 30;
    public int BigRockPct { get; set; } = 25;
    /// <summary>T28: true = 소품을 군집 위주로 (같은 군집 소품끼리 더 모이고 군집 수는 줄임, 총량 유지). false(기본) = 예전 그대로.</summary>
    public bool Clump { get; set; }
    public float ClumpPctMul { get; set; } = 0.75f;
    public float ClumpCountMul { get; set; } = 1.8f;
    public float ClumpRadiusMul { get; set; } = 0.7f;
    /// <summary>PR1 (기본 꺼짐): true = 기존 흩뿌리기 뒤에 자연 엄폐 소품 (덤불 · 수수밭 · 그루터기 · 통나무 · 노두 · 고사목 · 둔덕 · 버드나무 · 갈대 · 눈더미) 을 지형별로 더 놓는다. 미션 json "propsV2" 로도 켠다. false = 기존 지도 그대로 (비트 단위).</summary>
    public bool PropsV2 { get; set; }
    public int V2Cell { get; set; } = 14;
    public int V2ClusterPct { get; set; } = 45;
    public int V2ClusterMin { get; set; } = 2;
    public int V2ClusterMax { get; set; } = 4;
    public float V2ClusterRadius { get; set; } = 2.6f;
    /// <summary>지형 군집 키 (forestEdge · field · river · highland · snow) -> 가중치 목록 (목록 순서대로 누적 - 열거 순서 무관).</summary>
    public Dictionary<string, List<PropWeight>> V2 { get; set; } = new();
}

public sealed class PropRules
{
    public bool Enabled { get; set; } = true;
    public float CoverReach { get; set; } = 0.9f;
    public float CoverConeDeg { get; set; } = 60f;
    public float LosMargin { get; set; } = 0.2f;
    public float LosEndClear { get; set; } = 0.8f;
    public float LosMaxBlock { get; set; } = 0.6f;
    public float MinShellDamage { get; set; } = 40f;
    public float SpotCoverWeight { get; set; } = 1f;
    /// <summary>소품 뒤 (사격자 기준) 병사의 제압도 소품 엄폐 배수만큼 줄인다 (props2).</summary>
    public bool SuppressCover { get; set; } = true;
    /// <summary>T28: true = 숲 윤곽을 노이즈 경계 + 가장자리 성긴 칸으로 불규칙하게 (생성 맵 덩어리 · 지도 파일의 forest rect / circle 칠하기). false(기본) = 예전 지도 그대로.</summary>
    public bool OrganicForest { get; set; }
    public PropScatterRules Scatter { get; set; } = new();
}

public sealed class PropDefs
{
    public PropRules Rules { get; set; } = new();
    public Dictionary<string, PropDef> Types { get; set; } = new();
    [JsonIgnore] public float ConeCos2 { get; private set; }
    [JsonIgnore] public float MaxPad { get; private set; }

    static PropDefs? current;
    public static PropDefs Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "props.json"));
    /// <summary>[테스트] 규칙을 바꾼 정의로 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(PropDefs? d) => current = d;

    public static PropDefs Load(string path)
    {
        PropDefs d = new();
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<PropDefs>(File.ReadAllText(path), DataJson.Opts) is { } r) d = r;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        d.Finish();
        return d;
    }

    internal void Finish()
    {
        foreach (var kv in Types) kv.Value.Key = kv.Key;
        float c = DetMath.Cos(Rules.CoverConeDeg * (DetMath.Pi / 180f));
        ConeCos2 = c * c;
        float pad = Rules.CoverReach;
        foreach (var t in Types.Values) pad = MathF.Max(pad, t.Radius + Rules.LosMargin + 0.3f);
        MaxPad = pad;
    }

    public bool TryGet(string key, out PropDef def) => Types.TryGetValue(key, out def!);

    /// <summary>props.json 검증: 종류 범위 · 파괴 변종 참조 무결성 · propsV2 지형표 참조. 잘못이면 InvalidDataException.
    /// v2 표의 알 수 없는 종류는 산포 때 조용히 건너뛰므로 (시드 불변) 오류가 아니라 경고 로그 한 줄로 알린다.</summary>
    public void Validate()
    {
        void Req(bool ok, string msg) { if (!ok) throw new InvalidDataException("props.json: " + msg); }
        foreach (var (k, t) in Types)
        {
            Req(t.Cover > 0f && t.Cover <= 1f && t.LosBlock >= 0f && t.LosBlock <= 1f && t.Radius > 0f && t.Radius <= 2f && t.Hp >= 0f, $"소품 '{k}' 값 범위 (cover 0 초과 1 이하, losBlock 0..1, radius 0 초과 2 이하, hp >= 0)");
            if (t.Destroyed.Length == 0) continue;
            Req(Types.TryGetValue(t.Destroyed, out var d) && d.Hp == 0f && t.Hp > 0f && t.Destroyed != k, $"소품 '{k}': destroyed '{t.Destroyed}' 는 hp 0 인 다른 종류여야 하고 이 소품 hp > 0");
        }
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (biome, list) in Rules.Scatter.V2)
        {
            Req(GameMap.V2Biomes.Contains(biome), $"scatter.v2 지형 '{biome}' 알 수 없음");
            foreach (var e in list)
            {
                Req(e.W >= 0, $"scatter.v2.{biome}: 가중치 < 0 ('{e.Type}')");
                if (!Types.ContainsKey(e.Type)) unknown.Add(e.Type);
            }
        }
        if (unknown.Count > 0) Console.Error.WriteLine("[경고] props.json scatter.v2 에 알 수 없는 소품 종류 (산포에서 건너뜀): " + string.Join(", ", unknown));
    }
}

/// <summary>낱개 소품 한 개의 상태 (자리 · 종류는 지도 PropSeeds 와 같은 순서, 종류는 부서지면 변종으로 바뀜).</summary>
public sealed class Prop
{
    public int Id;
    public float X, Y;
    public string Type = "";
    public PropDef Def = null!;
    public float Hp;
    public bool Destroyed;   // 변종으로 바뀐 적 있음
    public Vector2 Pos => new(X, Y);
}

/// <summary>지도에 놓인 소품 상태 + 타일 격자 (spatial hash) 조회.</summary>
public sealed class PropLayer
{
    readonly GameMap map;
    readonly PropDefs defs;
    public readonly List<Prop> Items = new();
    readonly List<int>?[] cells;
    /// <summary>부서질 때마다 늘어남 (렌더 캐시용).</summary>
    public int Version;
    public int Destroyed;

    public PropLayer(GameMap map)
    {
        this.map = map;
        defs = PropDefs.Current;
        cells = new List<int>?[map.Width * map.Height];
        foreach (var s in map.PropSeeds) AddInternal(s.X, s.Y, s.Type);
    }

    public int Count => Items.Count;

    Prop AddInternal(float x, float y, string type)
    {
        if (!defs.TryGet(type, out var def)) throw new InvalidDataException($"알 수 없는 소품: {type}");
        var p = new Prop { Id = Items.Count, X = x, Y = y, Type = type, Def = def, Hp = def.Hp };
        Items.Add(p);
        float pad = MathF.Max(defs.Rules.CoverReach, def.Radius + defs.Rules.LosMargin + 0.3f);
        if (def.Destroyed.Length > 0 && defs.TryGet(def.Destroyed, out var dd)) pad = MathF.Max(pad, dd.Radius + defs.Rules.LosMargin + 0.3f);   // 변종이 더 클 수 있음
        int x0 = (int)MathF.Floor(x - pad), x1 = (int)MathF.Floor(x + pad), y0 = (int)MathF.Floor(y - pad), y1 = (int)MathF.Floor(y + pad);
        for (int ty = Math.Max(0, y0); ty <= Math.Min(map.Height - 1, y1); ty++)
        for (int tx = Math.Max(0, x0); tx <= Math.Min(map.Width - 1, x1); tx++)
            (cells[ty * map.Width + tx] ??= new List<int>(2)).Add(p.Id);
        return p;
    }

    /// <summary>World.AddProp 에서: 지도 PropSeeds 에 이미 넣은 소품을 층에도 넣는다.</summary>
    internal void Append(float x, float y, string type) => AddInternal(x, y, type);

    /// <summary>target 에 선 병사가 from (사격자) 쪽 소품에서 받는 명중 배수 (소품 없음 = 1). 소품 중심이 반경 coverReach 안 + 사격자 쪽 원뿔 안이면 엄폐, 여럿이면 가장 좋은 하나.</summary>
    public float CoverHit(Vector2 target, Vector2 from)
    {
        if (Items.Count == 0 || !defs.Rules.Enabled) return 1f;
        int tx = (int)MathF.Floor(target.X), ty = (int)MathF.Floor(target.Y);
        if ((uint)tx >= (uint)map.Width || (uint)ty >= (uint)map.Height) return 1f;
        var list = cells[ty * map.Width + tx];
        if (list == null) return 1f;
        float reach2 = defs.Rules.CoverReach * defs.Rules.CoverReach;
        Vector2 s = from - target;
        float ls2 = s.LengthSquared();
        float best = 1f;
        foreach (int id in list)
        {
            var p = Items[id];
            if (p.Def.Cover >= best) continue;
            Vector2 v = p.Pos - target;
            float d2 = v.LengthSquared();
            if (d2 > reach2) continue;
            if (d2 > 0.04f && ls2 > 1e-6f)
            {
                float dot = Vector2.Dot(v, s);
                if (dot <= 0 || dot * dot < defs.ConeCos2 * d2 * ls2) continue;   // 사격자 쪽 원뿔 밖
            }
            best = p.Def.Cover;
        }
        return best;
    }

    readonly List<int> seenBuf = new();

    /// <summary>사격선 a->b 가 소품 곁을 지날 때 곱하는 배수 (1 = 막힘 없음). 양 끝 coverReach / losEndClear 안의 소품은 제외 (표적 쪽은 엄폐로 센다), 하한 1 - losMaxBlock.</summary>
    public float LosMul(Vector2 a, Vector2 b)
    {
        if (Items.Count == 0 || !defs.Rules.Enabled) return 1f;
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        if (len2 < 1e-6f) return 1f;
        float len = MathF.Sqrt(len2);
        int steps = (int)(len / 0.5f) + 1;
        Vector2 stepV = ab / steps;
        float mul = 1f;
        float endA2 = defs.Rules.LosEndClear * defs.Rules.LosEndClear, endB2 = defs.Rules.CoverReach * defs.Rules.CoverReach;
        seenBuf.Clear();
        int lastCell = -1;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 q = a + stepV * i;
            int tx = (int)MathF.Floor(q.X), ty = (int)MathF.Floor(q.Y);
            if ((uint)tx >= (uint)map.Width || (uint)ty >= (uint)map.Height) continue;
            int ci = ty * map.Width + tx;
            if (ci == lastCell) continue;
            lastCell = ci;
            var list = cells[ci];
            if (list == null) continue;
            foreach (int id in list)
            {
                if (seenBuf.Contains(id)) continue;
                seenBuf.Add(id);
                var p = Items[id];
                if (p.Def.LosBlock <= 0) continue;
                Vector2 pa = p.Pos - a;
                if (pa.LengthSquared() <= endA2 || (p.Pos - b).LengthSquared() <= endB2) continue;
                float t = Vector2.Dot(pa, ab) / len2;
                if (t <= 0 || t >= 1) continue;
                float lim = p.Def.Radius + defs.Rules.LosMargin;
                if ((pa - ab * t).LengthSquared() > lim * lim) continue;
                mul *= 1f - p.Def.LosBlock;
            }
        }
        float floor = 1f - defs.Rules.LosMaxBlock;
        return mul < floor ? floor : mul;
    }

    /// <summary>폭발 피해: impact 반경 splash 안 (소품 반지름 포함) 소품에 w.Damage x 거리 감쇠. hp 0 이하 -> 변종. 부서진 수를 돌려준다.</summary>
    public int Blast(Vector2 impact, WeaponDef w)
    {
        if (Items.Count == 0 || !defs.Rules.Enabled || w.Damage < defs.Rules.MinShellDamage) return 0;
        float reach = w.Splash + defs.MaxPad;
        int x0 = Math.Max(0, (int)MathF.Floor(impact.X - reach)), x1 = Math.Min(map.Width - 1, (int)MathF.Floor(impact.X + reach));
        int y0 = Math.Max(0, (int)MathF.Floor(impact.Y - reach)), y1 = Math.Min(map.Height - 1, (int)MathF.Floor(impact.Y + reach));
        int broke = 0;
        for (int ty = y0; ty <= y1; ty++)
        for (int tx = x0; tx <= x1; tx++)
        {
            var list = cells[ty * map.Width + tx];
            if (list == null) continue;
            foreach (int id in list)
            {
                var p = Items[id];
                if ((int)MathF.Floor(p.X) != tx || (int)MathF.Floor(p.Y) != ty) continue;   // 중심 칸에서만 한 번
                if (p.Def.Hp <= 0) continue;
                float d = Vector2.Distance(impact, p.Pos) - p.Def.Radius;
                if (d > w.Splash) continue;
                p.Hp -= w.Damage * World.SplashFactor(w, d);
                if (p.Hp > 0) continue;
                if (p.Def.Destroyed.Length > 0 && defs.TryGet(p.Def.Destroyed, out var nd))
                {
                    p.Type = p.Def.Destroyed; p.Def = nd; p.Hp = nd.Hp; p.Destroyed = true;
                    Destroyed++; Version++; broke++;
                }
                else p.Hp = 0;
            }
        }
        return broke;
    }

    // ---------------------------------------------------------------- 저장 · 체크섬

    internal void Write(SnapWriter w)
    {
        w.I(Items.Count);
        foreach (var p in Items) { w.Str(p.Type); w.F(p.Hp); w.Bool(p.Destroyed); }
        w.I(Destroyed);
    }

    internal void Read(SnapReader r)
    {
        int n = r.I();
        if (n != Items.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "prop count"));
        foreach (var p in Items)
        {
            string t = r.Str() ?? "";
            if (!defs.TryGet(t, out var def)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "prop type"));
            p.Type = t; p.Def = def; p.Hp = r.F(); p.Destroyed = r.Bool();
            if (!float.IsFinite(p.Hp)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "prop hp"));
        }
        Destroyed = r.I();
        Version++;
    }
}

public sealed partial class World
{
    /// <summary>소품 층 (F6). 지도 PropSeeds 에서 만들고, 상태 (hp · 변종) 는 ORG_ 끝에 저장한다.</summary>
    [SnapExt] public PropLayer Props { get; private set; } = null!;

    void InitProps() => Props = new PropLayer(Map);

    /// <summary>소품을 하나 놓는다 (미션 · 시험). blocksMove 면 칸을 막는다. 지도 밖이면 false.</summary>
    public bool AddProp(float x, float y, string type)
    {
        if (!PropDefs.Current.TryGet(type, out var def) || !Map.InBounds((int)MathF.Floor(x), (int)MathF.Floor(y))) return false;
        Map.AddPropSeed(x, y, type, def.BlocksMove);
        Props.Append(x, y, type);
        return true;
    }

    /// <summary>실제 사격 판정용 명중률 (HitChance + [측정] 소품 엄폐 통계: 보병 표적에 소품 엄폐가 걸린 사격 수).</summary>
    internal float ShotChance(Entity shooter, Entity target, WeaponDef w, float dist)
    {
        if (Props.Count > 0 && target is Unit tu && tu.Def.IsInfantry && !IsIndirect(w))
        {
            Stats.PropShots++;
            if (PropCover(tu.Pos, shooter.Pos) < 1f) Stats.PropCoverShots++;
        }
        return HitChance(shooter, target, w, dist);
    }

    /// <summary>[hit 배수] 표적(target)을 from 에서 쏠 때의 소품 엄폐 - 지형 엄폐와는 겹치지 않고 좋은 쪽 (TerrainAccuracy).</summary>
    internal float PropCover(Vector2 target, Vector2 from) => Props.CoverHit(target, from);

    /// <summary>위협 방향 점: 표적이 있으면 표적, 없으면 바라보는 쪽 12칸 앞 (소품 엄폐 점수용).</summary>
    internal Vector2 ThreatPoint(Unit u) =>
        u.Target is { } t ? t.Pos : u.Pos + new Vector2(DetMath.Cos(u.Facing), DetMath.Sin(u.Facing)) * 12f;

    /// <summary>F1 사격 위치 · 피격 엄폐 찾기용 점수 (명중 배수처럼 낮을수록 좋음): 지형 엄폐와 위협 쪽 소품 엄폐 중 좋은 쪽.</summary>
    internal float CoverHitAt(Vector2 q, Unit u)
    {
        float h = Map.TerrainCover(q).Hit;
        if (Props.Count == 0 || !PropDefs.Current.Rules.Enabled) return h;
        float pc = 1f - PropDefs.Current.Rules.SpotCoverWeight * (1f - Props.CoverHit(q, ThreatPoint(u)));
        return pc < h ? pc : h;
    }

    /// <summary>미션 소품 흩뿌리기: 지도 생성기 규칙, 단 병력 2.5칸 안 · 배치 구역 · 증원 위치 4칸 안은 비운다 (막는 바위에 갇히지 않게).</summary>
    internal int ScatterMissionProps(MissionDef def)
    {
        var avoidAt = new List<Vector2>();
        foreach (var f in def.Reinforcements) avoidAt.Add(MissionDef.ForceCenterOf(def, f));
        var ug = new PointGrid(Map.Width, Map.Height, 2.5f);
        if (!GameMap.LegacyAvoid) foreach (var u in Units) if (u.Alive) ug.Add(u.Pos.X, u.Pos.Y);
        bool Avoid(float x, float y)
        {
            var p = new Vector2(x, y);
            if (GameMap.LegacyAvoid)   // 시험용 참조 구현
            {
                foreach (var u in Units) if (u.Alive && Vector2.DistanceSquared(u.Pos, p) < 6.25f) return true;
            }
            else
            {
                // PR5: 유닛 격자 - 후보만 좁히고 거리식은 그대로 (격자는 호출 시작 때 한 번 만든다)
                ug.Range(x, y, out int x0, out int y0, out int x1, out int y1);
                for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                    for (int i = ug.Head(cx, cy); i >= 0; i = ug.Next(i))
                        if (Vector2.DistanceSquared(ug.Pos[i], p) < 6.25f) return true;
            }
            foreach (var c in avoidAt) if (Vector2.DistanceSquared(c, p) < 16f) return true;
            foreach (var z in def.DeployZones) if (z.Contains(p)) return true;
            return false;
        }
        int from = Map.PropSeeds.Count;
        int n = Map.ScatterProps(false, Avoid, def.PropDensity, def.PropCell, def.PropsV2);
        for (int i = from; i < Map.PropSeeds.Count; i++) Props.Append(Map.PropSeeds[i].X, Map.PropSeeds[i].Y, Map.PropSeeds[i].Type);
        return n;
    }

    // ORG_ 끝, WriteFireControl 뒤 - FormatVersion 42
    void WriteProps(SnapWriter w) => Props.Write(w);
    void ReadProps(SnapReader r) => Props.Read(r);

    void HashProps(ref Hasher h)
    {
        h.Add(Props.Count);
        foreach (var p in Props.Items) { h.Add(p.Type); h.Add(p.X); h.Add(p.Y); h.Add(p.Hp); }
    }
}
