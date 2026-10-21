using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 대대급 전투 캠페인 측정기 (읽기 전용 — 시뮬 상태 불변). 환경변수 KD_BN_OUT=파일 일 때만 TacticsObs 가 부른다.
/// 매 틱: 사격 로그 (Shots / Impacts) 로 무기별 명중 · 제압 가산 · 근접 탄착 · 살상 · 아군 오사, 유닛 제압 증감 (감쇠 · 상한), 틱 시간.
/// 2초마다: 중대 행 (상태 · 정면 · 간격 · 엄폐 · 측면 노출 · 유휴 · 막힘 · 제압), 대대 (상호 지원 거리 · 전선 틈), 제압 분포.
/// 결과는 JSON 한 개 (counters = 평면 합계 사전, hist = 히스토그램, rows = 중대 행, events = 중대 전환 사건, ser = 5초 시계열).
/// docs/BATTALION_CAMPAIGN_2026-10-19.md, tools/bn_campaign/.
/// </summary>
public static class BnCamp
{
    public static bool On => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KD_BN_OUT"));
    const int SampleEvery = 120;            // 틱 (2초)
    const int Second = 60;
    const long EngagedWin = 300;            // 최근 5초 안 피격 = 교전 중

    sealed class CoInfo
    {
        public int Ini = -1, IniOff, FirstCasT = -1; public string Prev = ""; public double PrevT;
        public readonly Queue<double> Sup = new();
        public double EngageSince = -1; public double SupSinceEngage; public int SupN;
        public float StuckFrac; public readonly Queue<int> AliveHist = new();
    }

    static World W = null!;
    static readonly Dictionary<string, double> C = new();
    static readonly Dictionary<string, int[]> H = new();
    static readonly List<object[]> Rows = new();
    static readonly List<object[]> Events = new();
    static readonly List<object[]> Ser = new();
    static readonly Dictionary<(int, int), CoInfo> Co = new();
    static short[] prevSup = new short[1], prevKills = new short[1];
    static Vector2[] pos6 = new Vector2[1];
    static bool[] seen = new bool[1];
    static long lastShotTick = -1, lastImpTick = -1;
    static readonly List<Unit> buf = new();
    static readonly Dictionary<int, int> tgtShooters = new();
    static readonly HashSet<int> firers = new();
    static readonly Dictionary<string, HashSet<int>> firersCls = new();
    static readonly Dictionary<int, int> startAlive = new();
    static readonly Dictionary<string, int> startByDef = new();
    static readonly Dictionary<int, bool> lastShotMarch = new();
    static bool[] prevCh = new bool[1], prevCmd = new bool[1];
    static readonly Dictionary<int, double> tHalf = new();
    static double firstShot = -1, firstCharge = -1, firstCas = -1;
    static readonly Dictionary<int, double> firstCasSide = new();
    static readonly List<(double t, int k1, int k2)> killBins = new();
    static int cumK1, cumK2, lastBinK1, lastBinK2;
    static double stepMsSum, stepMsMax, obsMsSum; static long stepN;
    static readonly List<float> stepMs = new();
    static long t0Step;
    static string[] names = Array.Empty<string>();

    static void Add(string k, double v = 1) { C[k] = C.GetValueOrDefault(k) + v; }
    static int[] Hist(string k, int n) { if (!H.TryGetValue(k, out var a)) H[k] = a = new int[n]; return a; }

    static string WClass(WeaponDef w) => w.Key switch
    {
        "rifle" or "rifle_long" or "rifle_mosin" or "rifle_berdan2" or "rifle_arisaka38" or "rifle_murata" or "rifle_hanyang88" or "rifle_mauser98" or "rifle_lebel86" or "carbine" or "carbine_assault" => "rifle",
        "lmg" or "mg" => "lmg",
        "hmg" or "hmg_type3" => "hmg",
        "sniper_rifle" => "sniper",
        "pistol" => "pistol",
        "howitzer_heavy" => "howitzer",
        _ => w.Splash > 0 ? "gun" : w.Key
    };
    static int RB(float d) => Math.Min(8, (int)(d / 3f));   // 0-3 .. 24+
    static int SB(float r) => r < 0.05f ? 0 : r < 0.2f ? 1 : r < 0.5f ? 2 : 3;

    static void Ensure(int id)
    {
        if (id < prevSup.Length) return;
        int n = Math.Max(id + 64, prevSup.Length * 2);
        Array.Resize(ref prevSup, n); Array.Resize(ref prevKills, n); Array.Resize(ref pos6, n); Array.Resize(ref seen, n); Array.Resize(ref prevCh, n); Array.Resize(ref prevCmd, n);
    }

    public static void Init(World w)
    {
        W = w; C.Clear(); H.Clear(); Rows.Clear(); Events.Clear(); Ser.Clear(); Co.Clear(); firersCls.Clear(); startAlive.Clear(); tHalf.Clear(); killBins.Clear(); stepMs.Clear(); firstCasSide.Clear();
        lastShotTick = lastImpTick = -1; firstShot = firstCharge = firstCas = -1; cumK1 = cumK2 = lastBinK1 = lastBinK2 = 0; stepMsSum = stepMsMax = obsMsSum = 0; stepN = 0;
        int mx = 0; foreach (var u in w.Units) mx = Math.Max(mx, u.Id);
        prevCh = new bool[mx + 64]; prevCmd = new bool[mx + 64]; prevSup = new short[mx + 64]; prevKills = new short[mx + 64]; pos6 = new Vector2[mx + 64]; seen = new bool[mx + 64];
        foreach (var u in w.Units) { startAlive[u.Owner] = startAlive.GetValueOrDefault(u.Owner) + 1; { var k = $"{u.Owner}|{u.Def.Key}"; startByDef[k] = startByDef.GetValueOrDefault(k) + 1; } pos6[u.Id] = u.Pos; }
    }

    public static void BeforeStep() { t0Step = System.Diagnostics.Stopwatch.GetTimestamp(); }

    public static void Tick(World w)
    {
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0Step) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        stepMsSum += ms; stepN++; if (ms > stepMsMax && w.Tick > 120) stepMsMax = ms; if (stepN % 4 == 0) stepMs.Add((float)ms);
        long o0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var rc = w.Defs.CombatRules;
        // ---- 사격 로그
        long maxShot = lastShotTick;
        foreach (var s in w.Shots)
        {
            if (s.Tick <= lastShotTick) continue;
            if (s.Tick > maxShot) maxShot = s.Tick;
            if (w.Find(s.ShooterId) is not Unit sh || sh.Weapon is not { } wp) continue;
            int f = sh.Owner;
            string cls = WClass(wp);
            if (firstShot < 0 && sh.Def.IsInfantry) firstShot = w.Time;
            Add($"{f}|shots.{cls}");
            bool mfShot = sh.Def.IsInfantry && sh.MarchTarget != 0 && sh.Velocity != Vector2.Zero && (sh.Order == UnitOrder.Move || sh.Order == UnitOrder.AttackMove);   // 행진 사격 (돌격 사격) 으로 본 발
            lastShotMarch[sh.Id] = mfShot;
            if (mfShot && cls == "rifle") { Add($"{f}|mf.shots"); if (s.Hit) Add($"{f}|mf.hits"); Add($"{f}|mf.range", Vector2.Distance(s.From, s.To)); }
            else if (cls == "rifle" && sh.Def.IsInfantry) { Add($"{f}|st.shots"); if (s.Hit) Add($"{f}|st.hits"); Add($"{f}|st.range", Vector2.Distance(s.From, s.To)); }
            firers.Add(sh.Id);
            if (!firersCls.TryGetValue(f + "|" + cls, out var fs)) firersCls[f + "|" + cls] = fs = new();
            fs.Add(sh.Id);
            if (wp.Splash > 0 || wp.Fire == "indirect") continue;          // 포: 착탄 쪽에서 센다
            float range = Vector2.Distance(s.From, s.To);
            int rb = RB(range);
            Add($"{f}|rb.{cls}.{rb}"); if (s.Hit) Add($"{f}|rbh.{cls}.{rb}");
            if (s.Hit) Add($"{f}|hits.{cls}");
            if (cls == "rifle")
            {
                int sb = SB(sh.SuppressionRatio);
                Add($"{f}|sbs.{sb}"); if (s.Hit) Add($"{f}|sbh.{sb}");
                if (range >= 12 && range <= 24) { Add($"{f}|sb2s.{sb}"); if (s.Hit) Add($"{f}|sb2h.{sb}"); }
            }
            // 표적
            Unit? tg = null;
            if (sh.Target is Unit t0 && t0.Alive && w.IsHostile(f, t0.Owner) && Vector2.Distance(t0.Pos, s.To) <= (s.Hit ? 1.0f : 1.2f)) tg = t0;
            if (tg == null)
            {
                float best = 1.2f;
                foreach (var e in w.UnitsInRadius(s.To, 1.5f, buf)) if (e.Alive && w.IsHostile(f, e.Owner)) { float d = Vector2.Distance(e.Pos, s.To); if (d < best) { best = d; tg = e; } }
            }
            if (tg != null) tgtShooters[tg.Id] = tgtShooters.GetValueOrDefault(tg.Id) + 1;
            if (wp.Suppress > 0)
            {
                if (wp.Spray > 0)
                {
                    int n = 0; double g = 0;
                    foreach (var e in w.UnitsInRadius(s.To, wp.Spray + 0.5f, buf))
                    {
                        if (!e.Alive || !w.IsHostile(f, e.Owner) || Vector2.Distance(s.To, e.Pos) - e.Radius > wp.Spray) continue;
                        double gain = wp.Suppress * e.Def.SuppressResist * w.CoverSuppressMul(e);
                        if (gain > 0) { n++; g += gain; }
                    }
                    Add($"{f}|gain.{cls}", g); Add($"{f}|gainN.{cls}", n);
                    if (!s.Hit && n > 1) Add($"{f}|nm.sprayNeighbors", n - 1);
                }
                else if (tg != null)
                {
                    double gain = wp.Suppress * tg.Def.SuppressResist * w.CoverSuppressMul(tg);
                    Add($"{f}|gain.{cls}", gain); Add($"{f}|gainN.{cls}", gain > 0 ? 1 : 0);
                }
            }
            // 근접 탄착 (가상): 빗나간 탄이 표적 외 적 병사 1.5칸 안을 지났는가 (이 엔진은 탄도 없음 — 제압은 표적만)
            if (!s.Hit && wp.Spray <= 0 && (cls == "rifle" || cls == "sniper"))
            {
                Add($"{f}|nm.shots");
                var pass = new HashSet<int>();
                float len = range; int steps = Math.Max(1, (int)(len / 1.5f));
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector2.Lerp(s.From, s.To, (float)i / steps);
                    foreach (var e in w.UnitsInRadius(p, 1.5f, buf)) if (e.Alive && e != tg && w.IsHostile(f, e.Owner) && e.Def.IsInfantry && Vector2.Distance(e.Pos, p) <= 1.5f) pass.Add(e.Id);
                }
                Add($"{f}|nm.passing", pass.Count); if (pass.Count > 0) Add($"{f}|nm.shotsWithPassing");
                int near = 0;
                foreach (var e in w.UnitsInRadius(s.To, 1.5f, buf)) if (e.Alive && e != tg && w.IsHostile(f, e.Owner) && e.Def.IsInfantry && Vector2.Distance(e.Pos, s.To) <= 1.5f) near++;
                Add($"{f}|nm.impactNear", near);
            }
        }
        lastShotTick = maxShot;
        // ---- 착탄 (포 · 직사포 · 수류탄)
        long maxImp = lastImpTick;
        foreach (var im in w.Impacts)
        {
            if (im.Tick <= lastImpTick) continue;
            if (im.Tick > maxImp) maxImp = im.Tick;
            if (im.Weapon == "grenade" || w.Find(im.ShooterId) is not Unit sh || sh.Weapon is not { } wp) continue;
            int f = im.Faction; string cls = WClass(wp);
            Add($"{f}|imp.{cls}");
            int en = 0, fr = 0; double g = 0;
            foreach (var e in w.UnitsInRadius(im.Impact, im.Radius + 1.0f, buf))
            {
                if (!e.Alive || Vector2.Distance(e.Pos, im.Impact) - e.Radius > im.Radius) continue;
                if (w.IsHostile(f, e.Owner)) { en++; g += wp.Suppress * e.Def.SuppressResist * w.CoverSuppressMul(e); }
                else if (e.Owner == f || w.Allied(f, e.Owner)) fr++;
            }
            Add($"{f}|impEnemy.{cls}", en); Add($"{f}|gain.{cls}", g); Add($"{f}|gainN.{cls}", en);
            if (fr > 0) { Add($"{f}|ff.impacts"); Add($"{f}|ff.units", fr); }
            Add($"{f}|ff.exposedImpacts");
            if (en == 0) Add($"{f}|imp0.{cls}");
        }
        lastImpTick = maxImp;
        // ---- 유닛 제압 증감 · 살상
        foreach (var u in w.Units)
        {
            if (!u.Alive) continue;
            Ensure(u.Id);
            int f = u.Owner;
            if (u.Def.IsInfantry)
            {
                short s = u.Suppression; short ps = prevSup[u.Id];
                int d = s - ps;
                if (d < 0) { Add($"{f}|dec.total", -d); if (ps < 0.2 * rc.SuppressMax) Add($"{f}|dec.low", -d); }
                else if (d > 0) { Add($"{f}|gain.obs", d); }
                if (s >= rc.SuppressMax) Add($"{f}|cap.unitTicks");
                prevSup[u.Id] = s;
            }
            if (u.Def.IsInfantry && !u.Def.IsOfficer && u.Id < prevCh.Length)
            {
                bool ch = u.Charging;
                bool cmd = u.Order == UnitOrder.AttackMove && u.Velocity != Vector2.Zero && w.MarchCommanded(u);
                if (ch && !prevCh[u.Id]) { Add($"{f}|chg.start"); if (prevCmd[u.Id]) Add($"{f}|chg.fromMarch"); }
                prevCh[u.Id] = ch; prevCmd[u.Id] = cmd;
            }
            short k = u.Kills; short pk = prevKills[u.Id];
            if (!seen[u.Id]) { seen[u.Id] = true; pk = k; }
            if (k > pk)
            {
                int dk = k - pk;
                string kc;
                if (u.Def.Key.StartsWith("cavalry")) kc = "cav";
                else if (u.Target is { } tt && Vector2.Distance(tt.Pos, u.Pos) < 1.6f && (u.Charging || u.Weapon == null || u.Weapon.Range < 1f || Vector2.Distance(tt.Pos, u.Pos) < 1.0f)) kc = "melee";
                else kc = u.Weapon != null ? WClass(u.Weapon) : "none";
                Add($"{f}|kills.{kc}", dk); Add($"{f}|kills.total", dk);
                if (kc == "rifle") Add(lastShotMarch.GetValueOrDefault(u.Id) ? $"{f}|mf.kills" : $"{f}|st.kills", dk);
                if (f == 1) cumK1 += dk; else cumK2 += dk;
            }
            prevKills[u.Id] = k;
        }
        if (w.Tick % Second == 0) PerSecond(w);
        if (w.Tick % SampleEvery == 0) Sample(w);
        obsMsSum += (System.Diagnostics.Stopwatch.GetTimestamp() - o0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    static void PerSecond(World w)
    {
        // 목표 집중 (과잉 사격) · 사수당 제압 시간
        foreach (var (key, set) in firersCls) { Add(key.Split('|')[0] + "|firersec." + key.Split('|')[1], set.Count); set.Clear(); }
        int nf = 0; foreach (var f in new[] { 1, 2 }) nf += 0;
        if (tgtShooters.Count > 0)
        {
            // 사격자 집합이 세력 구분 없이 합쳐져 있으므로 세력별로 다시 센다
            var perSide = new Dictionary<int, (int firers, int tg, int hot, int hotF)>();
            foreach (var (id, n) in tgtShooters)
            {
                if (w.Find(id) is not Unit t) continue;
                int atk = t.Owner == 1 ? 2 : 1;
                var v = perSide.GetValueOrDefault(atk);
                v.firers += n; v.tg++; if (n >= 3) { v.hot++; v.hotF += n; }
                perSide[atk] = v;
            }
            foreach (var (atk, v) in perSide) { Add($"{atk}|conc.firers", v.firers); Add($"{atk}|conc.targets", v.tg); Add($"{atk}|conc.hot", v.hot); Add($"{atk}|conc.hotFirers", v.hotF); }
        }
        tgtShooters.Clear(); firers.Clear();
        // 무장 시간 · 제압 구간별 사격률
        long tk = w.Tick;
        foreach (var u in w.Units)
        {
            if (!u.Alive || u.Weapon == null) continue;
            int f = u.Owner; string cls = WClass(u.Weapon);
            Add($"{f}|armedsec.{cls}");
            if (u.Def.IsInfantry && !u.Def.IsOfficer && !u.Charging)
            {
                string ut = u.Def.Key == "infantry_scout" ? "scout" : u.Def.Key is "heavy_machinegunner" or "heavy_machinegunner_fengtian" ? "lmg" : "rifle";
                bool am = u.Order == UnitOrder.AttackMove && u.Velocity != Vector2.Zero && u.Group != null && !u.CanDeploy && !u.QuickStep;
                if (am)
                {
                    Add($"{f}|mf.amsec.{ut}");
                    if (u.Company is { MarchFireOn: true }) Add($"{f}|mf.on.{ut}");
                    if (u.Company is { MarchFireOn: true } && u.Suppression >= w.Defs.CombatRules.MarchMaxSuppress) Add($"{f}|mf.block.{ut}");
                    if (w.MarchCommanded(u)) { Add($"{f}|mf.cmd.{ut}"); if (u.MarchTarget != 0) Add($"{f}|mf.tgt.{ut}"); }
                }
                else if (u.Order == UnitOrder.Move && u.Velocity != Vector2.Zero) Add($"{f}|mf.movesec.{ut}");
            }
            if (tk - u.LastShotTick < EngagedWin) Add($"{f}|actsec.{cls}");   // 활동 사수-초: 최근 5초 안 쏜 유닛
            if (cls == "rifle" && u.Def.IsInfantry)
            {
                bool eng = tk - u.LastFiredAtTick < EngagedWin;
                if (eng) { int sb = SB(u.SuppressionRatio); Add($"{f}|sbsec.{sb}"); if (tk - u.LastShotTick < Second) Add($"{f}|sbfire.{sb}"); if (u.Pinned) Add($"{f}|pinsec.eng"); }
                if (!u.IsOfficer) { Add($"{f}|infsec"); if (u.Pinned) Add($"{f}|pinsec.all"); }
            }
        }
    }

    static void Sample(World w)
    {
        double t = w.Time;
        var rc = w.Defs.CombatRules;
        var alive = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        var charging = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        var cen = new Dictionary<int, Vector2>(); var cnt = new Dictionary<int, int>();
        foreach (var u in w.Units)
        {
            if (!u.Alive) continue;
            alive[u.Owner] = alive.GetValueOrDefault(u.Owner) + 1;
            if (u.Charging) charging[u.Owner] = charging.GetValueOrDefault(u.Owner) + 1;
            if (u.Weapon != null) { cen[u.Owner] = cen.GetValueOrDefault(u.Owner) + u.Pos; cnt[u.Owner] = cnt.GetValueOrDefault(u.Owner) + 1; }
        }
        foreach (var f in new[] { 1, 2 })
        {
            if (cnt.GetValueOrDefault(f) > 0) cen[f] /= cnt[f];
            int lost = startAlive.GetValueOrDefault(f) - alive.GetValueOrDefault(f);
            if (lost > 0 && !firstCasSide.ContainsKey(f)) firstCasSide[f] = t;
            if (lost > 0 && firstCas < 0) firstCas = t;
            if (!tHalf.ContainsKey(f) && alive[f] < startAlive.GetValueOrDefault(f) * 0.5) tHalf[f] = t;
            if (charging[f] >= 8 && firstCharge < 0) firstCharge = t;
        }
        if (w.Tick % (Second * 5) == 0) Ser.Add(new object[] { Math.Round(t), alive[1], alive[2], charging[1], charging[2], cumK1, cumK2 });
        if (w.Tick % (Second * 10) == 0) { killBins.Add((t, cumK1 - lastBinK1, cumK2 - lastBinK2)); lastBinK1 = cumK1; lastBinK2 = cumK2; }
        // ---- 중대
        foreach (var (f, ai) in w.Controllers)
        {
            if (ai is not TacticalAi ta || ta.StateOf(f) is not { } s || s.Phase < 2) continue;
            int foe = f == 1 ? 2 : 1;
            var foeUnits = new List<Unit>(); foreach (var u in w.Units) if (u.Alive && u.Owner != f && w.IsHostile(f, u.Owner) && u.Weapon != null) foeUnits.Add(u);
            if (foeUnits.Count == 0) continue;
            var foeC = new Vector2(foeUnits.Average(u => u.Pos.X), foeUnits.Average(u => u.Pos.Y));
            // 적 라인 중대 무게중심
            var foeCos = new List<Vector2>();
            if (w.Controllers.FirstOrDefault(c => c.Faction == foe).Ai is TacticalAi fa && fa.StateOf(foe) is { } fs)
                foreach (var g in fs.Groups) if (g.Kind == TacKind.Line && g.Alive > 0) { var m = Mem(w, g); if (m.Count > 0) foeCos.Add(new Vector2(m.Average(u => u.Pos.X), m.Average(u => u.Pos.Y))); }
            var lines = new List<(TacGroup g, List<Unit> mem, Vector2 c, float width, float perp, float strength)>();
            foreach (var g in s.Groups)
            {
                if (g.Kind != TacKind.Line || g.Alive <= 0) continue;
                var mem = Mem(w, g); if (mem.Count == 0) continue;
                var inf = mem.Where(u => u.Def.IsInfantry && !u.IsOfficer && !u.Def.Scout).ToList();
                if (inf.Count < 3) continue;
                var c = new Vector2(inf.Average(u => u.Pos.X), inf.Average(u => u.Pos.Y));
                var nearestFoe = foeCos.Count > 0 ? foeCos.OrderBy(p => Vector2.DistanceSquared(p, c)).First() : foeC;
                var fwd = nearestFoe - c; if (fwd.LengthSquared() < 1e-3f) fwd = new Vector2(0, 1); fwd = Vector2.Normalize(fwd);
                var perp = new Vector2(-fwd.Y, fwd.X);
                var key = (f, g.Id);
                if (!Co.TryGetValue(key, out var ci)) Co[key] = ci = new CoInfo();
                int offAlive = mem.Count(u => u.IsOfficer);
                if (ci.Ini < 0) { ci.Ini = inf.Count; ci.IniOff = offAlive; }
                if (inf.Count < ci.Ini && ci.FirstCasT < 0) ci.FirstCasT = (int)t;
                // 형태
                var pp = inf.Select(u => Vector2.Dot(u.Pos - c, perp)).OrderBy(x => x).ToList();
                var dd = inf.Select(u => Vector2.Dot(u.Pos - c, fwd)).OrderBy(x => x).ToList();
                float width = Q(pp, 0.95f) - Q(pp, 0.05f), depth = Q(dd, 0.95f) - Q(dd, 0.05f);
                int clump = 0; double nn = 0;
                for (int i = 0; i < inf.Count; i++)
                {
                    float best = 99; for (int j = 0; j < inf.Count; j++) if (i != j) best = MathF.Min(best, Vector2.Distance(inf[i].Pos, inf[j].Pos));
                    if (best < 1.0f) clump++; nn += best;
                }
                nn /= inf.Count;
                int uf = 0, cTrench = 0, cForest = 0, cOther = 0, firing = 0, moving = 0;
                double supSum = 0, supEng = 0; int engN = 0;
                foreach (var u in inf)
                {
                    float r = u.SuppressionRatio; supSum += r;
                    if (w.Tick - u.LastShotTick < 120) firing++;
                    if (w.Tick - u.LastMoveTick < 60) moving++;
                    if (w.Tick - u.LastFiredAtTick < EngagedWin)
                    {
                        uf++; supEng += r; engN++;
                        var (tx, ty) = GameMap.TileOf(u.Pos);
                        var tt = w.Map.InBounds(tx, ty) ? w.Map.Get(tx, ty) : TerrainType.Grass;
                        var cv = w.Map.TerrainCover(u.Pos);
                        if (tt == TerrainType.Trench) cTrench++; else if (tt == TerrainType.Forest) cForest++; else if (cv.Hit < 0.999f) cOther++;
                    }
                }
                // 측면 노출: 이 중대를 노리는 적 사수의 방향 대 중대 정면 (마지막 이동 도착 방향)
                int flank = -1; int shooters = 0;
                {
                    var sc = Vector2.Zero;
                    foreach (var e in foeUnits) if (e.Target is Unit tu && tu.Company == (g.CompanyId > 0 ? w.FindCompany(g.CompanyId) : null) && g.CompanyId > 0 && Vector2.Distance(e.Pos, c) < 40) { sc += e.Pos; shooters++; }
                    if (shooters >= 3)
                    {
                        sc /= shooters; var dir = sc - c; var face = w.FindCompany(g.CompanyId)?.Facing ?? Vector2.Zero;
                        if (face.LengthSquared() < 0.01f || dir.LengthSquared() < 0.01f) face = fwd;
                        float ang = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(dir), Vector2.Normalize(face)), -1, 1)) * 180f / MathF.PI;
                        flank = ang > 100 ? 2 : ang > 60 ? 1 : 0;
                    }
                }
                float nearestEnemy = 999; foreach (var e in foeUnits) nearestEnemy = MathF.Min(nearestEnemy, Vector2.Distance(e.Pos, c));
                bool idleOOR = firing == 0 && moving <= inf.Count * 0.05 && nearestEnemy > 30;
                bool idleIn = firing == 0 && moving <= inf.Count * 0.05 && nearestEnemy <= 30;
                // 막힘 (6초마다)
                if (w.Tick % (Second * 6) == 0)
                {
                    int stuck = 0, ordered = 0;
                    foreach (var u in inf)
                    {
                        Ensure(u.Id);
                        if ((u.Order == UnitOrder.Move || u.Order == UnitOrder.AttackMove) && !u.Engaging) { ordered++; if (Vector2.Distance(u.Pos, pos6[u.Id]) < 0.4f) stuck++; }
                        pos6[u.Id] = u.Pos;
                    }
                    ci.StuckFrac = inf.Count > 0 ? stuck / (float)inf.Count : 0;
                    Add($"{f}|stuck.units", stuck); Add($"{f}|stuck.ordered", ordered); Add($"{f}|stuck.windows"); if (ci.StuckFrac >= 0.2f) Add($"{f}|stuck.jams");
                }
                ci.AliveHist.Enqueue(inf.Count); while (ci.AliveHist.Count > 16) ci.AliveHist.Dequeue();
                double lost30 = (ci.AliveHist.Peek() - inf.Count) / (double)ci.Ini;
                int foe30 = 0; foreach (var e in foeUnits) if (Vector2.Distance(e.Pos, c) < 30) foe30++;
                double supMean = supSum / inf.Count;
                ci.Sup.Enqueue(supMean); while (ci.Sup.Count > 15) ci.Sup.Dequeue();
                double sup10 = ci.Sup.TakeLast(5).Average(), sup30 = ci.Sup.Average();
                string st = g.State.ToString();
                if (st == "Engage" && ci.EngageSince < 0) { ci.EngageSince = t; ci.SupSinceEngage = 0; ci.SupN = 0; }
                if (st == "Engage") { ci.SupSinceEngage += supMean; ci.SupN++; }
                if (ci.Prev != "" && ci.Prev != st)
                {
                    double supMeanEng = ci.SupN > 0 ? ci.SupSinceEngage / ci.SupN : 0;
                    int overFrac = inf.Count(u => u.SuppressionRatio >= 0.2f);
                    Events.Add(new object[] { Math.Round(t), f, g.Id, ci.Prev, st, inf.Count, ci.Ini, offAlive, ci.IniOff, Math.Round(sup10, 3), Math.Round(sup30, 3), Math.Round(supMeanEng, 3), Math.Round(overFrac / (double)inf.Count, 3), ci.FirstCasT, Math.Round(nearestEnemy, 1), g.Role.ToString(), Math.Round(width, 1), Math.Round(lost30, 3), foe30, g.RetreatCause, Math.Round(TacticalAi.ContactSuppression(w, mem), 3) });
                    if (st != "Engage") ci.EngageSince = -1;
                }
                ci.Prev = st;
                lines.Add((g, mem, c, width, 0f, inf.Count / (float)ci.Ini));
                Rows.Add(new object[] { Math.Round(t), f, g.Id, st, g.Role.ToString(), inf.Count, ci.Ini, offAlive, ci.IniOff, Math.Round(c.X, 1), Math.Round(c.Y, 1), Math.Round(width, 1), Math.Round(depth, 1), Math.Round(clump / (double)inf.Count, 3), Math.Round(nn, 2), uf, cTrench, cForest, cOther, flank, shooters, Math.Round(supMean, 3), engN > 0 ? Math.Round(supEng / engN, 3) : -1, firing, moving, idleOOR ? 1 : 0, idleIn ? 1 : 0, Math.Round(ci.StuckFrac, 2), Math.Round(nearestEnemy, 1) });
            }
            // 대대: 상호 지원 거리 · 전선 틈
            var act = lines.Where(l => l.strength >= 0.25f).ToList();
            if (act.Count >= 2)
            {
                var oc = new Vector2(act.Average(l => l.c.X), act.Average(l => l.c.Y)); var ax = foeC - oc; ax = ax.LengthSquared() < 1e-3f ? new Vector2(0, 1) : Vector2.Normalize(ax); var pv = new Vector2(-ax.Y, ax.X);
                for (int i = 0; i < act.Count; i++) act[i] = (act[i].g, act[i].mem, act[i].c, act[i].width, Vector2.Dot(act[i].c, pv), act[i].strength);
            }
            if (act.Count >= 2)
            {
                foreach (var a in act)
                {
                    float nd = 999; foreach (var b in act) if (a.g != b.g) nd = MathF.Min(nd, Vector2.Distance(a.c, b.c));
                    Add($"{f}|sup.dist", nd); Add($"{f}|sup.n"); if (nd <= 24) Add($"{f}|sup.within24"); if (nd <= 40) Add($"{f}|sup.within40");
                }
                var srt = act.OrderBy(l => l.perp).ToList();
                double maxGap = -99, sumGap = 0; int ng = 0;
                for (int i = 0; i + 1 < srt.Count; i++) { double gap = (srt[i + 1].perp - srt[i].perp) - (srt[i].width + srt[i + 1].width) / 2; maxGap = Math.Max(maxGap, gap); sumGap += gap; ng++; }
                Add($"{f}|gap.max", maxGap); Add($"{f}|gap.mean", sumGap / ng); Add($"{f}|gap.n"); if (maxGap > 20) Add($"{f}|gap.over20"); if (maxGap > 40) Add($"{f}|gap.over40");
            }
        }
        // ---- 제압 분포 (교전 중 / 전체 보병)
        foreach (var u in w.Units)
        {
            if (!u.Alive || !u.Def.IsInfantry || u.IsOfficer) continue;
            int b = Math.Min(100, (int)(u.SuppressionRatio * 100));
            Hist($"{u.Owner}|supAll", 101)[b]++;
            if (w.Tick - u.LastFiredAtTick < EngagedWin) Hist($"{u.Owner}|supEng", 101)[b]++;
        }
    }

    static List<Unit> Mem(World w, TacGroup g)
    {
        if (g.CompanyId > 0 && w.FindCompany(g.CompanyId) is { } co) return co.Members.Where(u => u.Alive).ToList();
        var l = new List<Unit>(); foreach (var id in g.Units) if (w.Find(id) is Unit u) l.Add(u); return l;
    }
    static float Q(List<float> sorted, float q) => sorted.Count == 0 ? 0 : sorted[Math.Clamp((int)(q * (sorted.Count - 1)), 0, sorted.Count - 1)];

    public static void Finish(World w, string path, string missionId, int? seed, string result, double dur)
    {
        var o = new Dictionary<string, object>();
        o["mission"] = missionId; o["seed"] = seed ?? -1; o["result"] = result; o["dur"] = Math.Round(dur, 1); o["ticks"] = w.Tick;
        o["start"] = new Dictionary<string, int> { ["1"] = startAlive.GetValueOrDefault(1), ["2"] = startAlive.GetValueOrDefault(2) };
        var alive = new Dictionary<string, int>(); foreach (var f in new[] { 1, 2 }) alive[f.ToString()] = w.Units.Count(u => u.Owner == f && u.Alive);
        o["alive"] = alive;
        // 캠페인 2: 병종(유닛 정의)별 시작 · 생존 (포 · 기병 · 기관총 생존, 대포병 지표). 읽기 전용.
        var byDef = new Dictionary<string, int>(); foreach (var u in w.Units) { var k = $"{u.Owner}|{u.Def.Key}"; byDef[k] = byDef.GetValueOrDefault(k) + (u.Alive ? 1 : 0); }
        o["aliveByDef"] = byDef; o["startByDef"] = startByDef;
        // 최종 중대 상태
        var fin = new List<object>();
        foreach (var (f, ai) in w.Controllers) if (ai is TacticalAi ta && ta.StateOf(f) is { } s) foreach (var g in s.Groups) if (g.Kind == TacKind.Line) fin.Add(new object[] { f, g.Id, g.State.ToString(), g.Alive, g.Initial });
        o["finalGroups"] = fin;
        // stage 3A 기동 계획 기록 (세력 → PlanLog 평면 int, TacticalAi.PlanLogStride 개씩) · 누계
        var plans = new Dictionary<string, object>();
        foreach (var (f, ai) in w.Controllers)
            if (ai is TacticalAi ta && ta.StateOf(f) is { } s)
                plans[f.ToString()] = new Dictionary<string, object> { ["log"] = s.PlanLog.ToList(), ["stride"] = TacticalAi.PlanLogStride, ["profile"] = s.Profile.Name, ["manoeuvre"] = s.Profile.Strategy.Manoeuvre,
                    ["frontal"] = s.PlansFrontal, ["flank"] = s.PlansFlank, ["pincer"] = s.PlansPincer, ["schwerpunkt"] = s.PlansSchwerpunkt, ["releases"] = s.PlanReleases, ["aborts"] = s.PlanAborts, ["withdrawals"] = s.Withdrawals,
                    ["open"] = s.Regiments.Where(r => r.Plan is ManPlan.Flank or ManPlan.Pincer && r.PlanStage == 1).Select(r => new[] { r.Id, (int)r.Plan, r.PlanSide, (int)r.PlanTick, (int)r.FixTick, (int)r.ArriveTick }).ToList() };
        o["plans"] = plans;
        o["firstShot"] = firstShot; o["firstCharge"] = firstCharge; o["firstCas"] = firstCas;
        o["firstCasSide"] = firstCasSide.ToDictionary(k => k.Key.ToString(), k => k.Value);
        o["tHalf"] = tHalf.ToDictionary(k => k.Key.ToString(), k => k.Value);
        o["killBins"] = killBins.Select(b => new object[] { b.t, b.k1, b.k2 }).ToList();
        stepMs.Sort();
        o["perf"] = new Dictionary<string, double> { ["stepMean"] = stepN > 0 ? stepMsSum / stepN : 0, ["stepMax"] = stepMsMax, ["stepP99"] = stepMs.Count > 0 ? stepMs[(int)(stepMs.Count * 0.99)] : 0, ["obsMean"] = stepN > 0 ? obsMsSum / stepN : 0, ["ticks"] = stepN };
        C["nearMissHits"] = w.Stats.NearMissHits; C["pinEvents"] = w.Stats.PinEvents; C["pinnedTicks"] = w.Stats.PinnedTicks;   // 제압 재설계 (N25)
        foreach (var (f, ai) in w.Controllers) if (ai is TacticalAi ta && ta.StateOf(f) is { } s) { C[$"{f}|fallbacks"] = s.Fallbacks; C[$"{f}|fallbacksSupp"] = s.FallbacksSupp; }
        o["counters"] = C.ToDictionary(k => k.Key, k => Math.Round(k.Value, 3));
        o["hist"] = H;
        o["rowCols"] = new[] { "t", "side", "gid", "state", "role", "alive", "ini", "off", "iniOff", "cx", "cy", "width", "depth", "clump", "nn", "underFire", "cTrench", "cForest", "cOther", "flank", "shooters", "sup", "supEng", "firing", "moving", "idleOOR", "idleIn", "stuck", "nearestEnemy" };
        o["rows"] = Rows;
        o["eventCols"] = new[] { "t", "side", "gid", "from", "to", "alive", "ini", "off", "iniOff", "sup10", "sup30", "supEngage", "frac02", "firstCasT", "nearestEnemy", "role", "width", "lost30", "foe30", "cause", "supC" };
        o["events"] = Events;
        o["ser"] = Ser;
        File.WriteAllText(path, JsonSerializer.Serialize(o), new UTF8Encoding(false));
    }
}
