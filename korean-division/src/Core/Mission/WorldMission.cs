using System.Numerics;
using System.Text.Json;
using KoreanDivision.UI;

namespace KoreanDivision.Core;

/// <summary>
/// 미션 모드 (World.Mission != null): 미션 파일로 월드를 만들고, 매 틱 목표 · 사건 · 증원 · 실패 조건을 평가해 승패를 정한다.
/// 표준 승패 (도시 · 전멸) 대신 쓰인다 (WorldCapture.UpdateVictory 가 이리로 넘긴다). 모든 판정은 결정적 (목록 순서 · 틱 시간만).
/// 배치 단계: 첫 틱 전 (Mission.Started == false) 에만 ApplyDeployment 로 플레이어 부대를 배치 구역 안에서 옮길 수 있다 —
/// 위치는 '정의 + 최종 중심 · 방향' 만으로 계산되므로 몇 번 옮겼든 결과가 같고, 리플레이는 최종 목록만 다시 적용한다.
/// </summary>
public sealed partial class World
{
    /// <summary>미션 실행 상태 (없으면 일반 게임 · 교전 테스트). MISN 구역에 저장.</summary>
    [SnapExt] public MissionState? Mission { get; private set; }

    /// <summary>[훅] 사건으로 날씨 · 시간대가 바뀜 (구현 = Weather.cs). 시작 날씨는 CreateMissionWorld 가 SetupWeather 로 적용한다.</summary>
    partial void OnMissionWeather(string? weather, string? timeOfDay);

    // ================================================================ 생성

    /// <summary>미션 월드: 지도 + 세력 (팀 · 이름) + 시설 + 시작 병력 (+ 배치 단계 결과). 대도시 없음 — 승패는 목표로.</summary>
    public static World CreateMissionWorld(MissionDef def, Defs defs, int? seed = null, IEnumerable<DeployEntry>? deployment = null)
    {
        int s = seed ?? def.Seed;
        GameMap map = def.MapData is { ValueKind: JsonValueKind.Object } md
            ? MapFile.LoadText(md.GetRawText(), "mission_" + def.Id, s)
            : MapFile.Load(def.Map!, s);
        // 시작 위치 = 세력별 첫 병력 위치 (AI 의 '적 시작 위치' · 카메라 기준). 세력 정의는 미션 것으로 바꾼다.
        map.StartTiles.Clear();
        for (int f = 1; f <= def.FactionCount; f++)
        {
            var first = def.Forces.FirstOrDefault(x => x.Faction == f) ?? def.Reinforcements.FirstOrDefault(x => x.Faction == f);
            var p = first != null ? ForceCenter(def, first) : new Vector2(map.Width / 2f, map.Height / 2f);
            map.StartTiles.Add((Math.Clamp((int)p.X, 0, map.Width - 1), Math.Clamp((int)p.Y, 0, map.Height - 1)));
        }
        MapFile.SetMissionFactions(map, def.Factions.Select(f => new FactionDef { Name = f.Name, En = f.En, Color = f.Color, Lang = f.Lang, Team = f.Team }).ToList());
        var w = new World(map, defs, def.FactionCount);
        w.Mission = new MissionState(def);
        for (int f = 1; f <= def.FactionCount; f++) w.AssignTeam(f, def.Factions[f - 1].Team);
        MapFile.ApplyFactions(w);
        if (def.Weather != null || def.Time != null)   // 미션 시작 날씨 · 시간대 = 지도 설정을 덮어쓴다 (틱 0 전, Weather.cs 의 같은 길)
            w.SetupWeather(def.Weather != null ? WeatherState.ParseKind(def.Weather) : w.Weather.Kind,
                           def.Time != null ? WeatherState.ParseTime(def.Time) : w.Weather.Time);
        foreach (var (x, y, owner, kind) in map.ResourceSites)
        {
            int o = owner <= def.FactionCount ? owner : 0;
            var b = w.PlaceBuilding(defs.Buildings[kind], o, x, y);
            if (b != null && o != 0) { b.CaptureFaction = o; b.CaptureProgress = 1; }
        }
        foreach (var force in def.Forces) w.SpawnForce(force, ForceCenter(def, force), MissionDef.DirOf(force.Facing));
        foreach (var pr in def.Props) w.AddProp(pr.X, pr.Y, pr.Type);   // F6 소품 (손으로)
        if (def.PropScatter) w.ScatterMissionProps(def);
        foreach (var tr in def.Trains) w.SpawnTrain(tr);   // 열차 (Train.cs, T30a) — 병력 · 소품 뒤 (유닛 id 불변)
        w.Fog.Update();   // 배치 적용 전에 (배치 단계에서 옮긴 월드와 같은 안개 기억이 되도록 — 안개는 첫 틱부터 다시 찍힌다)
        w.Nav.Sync();
        if (deployment != null)
            foreach (var e in deployment.ToList())
                if (!w.ApplyDeployment(e.Force, e.At, e.Facing)) throw new InvalidDataException($"배치 기록이 맞지 않음: {e.Force}");
        return w;
    }

    static Vector2 ForceCenter(MissionDef def, MissionForce f) => MissionDef.ForceCenterOf(def, f);

    /// <summary>사람이 아닌 세력 (관전이면 전부) 에 미션 AI 를 붙인다 (세력 정의의 ai, 기본 "mission").</summary>
    public static void AttachMissionAi(World world, int humanFaction, bool all)
    {
        if (world.Mission is not { } m) return;
        for (int f = 1; f < world.Factions.Count; f++)
        {
            if (!all && f == humanFaction) continue;
            var fd = m.Def.Factions[f - 1];
            string name = fd.Ai ?? Environment.GetEnvironmentVariable("KD_MISSION_AI") ?? AiRegistry.Default;   // 기본 = 전술 AI (+ 보조 MissionAi: 호송 마차 · 증원)
            if (name == "none") continue;
            if (AiRegistry.Create(name) is not IAiController ai) continue;
            world.Controllers.Add((f, ai));
            world.Faction(f).IsAi = true;
            if (ai is TacticalAi tac)
            {
                var area = m.Def.Area(fd.Area);
                var posture = ParseMissionPosture(fd.Posture);
                float radius = area?.Radius ?? (posture == AiPosture.HoldArea ? 12 : 0);
                tac.SetDirective(f, posture, string.IsNullOrWhiteSpace(fd.AiProfile) ? null : fd.AiProfile, area?.Center, radius);
                tac.SetMissionParams(f, fd.AiWithdrawAt, fd.AiCommitment);   // stage 3A: 미션별 철수 문턱 · 목표 고수 정도
                DirectiveGroups(m.Def, f, fd, tac);
                tac.SetSkipUnarmed(f, true);
                world.Controllers.Add((f, new MissionAi(true)));
            }
        }
    }

    /// <summary>미션 병력 사이 거리가 이보다 가까우면 같은 지시 그룹 (구역 없는 hold · defend 의 흩어진 무리 나누기, 타일 — 지도 ×1.5 기준).</summary>
    public const float DirectiveClusterRange = 27f;

    static AiPosture ParseMissionPosture(string? p) => (p ?? "attack").ToLowerInvariant() switch { "defend" => AiPosture.Defend, "hold" => AiPosture.HoldArea, "counter" => AiPosture.Counter, _ => AiPosture.Attack };

    /// <summary>
    /// 전술 AI 지시 그룹 (2단계): 병력 (forces · reinforcements) 마다 태세 = posture ?? 세력 posture, 구역 = aiArea ?? 세력 area.
    /// ① 병력 자체 지시 (posture · aiArea) 가 있으면 (태세, 구역) 이 같은 병력끼리 한 그룹 — 구역 없는 hold · defend 는 병력마다 따로.
    /// ② 세력 기본 태세가 구역 없는 hold · defend 이면 나머지 병력을 위치 (DirectiveClusterRange 안 단일 연결) 로 묶어, 무리가 둘 이상일 때 무리마다 그룹
    ///    (흩어진 매복조가 한 방어선으로 합쳐지지 않게). 나머지 = 그룹 0 (세력 기본 — 증원도 생기면 세력 편성에 들어간다).
    /// </summary>
    static void DirectiveGroups(MissionDef def, int f, MissionFaction fd, IAiDirectable tac)
    {
        var forces = def.Forces.Concat(def.Reinforcements).Where(x => x.Faction == f).ToList();
        var fpost = ParseMissionPosture(fd.Posture);
        int next = 1;
        var keyed = new List<(string Key, AiPosture Post, MissionArea? Area, List<string> Ids)>();
        var rest = new List<MissionForce>();
        foreach (var x in forces)
        {
            if (x.Posture == null && x.AiArea == null) { rest.Add(x); continue; }
            var post = ParseMissionPosture(x.Posture ?? fd.Posture);
            string? an = x.AiArea ?? fd.Area;
            var area = def.Area(an);
            string key = area == null && post is not (AiPosture.Attack or AiPosture.Counter) ? "force:" + x.Id : post + "|" + an;
            int at = keyed.FindIndex(k => k.Key == key);
            if (at < 0) keyed.Add((key, post, area, new List<string> { x.Id }));
            else keyed[at].Ids.Add(x.Id);
        }
        foreach (var (_, post, area, ids) in keyed)
            tac.SetDirective(f, next++, post, area?.Center, area?.Radius ?? (post == AiPosture.HoldArea ? 12 : 0), ids);
        if (fpost is AiPosture.Attack or AiPosture.Counter || def.Area(fd.Area) != null || rest.Count < 2) return;
        // 위치 무리 (단일 연결, 병력 순서 고정)
        var cluster = new int[rest.Count];
        for (int i = 0; i < rest.Count; i++) cluster[i] = i;
        int Root(int i) { while (cluster[i] != i) i = cluster[i]; return i; }
        for (int i = 0; i < rest.Count; i++)
        for (int j = i + 1; j < rest.Count; j++)
            if (Vector2.Distance(ForceCenter(def, rest[i]), ForceCenter(def, rest[j])) <= DirectiveClusterRange)
            { int a = Root(i), b = Root(j); if (a != b) cluster[Math.Max(a, b)] = Math.Min(a, b); }
        var roots = Enumerable.Range(0, rest.Count).Select(Root).Distinct().ToList();
        if (roots.Count < 2) return;
        foreach (int r in roots)
        {
            var ids = Enumerable.Range(0, rest.Count).Where(i => Root(i) == r).Select(i => rest[i].Id).ToList();
            tac.SetDirective(f, next++, fpost, null, fpost == AiPosture.HoldArea ? 12 : 0, ids);
        }
    }

    // ---------------------------------------------------------------- 병력 배치

    /// <summary>병력 하나의 유닛 목록과 위치 (생성 순서 고정 — 배치 단계의 이동도 같은 순서로 계산).</summary>
    internal List<(UnitDef Def, Vector2 Pos, int Block)> LayoutForce(MissionForce f, Vector2 center, Vector2 dir)
    {
        var res = new List<(UnitDef, Vector2, int)>();
        var comps = f.AllCompanies().ToList();
        Vector2 right = new(-dir.Y, dir.X);
        var blocks = new List<(List<UnitDef> Defs, Vector2[] Local, float MinX, float MaxX, float MinY, float MaxY)>();
        foreach (var c in comps)
        {
            var list = new List<UnitDef>();
            foreach (var (k, n) in c.Units.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (Defs.Units.TryGetValue(k, out var ud)) for (int i = 0; i < n; i++) list.Add(ud);
            float maxR = 0.3f;
            foreach (var d in list) maxR = MathF.Max(maxR, d.Radius);
            var form = MissionCompany.ParseForm(c.Formation ?? f.Formation) ?? FormType.Line;
            var local = OrgLayout.CompanySlots(form, list.Count, 2 * maxR + 0.35f, 0, Defs.Formations);
            float x0 = 0, x1 = 0, y0 = 0, y1 = 0;
            foreach (var l in local) { x0 = MathF.Min(x0, l.X); x1 = MathF.Max(x1, l.X); y0 = MathF.Min(y0, l.Y); y1 = MathF.Max(y1, l.Y); }
            blocks.Add((list, local, x0, x1, y0, y1));
        }
        string bf = (f.BattalionForm ?? "line").ToLowerInvariant();
        // 블록 중심 (로컬): line = 나란히, column = 앞뒤로, echelon = 오른쪽으로 갈수록 뒤로, reserve = 앞줄 2/3 + 뒷줄
        var centers = new Vector2[blocks.Count];
        const float gap = OrgLayout.BlockGap;
        if (bf == "column")
        {
            float y = 0;
            for (int i = 0; i < blocks.Count; i++) { centers[i] = new Vector2(0, y - blocks[i].MaxY); y -= blocks[i].MaxY - blocks[i].MinY + gap; }
        }
        else
        {
            var wv = Defs.Formations.Waves;   // 파상: 중대를 2~3줄 물결로 (OrgLayout.Plan 과 같은 줄 수 · 간격)
            int waveRows = Math.Clamp(blocks.Count >= wv.ThreeRowsAt ? 3 : wv.Rows, 1, Math.Max(1, blocks.Count));
            int front = bf == "reserve" && blocks.Count >= 2 ? Math.Max(1, (blocks.Count * 2 + 2) / 3) : bf == "waves" && blocks.Count >= 2 ? (blocks.Count + waveRows - 1) / waveRows : blocks.Count;
            float rowY = 0;
            for (int start = 0; start < blocks.Count; start += front)
            {
                int end = Math.Min(blocks.Count, start + front);
                float rowW = 0, depth = 0;
                for (int i = start; i < end; i++) rowW += blocks[i].MaxX - blocks[i].MinX + (i > start ? gap : 0);
                float x = -rowW / 2;
                for (int i = start; i < end; i++)
                {
                    float y = rowY - blocks[i].MaxY;
                    if (bf == "echelon") y -= (i - start) * ((blocks[i].MaxY - blocks[i].MinY) * 0.5f + gap);
                    centers[i] = new Vector2(x - blocks[i].MinX, y);
                    x += blocks[i].MaxX - blocks[i].MinX + gap;
                    depth = MathF.Max(depth, blocks[i].MaxY - blocks[i].MinY);
                }
                rowY -= depth + (bf == "waves" ? wv.DepthGap : gap * 2);
            }
        }
        // 전체 경계 상자 중심을 center 에
        float bx0 = float.MaxValue, bx1 = float.MinValue, by0 = float.MaxValue, by1 = float.MinValue;
        for (int i = 0; i < blocks.Count; i++)
        {
            bx0 = MathF.Min(bx0, centers[i].X + blocks[i].MinX); bx1 = MathF.Max(bx1, centers[i].X + blocks[i].MaxX);
            by0 = MathF.Min(by0, centers[i].Y + blocks[i].MinY); by1 = MathF.Max(by1, centers[i].Y + blocks[i].MaxY);
        }
        Vector2 mid = blocks.Count > 0 ? new((bx0 + bx1) / 2, (by0 + by1) / 2) : Vector2.Zero;
        center = FitDeployInMap(blocks.Select(b => (b.Local, b.MinX, b.MaxX, b.MinY, b.MaxY)).ToList(), centers, mid, center, dir);
        for (int i = 0; i < blocks.Count; i++)
            for (int k = 0; k < blocks[i].Defs.Count; k++)
            {
                var l = blocks[i].Local[k] + centers[i] - mid;
                res.Add((blocks[i].Defs[k], center + right * l.X + dir * l.Y, i));
            }
        return res;
    }

    /// <summary>배치 대형 가장자리와 지도 끝 사이 여유 (칸).</summary>
    public const float DeployEdgeMargin = 1.5f;

    /// <summary>
    /// bunchfix (2026-10-19, docs/BUNCHING_DIAG_2026-10-19.md 시작 직후 가장자리 중대): 병력 배치 대형이 지도 밖으로 삐져나가면
    /// (옛: 지도 밖 병사가 가장자리 한 칸으로 모여 시작 5초 안에 한 덩어리 + 종대 전환) 대형을 지도 안으로 옮긴다 — 반환 = 새 중심.
    /// 대형이 지도보다 넓으면 그 축의 중대 블록 중심 간격을 줄인다 (centers 를 고침, 블록 안 병사 간격은 그대로). 순수 함수 (정의 + 중심 + 방향).
    /// </summary>
    Vector2 FitDeployInMap(List<(Vector2[] Local, float MinX, float MaxX, float MinY, float MaxY)> blocks, Vector2[] centers, Vector2 mid, Vector2 center, Vector2 dir)
    {
        if (blocks.Count == 0) return center;
        Vector2 right = new(-dir.Y, dir.X);
        float lo = DeployEdgeMargin, hiX = Map.Width - DeployEdgeMargin, hiY = Map.Height - DeployEdgeMargin;
        (float X0, float X1, float Y0, float Y1) Box(Vector2 c)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            for (int i = 0; i < blocks.Count; i++)
                foreach (var lp in blocks[i].Local)
                {
                    var l = lp + centers[i] - mid;
                    var p = c + right * l.X + dir * l.Y;
                    x0 = MathF.Min(x0, p.X); x1 = MathF.Max(x1, p.X); y0 = MathF.Min(y0, p.Y); y1 = MathF.Max(y1, p.Y);
                }
            return (x0, x1, y0, y1);
        }
        var bx = Box(center);
        if (bx.X0 >= lo && bx.X1 <= hiX && bx.Y0 >= lo && bx.Y1 <= hiY) return center;
        // 지도보다 넓은 축 → 블록 중심 (mid 기준) 을 그 비율로 줄임 (한 번 — 블록 자체가 지도보다 넓으면 그대로 두고 옮기기만)
        float fx = (bx.X1 - bx.X0) > hiX - lo ? (hiX - lo) / (bx.X1 - bx.X0) : 1f;
        float fy = (bx.Y1 - bx.Y0) > hiY - lo ? (hiY - lo) / (bx.Y1 - bx.Y0) : 1f;
        if (fx < 1f || fy < 1f)
        {
            for (int i = 0; i < centers.Length; i++)
            {
                Vector2 off = centers[i] - mid;
                Vector2 wv = right * off.X + dir * off.Y;   // 세계 축으로 줄인 뒤 로컬로 되돌림
                wv = new Vector2(wv.X * fx, wv.Y * fy);
                centers[i] = mid + new Vector2(Vector2.Dot(wv, right), Vector2.Dot(wv, dir));
            }
            bx = Box(center);
        }
        float sx = bx.X0 < lo ? lo - bx.X0 : bx.X1 > hiX ? hiX - bx.X1 : 0f;
        float sy = bx.Y0 < lo ? lo - bx.Y0 : bx.Y1 > hiY ? hiY - bx.Y1 : 0f;
        return center + new Vector2(sx, sy);
    }

    /// <summary>설 수 있는 칸으로 (SpawnUnit 과 같은 규칙).</summary>
    Vector2 WalkablePos(Vector2 pos)
    {
        var (tx, ty) = GameMap.TileOf(pos);
        tx = Math.Clamp(tx, 0, Map.Width - 1); ty = Math.Clamp(ty, 0, Map.Height - 1);
        var (ox, oy) = GameMap.TileOf(pos);
        if (Map.InBounds(ox, oy) && Map.IsWalkable(ox, oy)) return pos;
        (tx, ty) = Map.NearestWalkable(tx, ty);
        return new Vector2(tx + 0.5f, ty + 0.5f);
    }

    /// <summary>병력 생성 (시작 병력 · 증원). 중대 편성 · 대대 번호 · 계급 · 태그 등록.</summary>
    void SpawnForce(MissionForce f, Vector2 center, Vector2 dir)
    {
        var m = Mission!;
        var comps = f.AllCompanies().ToList();
        var layout = LayoutForce(f, center, dir);
        float facing = DetMath.Atan2(dir.Y, dir.X);
        var ids = m.ForceUnits.TryGetValue(f.Id, out var have) ? have : m.ForceUnits[f.Id] = new List<int>();
        var perBlock = new List<Unit>[comps.Count];
        var officerIdx = new int[comps.Count];
        var infIdx = new int[comps.Count];
        bool explicitRanks = false;
        for (int i = 0; i < comps.Count; i++) { perBlock[i] = new List<Unit>(); explicitRanks |= comps[i].OfficerRanks is { Length: > 0 }; }
        foreach (var (def, pos, block) in layout)
        {
            var u = SpawnUnit(def, f.Faction, WalkablePos(pos));
            u.Facing = facing;
            if (comps[block].Weapon is { } ow && u.Def.Weapon == Defs.RifleSlot) u.Weapon = Defs.Weapon(ow);   // 미션이 소총을 고정 (N54)
            if (comps[block].Rank is { } mr && !u.IsOfficer)   // 명시 계급 (0 = 훈련병 허용); 없으면 SpawnUnit 의 기본 1
            {
                int rank = RankNames.Snap(FactionEn(f.Faction), mr);   // 미사용 계급은 아래로 (ranks.json null)
                u.GrantKills(rank > 0 ? Defs.CombatRules.RankKills[Math.Min(rank, Defs.CombatRules.RankKills.Length) - 1] : 0);
            }
            if (comps[block].InfantryRanks is { } ir && !u.IsOfficer && u.Def.IsInfantry && !u.Def.Heavy && !u.Def.Scout && infIdx[block] < ir.Length)   // 캠페인 이월 개별 계급
            {
                int rank = RankNames.Snap(FactionEn(f.Faction), ir[infIdx[block]++]);
                u.GrantKills(rank > 0 ? Defs.CombatRules.RankKills[Math.Min(rank, Defs.CombatRules.RankKills.Length) - 1] : 0);
            }
            if (u.IsOfficer)   // 미션이 정한 장교 계급 · 경험치 (CommandRanks.cs)
            {
                int oi = officerIdx[block]++;
                if (comps[block].OfficerRanks is { } ors && oi < ors.Length) u.OfficerBase = (byte)Math.Clamp(ors[oi], 0, Defs.Officers.MaxRank);
                u.OfficerXp = Math.Clamp(comps[block].OfficerXp, 0, Defs.Officers.XpMax);
            }
            ids.Add(u.Id);
            m.StartRank[u.Id] = Math.Max(0, u.Rank);
            if (comps[block].Tag is { Length: > 0 } tag) (m.Tags.TryGetValue(tag, out var tl) ? tl : m.Tags[tag] = new List<int>()).Add(u.Id);
            if (f.Faction < m.Spawned.Length) m.Spawned[f.Faction]++;
            perBlock[block].Add(u);
        }
        int battalion = f.Battalion ?? (comps.Count >= 2 ? FreeBattalionNumber(f.Faction) : 0);
        for (int i = 0; i < comps.Count; i++)
        {
            var units = perBlock[i];
            bool company = comps[i].Company ?? units.Any(u => u.Def.Category == "infantry") && units.Count >= 2;
            if (!company || units.Count == 0) continue;
            ExecFormCompany(f.Faction, units, Math.Clamp(battalion, 0, 9), spawn: explicitRanks ? SpawnRanks.Explicit : SpawnRanks.Auto);
            if (units[0].Company is { } c)
            {
                AssignSquadLeaders(c, comps[i], m);
                c.Form = MissionCompany.ParseForm(comps[i].Formation ?? f.Formation) ?? FormType.Line;
                c.Facing = dir;
                if (c.Battalion is { } r) r.Form = (f.BattalionForm ?? "line").ToLowerInvariant() switch { "column" => BatForm.Column, "echelon" => BatForm.Echelon, "reserve" => BatForm.TwoFrontReserve, "waves" => BatForm.Waves, _ => BatForm.Line };
            }
        }
    }

    /// <summary>
    /// 부사관 기본 편성 (2026-10-19 사용자 승인, 관찰 7차 §35): 보병 분대 · 기병 분대 (cavcmd 결정 2 — 기병도 category infantry + Mounted) 마다 분대장 (분대의 최고 계급 · 같으면 Id 작은 쪽 = Squad.Leader) 을
    /// 분대장 계급 (미션 squadLeaderRank, 없으면 combat.squadLeaderRank = 하사) 으로 — 이미 그 이상이면 그대로. 미션이 병력 계급 (rank) 을 정했으면 기본 규칙은 쓰지 않는다.
    /// 배치 때 한 번 (StartKills — 저장 · 체크섬에 이미 포함).
    /// </summary>
    void AssignSquadLeaders(Company c, MissionCompany mc, MissionState m)
    {
        int rank = mc.SquadLeaderRank ?? (mc.Rank is > 0 ? 0 : Defs.CombatRules.SquadLeaderRank);
        rank = RankNames.Snap(FactionEn(c.Owner), Math.Min(rank, Defs.CombatRules.RankKills.Length));
        if (rank <= 0) return;
        foreach (var q in c.Squads)
        {
            if (q.Leader is not { } l || !l.Def.IsInfantry || l.Rank >= rank) continue;   // 기병 (Mounted) 포함 — 장교 상실 뒤 N3 부사관 대리
            l.GrantKills(Defs.CombatRules.RankKills[rank - 1]);
            m.StartRank[l.Id] = l.Rank;
        }
        // S2: 장교 1명 이하인 소형 중대 (기관총 · 돌격 · 기병) 는 분대마다 부사관 한 명 더 (부분대장) — 분대장이 전사해도 다음 분대장도 부사관이라
        // 장교 상실 뒤 ActingNco 가 영구히 끊기지 않는다. 분대 4명 이상, 분대장 다음 낮은 Id 의 보병 (중화기 · 장교 제외).
        int offs = 0;
        foreach (var u in c.Members) if (u.IsOfficer) offs++;
        if (offs > 1) return;
        foreach (var q in c.Squads)
        {
            if (q.Members.Count < 4 || q.Leader is not { } ld) continue;
            foreach (var d in q.Members)
                if (d != ld && d.Alive && d.Def.IsInfantry && !d.Def.Heavy && !d.Def.Scout && !d.IsOfficer && d.Rank < rank)
                {
                    d.GrantKills(Defs.CombatRules.RankKills[rank - 1]);
                    m.StartRank[d.Id] = d.Rank;
                    break;
                }
        }
    }

    /// <summary>세력 번호 (1부터) → 지도 factions[].en (계급 이름 키). 범위 밖 = null.</summary>
    string? FactionEn(int faction)
    {
        var fd = MapFile.FactionsOf(Map);
        return faction >= 1 && faction <= fd.Count ? fd[faction - 1].En : null;
    }

    /// <summary>플레이어가 배치 단계에서 옮길 수 있는 병력인가.</summary>
    public bool IsDeployable(MissionForce f) =>
        Mission is { } m && f.Faction == m.Player && (f.Deployable ?? true) && m.Def.DeployZones.Any(z => z.Faction == f.Faction && z.Contains(ForceCenter(m.Def, f)));

    /// <summary>at 이 그 세력의 배치 구역 안인가.</summary>
    public bool InDeployZone(int faction, Vector2 at) => Mission is { } m && m.Def.DeployZones.Any(z => z.Faction == faction && z.Contains(at));

    /// <summary>
    /// [배치 단계 전용 — 첫 틱 전] 병력을 배치 구역 안의 새 중심 · 방향으로 옮긴다. 위치는 정의 + (at, facing) 만으로 다시 계산하므로
    /// 중간에 몇 번 옮겼든 마지막 값이 같으면 같은 상태가 된다. 성공하면 Deployment 목록 (병력 id 순) 갱신.
    /// </summary>
    public bool ApplyDeployment(string forceId, Vector2 at, float facing)
    {
        if (Mission is not { } m || m.Started || Tick != 0) return false;
        var f = m.Def.Forces.FirstOrDefault(x => x.Id == forceId);
        if (f == null || !IsDeployable(f) || !InDeployZone(f.Faction, at)) return false;
        if (!m.ForceUnits.TryGetValue(forceId, out var ids)) return false;
        facing = ((MathF.Round(facing) % 360) + 360) % 360;
        var dir = MissionDef.DirOf(facing);
        var layout = LayoutForce(f, at, dir);
        if (layout.Count != ids.Count) return false;
        float yaw = DetMath.Atan2(dir.Y, dir.X);
        for (int i = 0; i < ids.Count; i++)
        {
            if (Find(ids[i]) is not Unit u) return false;
            u.Pos = WalkablePos(layout[i].Pos);
            u.Facing = yaw;
            if (u.Company is { } c) c.Facing = dir;
        }
        hashDirty = true;
        m.Deployment.RemoveAll(d => d.Force == forceId);
        m.Deployment.Add(new DeployEntry(forceId, at, facing));
        m.Deployment.Sort((a, b) => string.CompareOrdinal(a.Force, b.Force));
        return true;
    }

    /// <summary>[표시] 병력의 현재 중심 (살아 있는 유닛 평균).</summary>
    public Vector2? ForceCentroid(string forceId)
    {
        if (Mission is not { } m || !m.ForceUnits.TryGetValue(forceId, out var ids)) return null;
        Vector2 s = Vector2.Zero; int n = 0;
        foreach (int id in ids) if (Find(id) is Unit u) { s += u.Pos; n++; }
        return n > 0 ? s / n : null;
    }

    /// <summary>[표시 · 배치] 병력의 기준 중심 (배치 기록 → 정의 위치).</summary>
    public Vector2 ForceAnchor(string forceId)
    {
        if (Mission is not { } m) return Vector2.Zero;
        foreach (var d in m.Deployment) if (d.Force == forceId) return d.At;
        var f = m.Def.Forces.FirstOrDefault(x => x.Id == forceId) ?? m.Def.Reinforcements.FirstOrDefault(x => x.Id == forceId);
        return f != null ? ForceCenter(m.Def, f) : Vector2.Zero;
    }

    /// <summary>[표시] 배치 단계의 병력 방향 (기록 없으면 정의값).</summary>
    public float ForceFacing(string forceId)
    {
        if (Mission is not { } m) return 0;
        foreach (var d in m.Deployment) if (d.Force == forceId) return d.Facing;
        return m.Def.Forces.FirstOrDefault(x => x.Id == forceId)?.Facing ?? 0;
    }

    // ================================================================ 매 틱

    /// <summary>WorldCapture.UpdateVictory 가 미션 모드에서 부른다 (Winner == 0 일 때만).</summary>
    void UpdateMission(float dt)
    {
        var m = Mission!;
        var def = m.Def;
        m.Started = true;
        if (m.Result != MissionState.Running) return;
        float t = (Tick + 1) * Config.SimDt;
        if (m.Alive.Length != Factions.Count) m.Alive = new int[Factions.Count];
        Array.Clear(m.Alive);
        foreach (var u in Units) if (u.Hp > 0 && !u.Surrendered && (uint)u.Owner < (uint)m.Alive.Length) m.Alive[u.Owner]++;
        if (m.AliveCars.Length != Factions.Count) { m.AliveCars = new int[Factions.Count]; m.TotalCars = new int[Factions.Count]; }
        Array.Clear(m.AliveCars); Array.Clear(m.TotalCars);
        foreach (var tr in Trains)   // T40: 살아 있는 객차는 병력 (기관차 없는 정지 열차도 포함, 잔해 제외)
            if ((uint)tr.Owner < (uint)m.AliveCars.Length)
                foreach (var c in tr.Cars) { m.TotalCars[tr.Owner]++; if (c.Alive) m.AliveCars[tr.Owner]++; }

        for (int i = 0; i < def.Reinforcements.Count; i++)
            if (!m.ReinforcementSpawned[i] && def.Reinforcements[i].Time is float rt && t >= rt) SpawnReinforcement(i);
        UpdateDueReinforcements(m);   // TC-B 지연 증원
        UpdatePatrols(m);             // TC-B 순찰

        for (int i = 0; i < def.Objectives.Count; i++)
            if (m.ObjState[i] == MissionState.Active)
                EvalObjective(m, i, t, dt);

        for (int i = 0; i < def.Events.Count; i++)
            if (!m.EventFired[i] && EventReady(m, def.Events[i].When, t))
            {
                m.EventFired[i] = true;
                foreach (var a in def.Events[i].Do) RunAction(m, a);
            }

        // 승패
        bool primaryFailed = false, allDone = true;
        for (int i = 0; i < def.Objectives.Count; i++)
        {
            if (!def.Objectives[i].Primary) continue;
            if (m.ObjState[i] == MissionState.Failed) primaryFailed = true;
            if (m.ObjState[i] != MissionState.Done) allDone = false;
        }
        int fail = -1;
        for (int i = 0; i < def.Failures.Count && fail < 0; i++)
            if (FailureTriggered(m, def.Failures[i], t, allDone)) fail = i;
        bool wiped = m.Alive[m.Player] == 0 && m.AliveCars[m.Player] == 0 && !PendingReinforcement(m, m.Player);
        if (primaryFailed || fail >= 0 || (wiped && !allDone)) EndMission(m, false, fail);
        else if (allDone) EndMission(m, true, -1);
    }

    bool PendingReinforcement(MissionState m, int faction)
    {
        for (int i = 0; i < m.Def.Reinforcements.Count; i++)
            if (!m.ReinforcementSpawned[i] && m.Def.Reinforcements[i].Faction == faction && (m.Def.Reinforcements[i].Time != null || m.ReinforcementDue[i] > 0)) return true;
        return false;
    }

    void SpawnReinforcement(int i)
    {
        var m = Mission!;
        if (m.ReinforcementSpawned[i]) return;
        m.ReinforcementSpawned[i] = true;
        var r = m.Def.Reinforcements[i];
        SpawnForce(r, ForceCenter(m.Def, r), MissionDef.DirOf(r.Facing));
        if (Allied(r.Faction, m.Player)) m.Messages.Add((Tick, string.Format(S.MsReinforce, r.Name ?? r.Id)));
    }

    /// <summary>구역 안의 (faction 과 같은 편) 점령 가능 유닛 수 · 적대 유닛 수.</summary>
    (int Own, int Enemy) AreaCount(MissionArea a, int faction)
    {
        int own = 0, enemy = 0;
        foreach (var u in Units)
        {
            if (u.Hp <= 0 || !a.Contains(u.Pos)) continue;
            if (Allied(faction, u.Owner)) { if (u.Def.CanCapture) own++; }
            else if (IsHostile(faction, u.Owner)) enemy++;
        }
        return (own, enemy);
    }

    int AliveOf(IReadOnlyList<int> ids)
    {
        int n = 0;
        foreach (int id in ids) if (Find(id) is Unit) n++;
        return n;
    }

    void EvalObjective(MissionState m, int i, float t, float dt)
    {
        var o = m.Def.Objectives[i];
        int side = o.Faction > 0 ? o.Faction : m.Player;
        if (o.Train != null) { EvalFreightObjective(m, i, o, side, dt); return; }   // T30c 화물열차 목표 (FreightObjective.cs)
        switch (o.Type)
        {
            case "capture":
            {
                var (own, enemy) = AreaCount(m.Def.Area(o.Area)!, side);
                if (own > 0 && enemy == 0) m.ObjProgress[i] += dt;
                else if (enemy > 0 && own == 0) m.ObjProgress[i] = MathF.Max(0, m.ObjProgress[i] - dt);
                if (m.ObjProgress[i] >= o.Seconds && own > 0 && enemy == 0) m.ObjState[i] = MissionState.Done;
                break;
            }
            case "eliminate":
            {
                if (!string.IsNullOrEmpty(o.Tag))
                {
                    var ids = m.TagUnits(o.Tag);
                    int alive = AliveOf(ids);
                    m.ObjProgress[i] = ids.Count == 0 ? 0 : 1f - alive / (float)ids.Count;
                    if (ids.Count > 0 && alive == 0) m.ObjState[i] = MissionState.Done;
                    break;
                }
                int spawned = 0, aliveN = 0;
                for (int f = 1; f < m.Spawned.Length; f++)
                {
                    bool target = o.Faction > 0 ? f == o.Faction : IsHostile(m.Player, f);
                    if (!target) continue;
                    spawned += m.Spawned[f] + m.TotalCars[f];   // T40 객차 포함 (열차 없으면 0 → 불변)
                    aliveN += m.Alive[f] + m.AliveCars[f];
                }
                float pct = spawned == 0 ? 0 : (spawned - aliveN) * 100f / spawned;
                m.ObjProgress[i] = pct;
                if (spawned > 0 && (o.Percent >= 100 ? aliveN == 0 : pct >= o.Percent)) m.ObjState[i] = MissionState.Done;
                break;
            }
            case "defend":
            {
                if (m.Def.Area(o.Area) is { } a)
                {
                    var (own, enemy) = AreaCount(a, side);
                    m.ObjProgress[i] = enemy > 0 && own == 0 ? m.ObjProgress[i] + dt : 0;
                    if (m.ObjProgress[i] >= o.LoseAfter) { m.ObjState[i] = MissionState.Failed; break; }
                }
                if (t >= o.Seconds) m.ObjState[i] = MissionState.Done;
                break;
            }
            case "escort":
            {
                var ids = m.TagUnits(o.Tag);
                var a = m.Def.Area(o.Area)!;
                int alive = 0, inside = 0;
                foreach (int id in ids)
                    if (Find(id) is Unit u) { alive++; if (a.Contains(u.Pos)) inside++; }
                m.ObjProgress[i] = inside;
                int need = Math.Max(1, o.Count);
                if (inside >= need) m.ObjState[i] = MissionState.Done;
                else if (alive < need) m.ObjState[i] = MissionState.Failed;
                break;
            }
        }
    }

    bool EventReady(MissionState m, MissionCondition c, float t)
    {
        if (c.Time is float ct && t < ct) return false;
        if (c.Area != null)
        {
            var a = m.Def.Area(c.Area)!;
            int f = c.Faction > 0 ? c.Faction : m.Player;
            bool any = false;
            foreach (var u in Units) if (u.Hp > 0 && u.Owner == f && a.Contains(u.Pos)) { any = true; break; }
            if (!any) return false;
        }
        if (c.Objective != null)
        {
            int idx = m.Def.Objectives.FindIndex(o => o.Id == c.Objective);
            int want = (c.State ?? "done").ToLowerInvariant() == "failed" ? MissionState.Failed : MissionState.Done;
            if (idx < 0 || m.ObjState[idx] != want) return false;
        }
        if (c.Killed != null)
        {
            var ids = m.TagUnits(c.Killed);
            if (ids.Count == 0 || AliveOf(ids) > 0) return false;
        }
        if (c.SeenBy > 0 && !SeenByFaction(m, c)) return false;   // TC-B 감지 경보
        return true;
    }

    void RunAction(MissionState m, MissionAction a)
    {
        if (a.Spawn != null)
        {
            int i = m.Def.Reinforcements.FindIndex(r => r.Id == a.Spawn);
            if (i >= 0) { if (a.Delay > 0) ScheduleReinforcement(m, i, a.Delay); else SpawnReinforcement(i); }
        }
        if (a.Message != null) m.Messages.Add((Tick, a.Message));
        if (a.Weather != null || a.Time != null)
        {
            if (a.Weather != null) m.Weather = a.Weather;
            if (a.Time != null) m.TimeOfDay = a.Time;
            OnMissionWeather(m.Weather, m.TimeOfDay);
        }
        if (a.Reveal != null)
        {
            int i = m.Def.Objectives.FindIndex(o => o.Id == a.Reveal);
            if (i >= 0 && !m.ObjRevealed[i]) { m.ObjRevealed[i] = true; m.Messages.Add((Tick, string.Format(S.MsNewObjective, m.Def.Objectives[i].Text ?? m.Def.Objectives[i].Id))); }
        }
    }

    bool FailureTriggered(MissionState m, MissionFailure f, float t, bool allDone)
    {
        switch (f.Type)
        {
            case "losses":
            {
                int sp = m.Spawned[m.Player];
                return sp > 0 && (sp - m.Alive[m.Player]) * 100f / sp >= f.Percent;
            }
            case "keyUnit":
            {
                var ids = m.TagUnits(f.Tag);
                return ids.Count > 0 && AliveOf(ids) < ids.Count;
            }
            case "timeLimit": return f.Seconds > 0 && t >= f.Seconds && !allDone;
        }
        return false;
    }

    void EndMission(MissionState m, bool won, int failReason)
    {
        m.Result = won ? MissionState.Won : MissionState.Lost;
        m.EndTick = Tick + 1;
        m.FailReason = failReason;
        Winners.Clear();
        for (int f = 1; f < Factions.Count; f++)
        {
            bool winner = won ? Allied(m.Player, f) : IsHostile(m.Player, f);
            if (winner) Winners.Add(f);
            else Factions[f].Defeated = true;
        }
        Winner = Winners.Count > 0 ? Winners[0] : -1;
    }

    /// <summary>[치트 · 테스트] 미션을 즉시 끝낸다 (명령 MissionEndCommand 로만 — 리플레이 재현).</summary>
    void ExecMissionEnd(bool won)
    {
        if (Mission is not { } m || m.Result != MissionState.Running) return;
        CheatsUsed = true;
        EndMission(m, won, -1);
    }

    // ================================================================ 저장 · 체크섬

    void WriteMission(SnapWriter w)
    {
        w.Bool(Mission != null);
        Mission?.Write(w);
    }

    void ReadMission(SnapReader r)
    {
        Mission = r.Bool() ? MissionState.Read(r) : null;
        if (Mission != null && Map.Id != null) MapFile.ReattachMissionFactions(Map, Mission.Def);
    }

    partial void HashMission(ref Hasher h);
    partial void HashMission(ref Hasher h)
    {
        if (Mission is not { } m) return;
        h.Add(m.Def.Hash);
        h.Add(m.Started ? 1 : 0);
        h.Add(m.Result); h.Add(m.EndTick); h.Add(m.FailReason);
        for (int i = 0; i < m.ObjState.Length; i++) { h.Add(m.ObjState[i]); h.Add(m.ObjProgress[i]); h.Add(m.ObjRevealed[i] ? 1 : 0); }
        foreach (var b in m.EventFired) h.Add(b ? 1 : 0);
        foreach (var b in m.ReinforcementSpawned) h.Add(b ? 1 : 0);
        foreach (var n in m.Spawned) h.Add(n);
        h.Add(m.Messages.Count);
        h.Add(m.Weather ?? ""); h.Add(m.TimeOfDay ?? "");
        foreach (var d in m.Deployment) { h.Add(d.Force); h.Add(d.At.X); h.Add(d.At.Y); h.Add(d.Facing); }
        foreach (var (k, l) in m.Tags) { h.Add(k); h.Add(l.Count); }
        HashAlertState(m, ref h);
    }
}

public sealed partial class MapFile
{
    /// <summary>미션 세력 정의를 지도에 붙인다 (지도 파일의 factions 대신 — 이름 · 색 · 언어 · 팀).</summary>
    internal static void SetMissionFactions(GameMap map, List<FactionDef> defs) => factionTable.AddOrUpdate(map, defs);

    /// <summary>스냅샷 복원 뒤: 팀 정보까지 미션 정의에서 다시 (지도 구역에는 이름 · 색 · 언어만 저장된다).</summary>
    internal static void ReattachMissionFactions(GameMap map, MissionDef def)
    {
        var have = FactionsOf(map);
        var list = def.Factions.Select((f, i) => new FactionDef
        {
            Name = i < have.Count ? have[i].Name : f.Name, En = f.En, Color = i < have.Count ? have[i].Color : f.Color,
            Lang = i < have.Count ? have[i].Lang : f.Lang, Team = f.Team,
        }).ToList();
        factionTable.AddOrUpdate(map, list);
    }
}

/// <summary>[치트 · 테스트] 미션 즉시 승리 / 패배 (콘솔 "mission win|lose", UI 퍼즈).</summary>
public sealed record MissionEndCommand(int Faction, bool Won) : GameCommand(Faction);
