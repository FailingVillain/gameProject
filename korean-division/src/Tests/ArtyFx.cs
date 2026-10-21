using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using KoreanDivision.Core;
namespace KoreanDivision.Tests;

/// <summary>
/// 포병 효과 측정기 (읽기 전용, 환경변수 KD_ARTYFX=1 일 때만 MeasX / TacticsObs 가 부른다; 시뮬 상태 불변).
/// 포 종류(세력:유닛키)별로: 포탄 수, 거리 분포, 착탄 명중(반경 안 적 1기 이상), 반경 안 적 수, 제압 건수(약식), 살상 크레딧(Unit.Kills — 엔진이
/// 막타 사격자에게 주는 값; 포가 죽기 전 값까지 보존), 세력별 종류별 시작/끝 수. 출력 한 줄 "ARTYFX {json}".
/// </summary>
public static class ArtyFx
{
    sealed class G
    {
        public int Units, Shells, Hit, EnemyInSplash, SuppEvents; public double SuppGain, DistSum, FirstShell = -1;
        public int[] Bucket = new int[5];   // 0-20, 20-40, 40-60, 60-80, 80+
        public int KillCredit; public int Lost;
        public int Far, FarAvoidable;   // N20: 사거리 80% 넘는 착탄 · 그중 쏠 때 유효 사거리 (80%) 안에 보이고 사선이 트인 적이 있었던 것
    }
    public static bool On => Environment.GetEnvironmentVariable("KD_ARTYFX") == "1";
    static readonly Dictionary<string, G> gs = new();
    static readonly Dictionary<int, (int owner, string key)> who = new();
    static readonly Dictionary<int, int> kills = new();   // 포 Id -> 마지막 Kills
    static readonly Dictionary<string, int> startKey = new();
    static int startA, startB;
    // N20: 관측자 (정찰조가 아닌 정찰병 = 관측반 · AI 가 부리는 관측자) 생존, 접촉 전 포탄, 포 이동 (1초 샘플)
    static readonly Dictionary<int, int> obsOwner = new();
    static readonly Dictionary<int, double> obsDeath = new();
    static readonly Dictionary<int, double> contactT = new();
    static readonly Dictionary<int, int> preShells = new(), prepShells = new();
    static readonly Dictionary<int, double> advanceT = new();   // 첫 보병 전진 (틀 약진 · 기동조 · 돌격) 시각
    static readonly Dictionary<int, (Vector2 p, bool moving)> gunPrev = new();
    static readonly Dictionary<int, int> gunSec = new(), gunMoveSec = new(), gunMoves = new();
    static readonly Dictionary<int, (Vector2 p, string near)> obsLast = new();
    static readonly bool ObsLog = Environment.GetEnvironmentVariable("KD_ARTYFX_OBSLOG") == "1";
    /// <summary>KD_ARTYFX_GUNLOG=<유닛키> — 5초마다 그 포의 위치 · 명령 · 방열 · 표적 · 가장 가까운 보이는 적 · 가장 가까운 아군 소총병 (원인 추적용).</summary>
    static readonly string? GunLog = Environment.GetEnvironmentVariable("KD_ARTYFX_GUNLOG");
    // N22: 소속 중대 정찰조 생존, 관측 가동률 (접촉 뒤 초 중 관측자 (spotter) 가 적을 보는 초 · 정찰병이 직접 적을 보는 초), 엄폐 상태 진입 수
    static readonly Dictionary<int, int> scoutOwner = new();
    static readonly Dictionary<int, int> upSec = new(), spotSec = new(), scoutSeeSec = new(), scoutSeeSum = new(), coverIn = new();
    static readonly HashSet<int> inCover = new();

    static bool IsGun(Unit u) => u.Def.Category == "gun" || u.Def.Key.StartsWith("heavy_") && (u.Def.Key.EndsWith("_gun") || u.Def.Key.EndsWith("howitzer"));

    public static void Init(World w)
    {
        gs.Clear(); who.Clear(); kills.Clear(); startKey.Clear();
        scoutOwner.Clear(); upSec.Clear(); spotSec.Clear(); scoutSeeSec.Clear(); scoutSeeSum.Clear(); coverIn.Clear(); inCover.Clear();
        ApplyOverrides(w);
        obsOwner.Clear(); obsDeath.Clear(); obsLast.Clear(); contactT.Clear(); preShells.Clear(); prepShells.Clear(); advanceT.Clear(); gunPrev.Clear(); gunSec.Clear(); gunMoveSec.Clear(); gunMoves.Clear();
        startA = w.Units.Count(u => u.Owner == 1); startB = w.Units.Count(u => u.Owner == 2);
        foreach (var u in w.Units)
        {
            who[u.Id] = (u.Owner, u.Def.Key);
            string k = u.Owner + ":" + u.Def.Key; startKey[k] = startKey.GetValueOrDefault(k) + 1;
            if (IsGun(u)) { Get(k).Units++; kills[u.Id] = 0; }
        }
    }
    static G Get(string k) { if (!gs.TryGetValue(k, out var g)) gs[k] = g = new G(); return g; }

    static int per5 => Math.Max(1, (int)MathF.Round(1f / Config.SimDt));
    static void Sample(World w)
    {
        foreach (var u in w.Units)
        {
            if (!u.Alive) continue;
            if (u.Def.Scout && u.ScoutCompany == null && (u.Company != null || !u.ScoutAuto) && !obsOwner.ContainsKey(u.Id)) obsOwner[u.Id] = u.Owner;
            if (u.Def.Scout && u.ScoutCompany != null && !scoutOwner.ContainsKey(u.Id) && !obsOwner.ContainsKey(u.Id)) scoutOwner[u.Id] = u.Owner;
            if (u.Def.Scout) { bool cv = (int)u.ScoutState == 4; if (cv && inCover.Add(u.Id)) coverIn[u.Owner] = coverIn.GetValueOrDefault(u.Owner) + 1; else if (!cv) inCover.Remove(u.Id); }
            if (IsGun(u))
            {
                var pv = gunPrev.TryGetValue(u.Id, out var q) ? q : (u.Pos, false);
                bool mv = Vector2.Distance(pv.Item1, u.Pos) > 0.4f;
                gunSec[u.Owner] = gunSec.GetValueOrDefault(u.Owner) + 1;
                if (mv) { gunMoveSec[u.Owner] = gunMoveSec.GetValueOrDefault(u.Owner) + 1; if (!pv.Item2) gunMoves[u.Owner] = gunMoves.GetValueOrDefault(u.Owner) + 1; }
                gunPrev[u.Id] = (u.Pos, mv);
            }
        }
        if (GunLog != null && w.Tick % (per5 * 5) == 0)
            foreach (var u in w.Units)
            {
                if (!u.Alive || u.Def.Key != GunLog) continue;
                float de = 1e9f, df = 1e9f; Unit? ne = null; int inRange = 0, inArcLos = 0;
                foreach (var e in w.Units)
                {
                    if (!e.Alive) continue;
                    float d = Vector2.Distance(e.Pos, u.Pos);
                    if (e.Owner != u.Owner && w.Fog.CanSee(u.Owner, e))
                    {
                        if (d < de) { de = d; ne = e; }
                        if (u.Weapon != null && d <= u.Weapon.Range) { inRange++; if (World.CanTraverseTo(u, e.Pos) && w.Map.HasFiringLine(u.Pos, e.Pos, u.Id, e.Id)) inArcLos++; }
                    }
                    else if (e.Owner == u.Owner && e.Def.IsInfantry && !e.Def.Heavy && !e.IsOfficer && !e.Def.Scout) df = MathF.Min(df, d);
                }
                Console.WriteLine($"GUNLOG t={w.Time:0} f={u.Owner} id={u.Id} at=({u.Pos.X:0},{u.Pos.Y:0}) ord={u.Order} dep={u.Deployed} tgt={(u.Target is Unit tu ? Vector2.Distance(tu.Pos, u.Pos).ToString("0") : "-")} nearVisEnemy={de:0} nearOwnRifle={df:0} lastShot={(u.LastShotTick > 0 ? (u.LastShotTick * Config.SimDt).ToString("0") : "-")} shift={(ne != null ? World.TraverseShiftTo(u, ne.Pos) * 57.3f : -1):0} los={(ne != null && w.Map.HasFiringLine(u.Pos, ne.Pos, u.Id, ne.Id))} inRange={inRange} arcLos={inArcLos}");
            }
        foreach (var (id, f0) in obsOwner)
        {
            if (obsDeath.ContainsKey(id)) continue;
            if (w.Find(id) is not Unit o || !o.Alive)
            {
                obsDeath[id] = w.Time;
                if (ObsLog && obsLast.TryGetValue(id, out var ol)) Console.WriteLine($"OBSDEATH t={w.Time:0} f={f0} at=({ol.p.X:0},{ol.p.Y:0}) {ol.near}");
                continue;
            }
            if (ObsLog)
            {
                float bd = 1e9f; string bk = "-"; float bd2 = 1e9f; string bk2 = "-";
                foreach (var e in w.Units)
                {
                    if (!e.Alive || e.Owner == o.Owner) continue;
                    float d = Vector2.Distance(e.Pos, o.Pos);
                    if (d < bd) { bd = d; bk = e.Def.Key + (w.Fog.CanSee(o.Owner, e) ? "/seen" : "/hidden"); }
                    if (e.Target == o && d < bd2) { bd2 = d; bk2 = e.Def.Key; }
                }
                float front = float.MinValue; foreach (var u in w.Units) if (u.Owner == o.Owner && u.Alive && u.Def.IsInfantry && !u.Def.Scout && !u.IsOfficer && !u.Def.Heavy) front = MathF.Max(front, o.Owner == 1 ? -u.Pos.Y : u.Pos.Y);
                float ahead = (o.Owner == 1 ? -o.Pos.Y : o.Pos.Y) - front;
                obsLast[id] = (o.Pos, $"nearest {bd:0} {bk}, targeted-by {bk2} {(bd2 < 1e8f ? bd2.ToString("0") : "-")}, ahead-of-front {ahead:0}, supp {o.SuppressionRatio:0.00}, order {o.Order}");
            }
        }
        for (int f = 1; f <= 2; f++)
        {
            if (!contactT.ContainsKey(f)) continue;
            upSec[f] = upSec.GetValueOrDefault(f) + 1;
            bool spot = false; int see = 0;
            foreach (var e in w.Units) if (e.Alive && e.Owner != f && w.IsHostile(f, e.Owner) && w.SeenBySpotter(f, e)) { spot = true; break; }
            foreach (var u in w.Units)
            {
                if (!u.Alive || u.Owner != f || !u.Def.Scout) continue;
                int sg = w.SightOf(u);
                foreach (var e in w.Units)
                    if (e.Alive && e.Owner != f && w.IsHostile(f, e.Owner) && Vector2.DistanceSquared(e.Pos, u.Pos) <= sg * sg && w.Fog.CanSee(f, e)) { see++; break; }
            }
            if (spot) spotSec[f] = spotSec.GetValueOrDefault(f) + 1;
            if (see > 0) scoutSeeSec[f] = scoutSeeSec.GetValueOrDefault(f) + 1;
            scoutSeeSum[f] = scoutSeeSum.GetValueOrDefault(f) + see;
        }
        foreach (var c in w.Companies)
            if (!advanceT.ContainsKey(c.Owner) && (c.Drill.BoundSquad > 0 || c.Drill.Man is 1 or 2 or 4)) advanceT[c.Owner] = w.Time;
        for (int f = 1; f <= 2; f++)
        {
            if (contactT.ContainsKey(f)) continue;
            foreach (var u in w.Units)
            {
                if (u.Owner != f || !u.Alive || !u.Def.IsInfantry || u.Weapon == null || u.Def.Scout || u.IsOfficer) continue;
                bool hit = false;
                foreach (var e in w.Units) if (e.Owner != f && e.Alive && e.Def.IsInfantry && !e.Def.Scout && Vector2.Distance(e.Pos, u.Pos) <= u.Weapon.Range * 1.2f && w.Fog.CanSee(f, e)) { hit = true; break; }
                if (hit) { contactT[f] = w.Time; break; }
            }
        }
    }

    public static void Tick(World w)
    {
        foreach (var u in w.Units) if (u.Alive && kills.ContainsKey(u.Id)) kills[u.Id] = u.Kills;
        int per = Math.Max(1, (int)MathF.Round(1f / Config.SimDt));
        if (w.Tick % per == 0) Sample(w);
        foreach (var im in w.Impacts)
        {
            if (im.Tick != w.Tick - 1 || im.Weapon == "grenade" || !who.TryGetValue(im.ShooterId, out var wh)) continue;
            string k = wh.owner + ":" + wh.key; var g = Get(k);
            g.Shells++; if (g.FirstShell < 0) g.FirstShell = w.Time;
            if (!contactT.ContainsKey(wh.owner)) preShells[wh.owner] = preShells.GetValueOrDefault(wh.owner) + 1;
            if (!advanceT.ContainsKey(wh.owner)) prepShells[wh.owner] = prepShells.GetValueOrDefault(wh.owner) + 1;
            float d = Vector2.Distance(im.From, im.Impact); g.DistSum += d;
            g.Bucket[d < 20 ? 0 : d < 40 ? 1 : d < 60 ? 2 : d < 80 ? 3 : 4]++;
            if (w.Find(im.ShooterId) is Unit sh && sh.Weapon is { } sw && !World.IsIndirect(sw) && d > sw.Range * 0.8f)
            {
                g.Far++;
                float eff = sw.Range * 0.8f;
                foreach (var e in w.Units)
                    if (e.Alive && e.Owner != wh.owner && w.IsHostile(wh.owner, e.Owner) && Vector2.Distance(e.Pos, im.From) <= eff && Vector2.Distance(e.Pos, im.From) >= sw.MinRange
                        && w.Fog.CanSee(wh.owner, e) && w.Map.HasFiringLine(im.From, e.Pos, sh.Id, e.Id)) { g.FarAvoidable++; break; }
            }
            int n = 0; double gain = 0;
            foreach (var e in w.Units)
            {
                if (!e.Alive || e.Owner == wh.owner || !w.IsHostile(wh.owner, e.Owner)) continue;
                if (Vector2.Distance(e.Pos, im.Impact) - e.Radius > im.Radius) continue;
                n++; gain += e.Def.SuppressResist;
            }
            if (n > 0) g.Hit++;
            g.EnemyInSplash += n; g.SuppEvents += n; g.SuppGain += gain;   // 이득 = Σ 저항 (× 무기 suppress 는 보고서에서)
        }
    }

    public static void Report(World w)
    {
        var res = new Dictionary<string, object>();
        res["t"] = Math.Round(w.Time, 1); res["winner"] = w.Winner; res["startA"] = startA; res["startB"] = startB;
        res["endA"] = w.Units.Count(u => u.Owner == 1); res["endB"] = w.Units.Count(u => u.Owner == 2);
        var end = new Dictionary<string, int>();
        foreach (var u in w.Units) { string k = u.Owner + ":" + u.Def.Key; end[k] = end.GetValueOrDefault(k) + 1; }
        res["start"] = startKey; res["end"] = end;
        var gun = new Dictionary<string, object>();
        foreach (var (k, g) in gs)
        {
            int kc = 0; foreach (var u in w.Units) { } 
            foreach (var (id, kk) in kills) if (who.TryGetValue(id, out var wh) && wh.owner + ":" + wh.key == k) kc += kk;
            gun[k] = new { units = g.Units, shells = g.Shells, hit = g.Hit, inSplash = g.EnemyInSplash, suppEv = g.SuppEvents, suppRes = Math.Round(g.SuppGain, 1),
                meanDist = g.Shells > 0 ? Math.Round(g.DistSum / g.Shells, 1) : 0, first = Math.Round(g.FirstShell, 1), bucket = g.Bucket, killCredit = kc, far = g.Far, farAvoidable = g.FarAvoidable };
        }
        res["gun"] = gun;
        var n20 = new Dictionary<string, object>();
        for (int f = 1; f <= 2; f++)
        {
            var ids = obsOwner.Where(kv => kv.Value == f).Select(kv => kv.Key).ToList();
            int dead = ids.Count(id => obsDeath.ContainsKey(id));
            n20[f.ToString()] = new { obs = ids.Count, obsAlive = ids.Count - dead, obsDeathMean = dead > 0 ? Math.Round(ids.Where(obsDeath.ContainsKey).Average(id => obsDeath[id]), 1) : -1,
                contact = Math.Round(contactT.GetValueOrDefault(f, -1), 1), preShells = preShells.GetValueOrDefault(f), advance = Math.Round(advanceT.GetValueOrDefault(f, -1), 1), prepShells = prepShells.GetValueOrDefault(f), gunSec = gunSec.GetValueOrDefault(f), gunMoveSec = gunMoveSec.GetValueOrDefault(f), gunMoves = gunMoves.GetValueOrDefault(f) };
        }
        res["n20"] = n20;
        var n22 = new Dictionary<string, object>();
        for (int f = 1; f <= 2; f++)
        {
            var ids = scoutOwner.Where(kv => kv.Value == f).Select(kv => kv.Key).ToList();
            int alive = ids.Count(id => w.Find(id) is Unit u && u.Alive);
            int up = upSec.GetValueOrDefault(f);
            n22[f.ToString()] = new { scouts = ids.Count, scoutsAlive = alive, upSec = up, spotUp = up > 0 ? Math.Round(spotSec.GetValueOrDefault(f) / (double)up, 3) : 0,
                scoutUp = up > 0 ? Math.Round(scoutSeeSec.GetValueOrDefault(f) / (double)up, 3) : 0, scoutSeeMean = up > 0 ? Math.Round(scoutSeeSum.GetValueOrDefault(f) / (double)up, 2) : 0, coverIn = coverIn.GetValueOrDefault(f) };
        }
        res["n22"] = n22;
        if (Environment.GetEnvironmentVariable("KD_ARTY2_OVR") is { Length: > 0 } ov) res["ovr"] = ov;
        Console.WriteLine("ARTYFX " + JsonSerializer.Serialize(res));
    }

    /// <summary>
    /// [측정 전용] 환경변수 KD_ARTY2_OVR = "ai.redeployCooldownSec=20,w.gun_infantry.splash=1.2,u.infantry_scout.hp=100" — 프로세스 메모리의 정의만 바꾼다
    /// (ai. = tactics.json artilleryAi, w.<무기> · u.<유닛> = balance.json 속성 이름, 대소문자 무시). 파일 · 저장 · 골든과 무관 (KD_ARTYFX=1 측정 실행에서만).
    /// </summary>
    static void ApplyOverrides(World w)
    {
        var spec = Environment.GetEnvironmentVariable("KD_ARTY2_OVR");
        if (string.IsNullOrWhiteSpace(spec)) return;
        foreach (var item in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = item.Split('=');
            var path = kv[0].Split('.');
            float val = float.Parse(kv[1], CultureInfo.InvariantCulture);
            object? obj = path[0] switch
            {
                "ai" => w.Defs.Tactics.ArtilleryAi,
                "scout" => w.Defs.ScoutAi,
                "w" => w.Defs.Weapon(path[1]),
                "u" => w.Defs.Units[path[1]],
                _ => null,
            };
            string prop = path[^1];
            var pi = obj?.GetType().GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (pi == null) { Console.WriteLine($"ARTY2_OVR unknown {item}"); continue; }
            object v = pi.PropertyType == typeof(int) ? (object)(int)MathF.Round(val) : pi.PropertyType == typeof(double) ? (object)(double)val : (object)val;
            pi.SetValue(obj, v);
            Console.WriteLine($"ARTY2_OVR {item}");
        }
    }
}
