using System.Numerics;
using System.Text.Json.Nodes;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision.Tests;

/// <summary>기본 4인 맵(corners4) · 지도 정의 세력 · 세력 선택(--faction) · 리플레이의 사람 세력 · 부대 지정 로직 검증 (창 없이).</summary>
public static class FactionsSelfTest
{
    static string ScratchDir => TestTemp.Dir;

    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        try
        {
            CornersMap(opt, check);
            FactionArgs(check);
            ChosenFactionAndAi(opt, check);
            ReplayRecordsHuman(opt, check);
            BanditCommonRifles(opt, check);
        }
        catch (Exception e)
        {
            check(false, $"세력 · 부대 테스트 중 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>[N54] 시험 고정: 소총 칸 보병 전원이 모신나강 (세력 분배 · 소총 수치 차이를 없앤다). 시드에 민감하게 맞춘 AI 시나리오 시험이 분배 도입 뒤에도 같은 조건으로 돌게 한다.</summary>
    internal static void PinRifles(World w)
    {
        var mos = w.Defs.Weapon(Defs.RifleSlot);
        foreach (var u in w.Units) if (u.Def.Weapon == Defs.RifleSlot) u.Weapon = mos;
    }

    // ---------------------------------------------------------------- 마적 · 공용 유닛 · 소총 변형 (N26 · N53 · N54, 2026-10-19)

    static void BanditCommonRifles(GameOptions opt, Action<bool, string> check)
    {
        var d = Defs.Current;
        string[] ens = { "korean", "japanese", "fengtian", "bandit", "russian", "soviet" };
        string[] bandit = { "bandit_thug", "bandit_rifleman", "bandit_horseman", "bandit_leader" };
        check(bandit.All(k => d.Units.ContainsKey(k) && ens.Where(e => d.Units[k].AllowedFor(e)).SequenceEqual(new[] { "bandit" })), "마적 유닛 4종 = 마적 전용 (factions)");
        check(!d.Units["infantry"].AllowedFor("bandit") || d.Units["infantry"].Factions == null, "일반 보병은 제한 없음");   // 제한 없는 유닛 = 마적도 편성 가능 (회귀 확인)
        check(d.Units["infantry_assault"].AllowedFor("korean") && !d.Units["infantry_assault"].AllowedFor("bandit") && !d.Units["infantry_assault"].AllowedFor("japanese"), "koreaOnly 그대로 동작 (돌격보병)");
        var thug = d.Units["bandit_thug"];
        check(thug.Melee == "dadao" && thug.Weapon == "dadao" && d.Weapon(thug.Weapon)!.Range < 1f && thug.Sidearm == null && thug.MeleeOnly, "마적 잡병: 대도만 (총 없음, 근접 전용)");
        var inf = d.Units["infantry"]; var rf = d.Units["bandit_rifleman"];
        check(rf.Hp == inf.Hp && rf.Speed == inf.Speed && rf.Sight == inf.Sight && rf.SightNear == inf.SightNear && rf.Accuracy == inf.Accuracy && rf.Weapon == inf.Weapon && rf.SuppressResist == inf.SuppressResist && rf.MarchFire == inf.MarchFire && rf.Category == inf.Category && rf.Radius == inf.Radius, "마적 소총병 = 정규군 보병 성능");
        check(thug.Hp == inf.Hp && thug.Speed == inf.Speed, "마적 잡병 체력 · 속도 = 정규군 보병");
        var cav = d.Units["cavalry_default"]; var hm = d.Units["bandit_horseman"];
        check(hm.Hp == cav.Hp && hm.Speed == cav.Speed && hm.Weapon == cav.Weapon && hm.Melee == cav.Melee && hm.Mounted && hm.ChargeSpeedMul == cav.ChargeSpeedMul && hm.StaminaMul == cav.StaminaMul && hm.Sight == cav.Sight, "마적 기병 = 총기병 성능");
        var off = d.Units["officer"]; var ld = d.Units["bandit_leader"];
        check(ld.Hp == off.Hp && ld.Speed == off.Speed && ld.Weapon == off.Weapon && ld.Melee == off.Melee && ld.CommandRadius == off.CommandRadius && ld.SuppressResist == off.SuppressResist && ld.Spotter == off.Spotter && ld.IsOfficer, "마적 지휘관 = 장교 성능");
        check(bandit.All(k => d.Units[k].Name.Length > 0 && d.Units[k].Description.Length > 0 && d.Units[k].ArtAs != null && d.Units.ContainsKey(d.Units[k].ArtAs!)), "마적 이름 · 설명 · 그림 대용(artAs)");
        check(RankNames.Snap("bandit", 1) is >= 0 && RankNames.Snap("nowhere", 3) >= 0, "계급표: 마적 있음 · 모르는 세력은 default 로");

        // 공용 유닛
        var cu = d.Units.Values.Where(u => u.Common && u.Key == "common_volunteer_tmp").ToList();   // N63 이후 공용 유닛이 더 있음 — 자리표시자만 검사
        check(cu.Count == 1 && cu[0].Key == "common_volunteer_tmp" && cu[0].Name.Contains("임시") && ens.All(e => cu[0].AllowedFor(e)) && cu[0].AllowedFor(null), "공용 유닛(자리표시자) = 모든 세력 편성 가능");
        var c = cu[0];
        check(c.Hp == inf.Hp && c.Speed == inf.Speed && c.Accuracy == inf.Accuracy && c.Weapon == inf.Weapon, "공용 자리표시자 = 정규군 보병 수치");

        // 미션 검증
        string txt = File.ReadAllText(MissionDef.PathOf("bandit_raid"));
        var md = MissionDef.Parse(txt, "bandit_raid");
        check(md.Factions[1].En == "bandit" && md.Forces.Count == 2, "마적 시험 미션 로드 (bandit_raid)");
        bool Throws(string t) { try { MissionDef.Parse(t, "bad"); return false; } catch (InvalidDataException) { return true; } }
        check(Throws(txt.Replace("\"infantry\": 28", "\"bandit_rifleman\": 28")), "한국군 편성에 마적 유닛 → 로드 오류");
        check(Throws(txt.Replace("\"bandit_thug\": 14", "\"infantry_assault\": 14")), "마적 편성에 돌격보병(한국 전용) → 로드 오류");
        check(!Throws(txt.Replace("\"infantry\": 28", "\"common_volunteer_tmp\": 28").Replace("\"bandit_thug\": 14", "\"common_volunteer_tmp\": 14")), "공용 유닛은 한국군 · 마적 어느 편성에도 허용");

        // 소총 변형
        var rifle = d.Weapons["rifle"]; var mos = d.Weapons["rifle_mosin"]; var ari = d.Weapons["rifle_arisaka38"];
        bool Same(WeaponDef a, WeaponDef b) => a.Damage == b.Damage && a.Range == b.Range && a.Cooldown == b.Cooldown && a.CooldownMin == b.CooldownMin && a.CooldownMax == b.CooldownMax && a.Accuracy == b.Accuracy && a.AccNear == b.AccNear && a.AccFar == b.AccFar && a.Ammo == b.Ammo && a.Suppress == b.Suppress;
        check(Same(rifle, mos) && mos.Name != rifle.Name, "모신나강 = rifle 수치 (별칭 rifle 유지)");
        check(Same(rifle, mos) && d.Weapons["rifle_arisaka38"].Damage == 35 && d.Weapons["rifle_arisaka38"].Range == 23 && d.Weapons["rifle_berdan2"].Cooldown == 3.0f && d.Weapons["rifle_murata"].Range == 20 && d.Weapons["rifle_hanyang88"].Damage == 45 && d.Weapons["rifle_lebel86"].Range == 22 && d.Weapons["rifle_mauser98"].AccNear == 0.40f
              && d.Weapons["rifle_murata"].AmmoType != d.Weapons["rifle_arisaka38"].AmmoType && d.Weapons["rifle_hanyang88"].AmmoType == d.Weapons["rifle_mauser98"].AmmoType, "소총 7종 수치 · 탄약 종류 (구식 일본탄은 일식과 별개)");
        // 분배표: 1000명 표본 ±3%, 결정적
        int N = 1000;
        double Share(string en, string wk) => Enumerable.Range(1, N).Count(i => d.WeaponKeyFor(inf, en, i, 1920) == wk) / (double)N;
        bool near(double a, double b) => Math.Abs(a - b) <= 0.03;
        check(Share("korean", "rifle_mosin") == 1 && Share("russian-red", "rifle_mosin") == 1 && Share("russian-white", "rifle_mosin") == 1 && Share("japanese", "rifle_arisaka38") == 1, "분배: 한국 · 러시아 = 모신 100%, 일본 = 38식 100%");
        check(near(Share("fengtian", "rifle_arisaka38"), .6) && near(Share("fengtian", "rifle_hanyang88"), .3) && near(Share("fengtian", "rifle_lebel86"), .1), $"분배: 봉천 38식 60 · 한양 30 · 르벨 10 (±3%) [{Share("fengtian", "rifle_arisaka38"):0.000} {Share("fengtian", "rifle_hanyang88"):0.000} {Share("fengtian", "rifle_lebel86"):0.000}]");
        check(near(Share("bandit", "rifle_murata"), .4) && near(Share("bandit", "rifle_mosin"), .3) && near(Share("bandit", "rifle_arisaka38"), .2) && near(Share("bandit", "rifle_hanyang88"), .1), "분배: 마적 무라타 40 · 모신 30 · 38식 20 · 한양 10 (±3%)");
        check(Enumerable.Range(1, 200).All(i => d.WeaponKeyFor(inf, "fengtian", i, 7) == d.WeaponKeyFor(inf, "fengtian", i, 7)) && Enumerable.Range(1, 200).Any(i => d.WeaponKeyFor(inf, "fengtian", i, 7) != d.WeaponKeyFor(inf, "fengtian", i, 8)), "분배는 결정적 (같은 id · 시드 = 같은 소총), 시드가 다르면 달라짐");
        check(Enumerable.Range(1, 200).All(i => d.WeaponKeyFor(c, "fengtian", i, 1) == "rifle_mosin") && Enumerable.Range(1, 200).All(i => d.WeaponKeyFor(d.Units["officer"], "japanese", i, 1) == "pistol"), "공용 유닛 = 모신 (분배 없음), 권총 등 다른 무기는 그대로");
        var old = d.Units["infantry_default_old"];
        check(old.Weapon == "rifle_berdan2" && old.Hp == inf.Hp && old.Speed == inf.Speed && old.Sight == inf.Sight && old.Accuracy == inf.Accuracy && old.SuppressResist == inf.SuppressResist && old.Melee == inf.Melee && old.AllowedFor("korean") && !old.AllowedFor("japanese") && !old.AllowedFor("bandit") && old.ArtAs == "infantry_reserve"
              && Enumerable.Range(1, 50).All(i => d.WeaponKeyFor(old, "korean", i, 1) == "rifle_berdan2"), "구식 보병: 정규군 보병 수치 · 베르단 II · 한국 전용 · 한국 보병 그림");
        var map = MapFile.Load("corners4", opt.Seed);
        var w = World.CreateStandard(map, d);
        MapFile.ApplyFactions(w);
        var got = Enumerable.Range(1, 4).SelectMany(f => Enumerable.Range(0, 60).Select(k => w.SpawnUnit(inf, f, new Vector2(8 + k % 20, 8 + f * 4 + k / 20)).Weapon!.Key)).ToArray();
        check(got.Take(60).All(k => k == "rifle_mosin") && got.Skip(60).Take(60).All(k => k == "rifle_arisaka38") && got.Skip(120).Take(60).Distinct().Count() == 3 && got.Skip(180).All(k => k == "rifle_mosin"), "생성 보병의 소총: 한국 모신 · 일본 38식 · 봉천 3종 혼합 · 백군 모신");
        string txt0 = File.ReadAllText(MissionDef.PathOf("bandit_raid"));
        var ow = MissionDef.Parse(txt0.Replace("\"bandit_rifleman\": 14", "\"bandit_rifleman\": 14, \"bandit_thug\": 0").Replace("\"companies\": [ { \"units\": { \"bandit_leader\"", "\"companies\": [ { \"weapon\": \"rifle_mauser98\", \"units\": { \"bandit_leader\""), "ow");
        var ow2 = World.CreateMissionWorld(ow, d, 1, new List<DeployEntry>());
        var mr = ow2.Units.Where(u => u.Def.Key == "bandit_rifleman").ToList();
        var base2 = World.CreateMissionWorld(MissionDef.Load("bandit_raid"), d, 1, new List<DeployEntry>());
        check(mr.Count == 14 && mr.All(u => u.Weapon!.Key == "rifle_mauser98") && base2.Units.Where(u => u.Def.Key == "bandit_rifleman").Select(u => u.Weapon!.Key).Distinct().Count() > 1, "미션 중대 weapon 지정이 분배를 덮어씀 (지정 없으면 분배)");
        var cu1 = w.SpawnUnit(c, 2, new Vector2(30, 30));
        check(cu1.Weapon!.Key == "rifle_mosin" && cu1.Owner == 2, "공용 유닛은 세력과 무관하게 모신나강");

        // 한국군 보병 단계 (지방대 · 진위대 · 친위대 · 시위대 · 돌격대) · 봉천 대도병
        var U = d.Units;
        check(U["infantry_default_old"].Name == "지방대" && U["infantry"].Name == "진위대" && U["infantry_regular"].Name == "친위대" && U["infantry_guard"].Name == "시위대" && U["infantry_assault"].Name == "돌격대", "한국군 단계 이름: 지방대 · 진위대 · 친위대 · 시위대 · 돌격대");
        check(U["infantry_default_old"].Weapon == "rifle_berdan2" && U["infantry"].Weapon == "rifle_mosin" && U["infantry_regular"].Weapon == "rifle_mosin" && U["infantry_guard"].Weapon == "rifle_mauser98" && U["infantry_assault"].Weapon == "carbine_assault", "단계별 소총: 베르단 · 모신 · 모신 · 마우저 · 카빈(그대로)");
        check(U["infantry_regular"].ShellDamageMul == 0.85f && U.Values.Where(u => u.ShellDamageMul != 1f).Select(u => u.Key).SequenceEqual(new[] { "infantry_regular" }), "철모 = 친위대만 포격 피해 ×0.85");
        {   // 포격 · 수류탄 피해만 줄고 소총 피해는 그대로
            var wh = World.CreateStandard(MapFile.Load("corners4", opt.Seed), d); MapFile.ApplyFactions(wh);
            foreach (var o in wh.Units.ToList()) { o.Weapon = null; o.Hp = 1e6f; }
            var pa = new Vector2(60, 60);
            var helm = wh.SpawnUnit(U["infantry_regular"], 1, pa); var plain = wh.SpawnUnit(U["infantry"], 1, pa + new Vector2(0.01f, 0)); helm.Hp = plain.Hp = 100;
            var how = d.Weapons["howitzer_heavy"];
            typeof(World).GetMethod("Explode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(wh, new object?[] { 0, 2, null, pa + new Vector2(20, 0), pa, 0, how });
            float dh = 100 - helm.Hp, dp = 100 - plain.Hp;
            check(dp > 0 && MathF.Abs(dh - dp * 0.85f) < 0.05f, $"철모: 포격 피해 {dh:0.0} = 일반 {dp:0.0} × 0.85");
            helm.Hp = plain.Hp = 100; var bullet = d.Weapons["rifle_mosin"];
            wh.Damage(helm, bullet.Damage, null, "rifle_mosin"); wh.Damage(plain, bullet.Damage, null, "rifle_mosin");
            check(helm.Hp == plain.Hp, "철모는 소총 피해에는 영향 없음");
        }
        var fd0 = U["infantry_dadao_fengtian"];
        check(fd0.Melee == "dadao" && fd0.Weapon == "dadao" && d.Weapon(fd0.Weapon)!.Range < 1f && fd0.Sidearm == null && fd0.MeleeOnly && fd0.Hp == U["infantry_fengtian"].Hp && fd0.Speed == U["infantry_fengtian"].Speed && fd0.Name == "봉천 대도병", "봉천 대도병: 대도만 · 사격 불가, 체력 · 속도 = 봉천 보병");
        check(ens.Where(e => fd0.AllowedFor(e)).SequenceEqual(new[] { "fengtian" }) && fd0.ArtAs == "infantry_fengtian", "봉천 대도병 = 봉천 전용 · 그림 대용 봉천 보병");
        {
            var wd = World.CreateStandard(MapFile.Load("corners4", opt.Seed), d); MapFile.ApplyFactions(wd);
            var dd = wd.SpawnUnit(fd0, 3, new Vector2(30, 30));
            check(dd.ChargeOnly && dd.Weapon!.Key == "dadao", "봉천 대도병은 생성 때 돌격만 켜짐 · 무기 대도");
        }
        // 마적 미션 시뮬: 잡병은 근접만, 소총병은 사격
        var mdef = MissionDef.Load("bandit_raid");
        var mw = World.CreateMissionWorld(mdef, d, 1, new List<DeployEntry>());
        World.AttachMissionAi(mw, mdef.Player, true);
        var thugs = mw.Units.Where(u => u.Def.Key == "bandit_thug").ToList();
        check(thugs.Count == 14 && thugs.All(u => u.ChargeOnly && u.Weapon!.Key == "dadao"), "미션의 마적 잡병 14기: 돌격만 켜짐 · 무기 대도");
        var last = thugs.ToDictionary(u => u.Id, u => u.LastShotTick);
        int far = 0, swings = 0; var thugIds = thugs.Select(u => u.Id).ToHashSet();
        var seen = new HashSet<(long, int)>();
        for (long t = 0; t < 240 / Config.SimDt && mw.Winner == 0; t++)
        {
            mw.Step();
            foreach (var ms in mw.MeleeStrikes) if (thugIds.Contains(ms.AttackerId) && seen.Add((ms.Tick, ms.AttackerId))) swings++;
            foreach (var u in thugs)
            {
                if (u.LastShotTick == last[u.Id]) continue;
                last[u.Id] = u.LastShotTick;
                var tg = u.Target;   // 휘두름은 근접 거리에서만 (총 사격이면 멀리서도 나감)
                if (tg != null && tg.EdgeDistance(u.Pos) - u.Radius > 2f) far++;
            }
        }
        check(far == 0, $"미션 시뮬 240초: 잡병 원거리 사격 {far}회 (근접 {swings}회)");
        // 근접 확인: 잡병 1기를 비무장 표적 옆에 두면 대도로 때린다 (돌격 사격 없이)
        var w2 = World.CreateStandard(MapFile.Load("corners4", opt.Seed), d); MapFile.ApplyFactions(w2);
        foreach (var o in w2.Units.Where(x => x.Owner != 2).ToList()) { o.Weapon = null; o.Hp = 1e6f; }   // 다른 병사는 싸우지 않게
        var dummy = w2.Units.First(x => x.Owner == 1);
        var th = w2.SpawnUnit(thug, 2, dummy.Pos + new Vector2(2.5f, 0)); th.Hp = 1e6f;
        int hits = 0; long lst = th.LastShotTick;
        for (int t = 0; t < 600; t++) { w2.Step(); if (th.LastShotTick != lst) { lst = th.LastShotTick; hits++; } }
        check(hits > 0 && dummy.Hp < 1e6f && th.ChargeOnly, $"잡병은 가까운 적을 대도로 벤다 (휘두름 {hits}회, 피해 {1e6f - dummy.Hp:0})");
    }

    static void CornersMap(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var map = MapFile.Load("corners4", opt.Seed);
        var fd = MapFile.FactionsOf(map);
        check(map.Players == 4 && map.Width == 192 && map.Height == 192, "corners4: 4인 · 192²");
        string[] ko = { S.FactionKorean, S.FactionJapanese, S.FactionFengtian, S.FactionRussianWhite };
        string[] en = { "korean", "japanese", "fengtian", "russian" };
        check(fd.Count == 4 && Enumerable.Range(0, 4).All(i => fd[i].Name == ko[i] && fd[i].En == en[i]), $"corners4: 세력 4개 이름 {string.Join("/", fd.Select(f => f.Name))}");

        var w = World.CreateStandard(map, defs);
        MapFile.ApplyFactions(w);
        check(Enumerable.Range(1, 4).All(f => w.Faction(f).Name == ko[f - 1]), "세력 이름이 지도에서 옴 (한국 · 일본 · 봉천 군벌 · 러시아 백군)");
        // 모서리: 1 = 좌상, 2 = 우상, 3 = 좌하, 4 = 우하
        var hq = Enumerable.Range(1, 4).Select(f => w.Buildings.First(b => b.Owner == f && b.Def.Headquarters).Pos).ToArray();
        bool corners = hq[0].X < 32 && hq[0].Y < 32 && hq[1].X > 96 && hq[1].Y < 32 && hq[2].X < 32 && hq[2].Y > 96 && hq[3].X > 96 && hq[3].Y > 96;
        check(corners, "corners4: 한국 좌상 · 일본 우상 · 봉천 좌하 · 러시아 백군 우하 모서리");

        // 색: 서로 뚜렷이 달라야 함
        var cols = fd.Select(f => f.Color!).ToArray();
        int minDist = int.MaxValue;
        for (int i = 0; i < 4; i++)
        for (int j = i + 1; j < 4; j++)
            minDist = Math.Min(minDist, Math.Abs(cols[i][0] - cols[j][0]) + Math.Abs(cols[i][1] - cols[j][1]) + Math.Abs(cols[i][2] - cols[j][2]));
        check(minDist >= 150, $"세력 색 최소 거리 {minDist}");

        // 대칭: 지형 · 건물 (90도 회전)
        int n = map.Width;
        bool terrainSym = true;
        for (int y = 0; y < n && terrainSym; y++)
        for (int x = 0; x < n; x++)
            if (map.Get(x, y) != map.Get(y, n - 1 - x)) { terrainSym = false; break; }
        bool siteSym = w.Buildings.All(b => w.Buildings.Any(o => o.Def == b.Def && Vector2.Distance(o.Pos, new Vector2(n - b.Pos.Y, b.Pos.X)) < 0.01f));
        check(terrainSym && siteSym, $"corners4: 4중 회전 대칭 (지형 {terrainSym}, 시설 {siteSym})");
        int Of(string k) => w.Buildings.Count(b => b.Def.Key == k);
        check(Of("metropolis") == 4 && Of("city") == 4 && Of("village") == 24, $"corners4: 시설 {w.Buildings.Count(b => b.Def.Capturable)}개 (대도시 4 · 소도시 4 · 마을 24)");

        // 시작 지점에서 도달 가능 + 모든 시설 주변(점령 반경 4)이 통행 가능한 땅
        var seen = new bool[n * n];
        var q = new Queue<(int, int)>();
        var (sx, sy) = map.StartTiles[0];
        seen[sy * n + sx] = true; q.Enqueue((sx, sy));
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = x + dx, b = y + dy;
                if (map.IsTerrainWalkable(a, b) && !seen[b * n + a]) { seen[b * n + a] = true; q.Enqueue((a, b)); }
            }
        }
        int unreachable = 0, unclear = 0;
        foreach (var b in w.Buildings.Where(b => b.Def.Capturable))
        {
            int sz = b.Def.Size, tx = (int)MathF.Floor(b.Pos.X - sz / 2f + 0.001f), ty = (int)MathF.Floor(b.Pos.Y - sz / 2f + 0.001f);
            bool clear = true;
            for (int y = Math.Max(0, ty - 4); y < Math.Min(n, ty + sz + 4); y++)
            for (int x = Math.Max(0, tx - 4); x < Math.Min(n, tx + sz + 4); x++)
                if (!map.IsTerrainWalkable(x, y)) clear = false;
            if (!clear) unclear++;
            if (!seen[ty * n + tx]) unreachable++;
        }
        check(unreachable == 0 && unclear == 0, $"corners4: 모든 시설이 1번 세력에서 도달 가능 (도달 불가 {unreachable}) · 주변 4칸 평탄 (막힘 {unclear})");
        int rock = 0, water = 0;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var t = map.Get(x, y);
            if (t == TerrainType.Rock) rock++; else if (t == TerrainType.Water) water++;
        }
        check(rock > 200 && water > 200, $"corners4: 지형 있음 (바위 {rock}, 물 {water})");
    }

    static void FactionArgs(Action<bool, string> check)
    {
        var defs = MapFile.Peek("corners4")!.Factions;
        check(FactionChoice.Parse("2", defs, 4) == 2 && FactionChoice.Parse("japanese", defs, 4) == 2 && FactionChoice.Parse("RUSSIAN", defs, 4) == 4
            && FactionChoice.Parse("korean", defs, 4) == 1 && FactionChoice.Parse(S.FactionFengtian, defs, 4) == 3, "--faction: 번호 · id(대소문자 무시) · 한글 이름");
        bool bad = true;
        foreach (var a in new[] { "0", "5", "-1", "nobody" })
            try { FactionChoice.Parse(a, defs, 4); bad = false; } catch (ArgumentException) { }
        try { FactionChoice.Parse("japanese", Array.Empty<FactionDef>(), 4); bad = false; } catch (ArgumentException) { }
        check(bad, "--faction: 범위 밖 · 모르는 이름 · 이름 없는 맵에서 이름 사용은 오류");
        var o = new GameOptions { MapId = "corners4" };
        FactionChoice.Resolve(o, "fengtian");
        var g = new GameOptions { Players = 6 };
        FactionChoice.Resolve(g, "6");
        check(o.Faction == 3 && g.Faction == 6, "--faction: 옵션에 반영 (고정 지도 · 생성 맵)");
    }

    static void ChosenFactionAndAi(GameOptions opt, Action<bool, string> check)
    {
        foreach (int human in new[] { 1, 3, 4 })
        {
            var w = World.CreateStandard(MapFile.Load("corners4", opt.Seed), Defs.Current);
            Game.AttachControllers(w, human, false, "standard");
            var aiIds = w.Controllers.Select(c => c.Item1).OrderBy(i => i).ToList();
            var expect = Enumerable.Range(1, 4).Where(f => f != human).ToList();
            check(aiIds.SequenceEqual(expect) && !w.Faction(human).IsAi && expect.All(f => w.Faction(f).IsAi), $"세력 {human} 선택: 나머지 {string.Join(",", expect)} 만 AI");
        }
        var s = World.CreateStandard(MapFile.Load("corners4", opt.Seed), Defs.Current);
        Game.AttachControllers(s, 0, true, "standard");
        check(s.Controllers.Count == 4, "관전: 4세력 모두 AI");
    }

    static void ReplayRecordsHuman(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var w = World.CreateStandard(MapFile.Load("corners4", opt.Seed), defs);
        MapFile.ApplyFactions(w);
        Game.AttachControllers(w, 3, false, "standard");
        for (int i = 0; i < 60 * 90; i++) w.Step();
        var rep = ReplayFile.FromWorld(w, 3);
        string path = Path.Combine(ScratchDir, "factions_test.kdr.json");
        rep.Save(path);
        var loaded = ReplayFile.Load(path);
        long bad = loaded.Verify(defs, out var report);
        check(loaded.HumanFaction == 3 && loaded.MapId == "corners4" && bad < 0, $"리플레이: 사람 세력 3 기록 · 재생 검증 ({report})");
        var rw = loaded.CreateWorld(defs);
        check(rw.Faction(1).Name == S.FactionKorean && rw.Faction(3).Name == S.FactionFengtian, "리플레이 재생: 세력 이름이 지도에서 복원됨");
        // 옛 파일(HumanFaction 없음)
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node.Remove("HumanFaction"); node.Remove("humanFaction");
        string oldPath = Path.Combine(ScratchDir, "factions_test_old.kdr.json");
        File.WriteAllText(oldPath, node.ToJsonString());
        var old = ReplayFile.Load(oldPath);
        check(old.HumanFaction == 0 && old.Verify(defs, out _) < 0, "옛 리플레이(사람 세력 없음)도 불러와 검증됨");
        File.Delete(path); File.Delete(oldPath);
    }

}
