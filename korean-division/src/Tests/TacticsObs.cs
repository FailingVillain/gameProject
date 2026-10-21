using System.Globalization;
using System.Numerics;
using System.Text;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 전술 관찰 도구 (읽기 전용 — 시뮬 상태를 바꾸지 않음, 틱 결과 불변): <c>KoreanDivision --tactics-obs &lt;미션id&gt; [--seed N] [--seconds S] [--out 파일]</c>.
/// 1초마다 전술 AI 상태 (TacticalAi.StateOf) · 월드 중대 · 유닛을 훑어 중대 상태 시간 · 전환 · 트리거 · 약진 · 화력 기반 제압 · 예비 투입 · 행진 사격 ·
/// 장교 승계 · 포병 · 기병 돌격 · 중화기 앞섬 · 유휴 중대 · 손실 곡선을 집계해 보고서로 출력한다. docs/TACTICS_OBS_2026-10-19.md.
/// </summary>
public static class TacticsObs
{
    sealed class GState { public long SqSince; public TacState S; public long Since; public int Sq; public bool Idle; public long IdleSince = -1; public bool IdleLogged; public int CmdId = -1; public bool March; public DrillPhase DPhase; public byte DMan; public long DLift; public int DBoundSq; public long DBoundSince; }
    sealed class AsPend { public double T0; public int Gid, F, E; public Vector2 P; public string Who = "", Kind = ""; public bool Retreat; }
    sealed class Side
    {
        public Dictionary<TacState, double> StateSec = new();
        public Dictionary<string, int> Trans = new();
        public Dictionary<string, double> RoleSec = new();
        public List<string> Log = new();
        public List<(double t, double sup, int n, string who, bool fireBase, double gap)> Manoeuvre = new();
        public double OverwatchSum; public int OverwatchN;
        public Dictionary<int, (int gid, double dist)> SpotLast = new();   // S3: 관측반 유닛 → (팀 그룹 id, 마지막 표본의 가장 가까운 적 거리)
        public Dictionary<int, HashSet<int>> SpotTeam = new();             // S3: 팀 그룹 id → 본 적 있는 유닛 id
        public List<(double t, int gid, int uid, double dist)> SpotDeaths = new();
        public int MarchActs, Successions, HandoverSec, NcoSec, OfficerSec, NoneSec;
        public int CavOfficerSec, CavNcoSec, CavNoneSec, CavHandoverSec, CavSuccessions;   // cavcmd: 기병 중대 지휘
        public double ArtFireSec, ArtSec, ArtShootSec, RegFireTargetSec, RegLiftSec; public int ArtGroups;
        public double HeavyAheadSec, HeavySec; public int HeavyAheadMax;
        public double IdleSec, IdleNearSec; public List<string> IdleEpisodes = new();
        public List<(double t, int alive)> Curve = new();
        public Dictionary<string, List<double>> Counters = new();
        public List<string> Cav = new();
        public Dictionary<string, double> TemplateSec = new();
        public double FirstEngage = -1, FirstManoeuvre = -1;
        public Dictionary<TacState, List<double>> Dwell = new();
        public List<double> BoundLegs = new();
        public List<string> Flips = new();
        public double SuppSum; public int SuppN; public double SuppPeak, SuppPeakT, SuppFracPeak, SuppFracPeakT, SuppUnitMax; public int SuppOver30;
        public List<(double t, double mean, double frac30)> Supp = new();
        public List<float> SuppEng = new(), SuppAllInf = new();
        public List<float> ManSupEng = new(), ManSupMax = new();
        public Dictionary<string, double> ActiveTacSec = new();
        public Dictionary<string, int> DrillEv = new();
        public List<string> DrillLog = new();
        public List<double> DrillBoundLegs = new();
        public int PrematureCharges, ChargeLaunches; public List<string> ChargeLog = new();
        public C3 X = new();
        public List<float> RankDisp = new(); public List<int> RankFront = new(), RankGap = new(); public int RankAlarmSec;
        public SortedDictionary<string, long> RankDiag = new();
        public List<int> RankHoles = new(), RankHoleGap = new(), RankBehind = new(), RankSlots = new(); public int RankBroken;   // gapfill2 앞줄 구멍 (1초 표본) · 판단 문턱 넘은 표본   // gapfill: 교전 중 중대 대열 흐트러짐 (1초 표본)
        public double OffBackSum, OffFrontIn, OffFrontN, OffAllIn, OffAllN; public int OffBackN; public Dictionary<char, double> ClsSec = new(), ClsSupp = new(); public Dictionary<char, int> ClsDeath = new(); public Dictionary<int, char> LastCls = new();
        public double RngSum; public int RngN; public List<float> Rng = new(); public int RngLow; public double LeaderlessAfter, TotalAfter; public Dictionary<string, double> TplAfter = new();
    }

    // ---- 3차 지표 (조직적 협동) ----
    sealed class CoSt { public long PhaseSeen = -1; public double FireOpenT = -1; public bool ManLogged; public int PrevBound; public byte PrevMan; public TacState PrevState; }
    sealed class ArtPend { public double T0; public Vector2 P; public string Who = ""; public float Best = 999; public bool Fired; }
    sealed class CavPend { public double T0; public Vector2 P; public int Id, Shooters, Peak; public bool AssaultNear; public string Who = ""; }
    sealed class C3
    {
        public List<AsPend> AsP = new(); public List<string> AsDone = new();
        public Dictionary<int, CoSt> Co = new();
        public double BoundPartnerSum, BoundMoverSum, ManPartnerSum, ManSisterSum, BasePartnerSum; public int BoundN, ManN, ManSisterN, BaseN;
        public int BoundPartnerSilent, BoundPartnerHalf, ManPartnerSilent, ManPartnerHalf;
        public Dictionary<string, List<(double sup, int cnt, double age)>> SupEv = new();
        public List<float> SupAllEngage = new(); public int SupAllSuperior, SupAllN;
        public List<string> SupLog = new();
        public Dictionary<string, int> TickStart = new(); public Dictionary<int, byte> PrevBoundT = new(), PrevManT = new();
        public List<(double lag, double ratio, string kind)> Lag = new();
        public List<(double lag, double ratio)> BnLag = new();
        public Dictionary<int, double> BnFireOpen = new();
        public List<string> LagLog = new();
        public int PrevReserve; public List<string> ResLog = new(); public int ResTrouble, ResTotal, TroubleSec, TotalSec; public Queue<int> AliveHist = new();
        public List<ArtPend> ArtP = new(); public List<string> ArtLog = new(); public int ArtAssaults, ArtOnTarget, ArtFiredAny;
        public ArtM AM = new(); public Queue<(double t, Vector2 p)> ArtAim = new(); public int ArtAimN, ArtAimOnEngaged;
        public List<double> AssaultTimes = new();
        public List<CavPend> CavP = new(); public List<string> CavLog = new(); public int CavN, CavShoot10, CavAssaultNear;
    }
    /// <summary>6차: 포병 역할 지표 (읽기 전용, 1초 샘플). 사격 = LastShotTick 갱신 감지.</summary>
    sealed class ArtM
    {
        public Dictionary<int, long> Seen = new(); public Dictionary<int, (Vector2 p, float f)> Prev = new();
        public Dictionary<string, int> Shots = new(), Unseen = new(), Far = new(), Short = new();
        public Dictionary<string, double> DistSum = new();
        public int Traverse, GunSec, MoveSec, HoldSec, NoTgtShots;
        public double FirstArt = -1, FirstInf = -1, FirstContact = -1; public int ArtBeforeInf, ArtBeforeContact, ArtOnAssaultShots;
        public List<(double t, Vector2 p, List<int> ids, float sup)> Pend = new();
        public int EffN, EffKills, EffSupN; public double EffSupSum;
        public int ArtSecTarget, ArtSecShoot;
    }
    sealed class PendCav { public double T0; public int F; public Vector2 C; public int Id, Base, Peak, Riders; public PendCav(double t0, int f, Vector2 c, int id, int b, int riders = 0) { T0 = t0; F = f; C = c; Id = id; Base = b; Peak = b; Riders = riders; } }

    static float SupThr = 0.55f, PatThr = 60f;

    /// <summary>5차: 매 틱 약진 / 기동조 시작 순간에 AI 와 같은 식 (TacticDrill 248행) 으로 분류 — 1초 샘플의 과소집계 해결. 읽기 전용.</summary>
    static void TickStarts(World w, Dictionary<int, Side> sides)
    {
        foreach (var (f, ai) in w.Controllers)
        {
            if (ai is not TacticalAi ta || ta.StateOf(f) is not { } s || !sides.TryGetValue(f, out var sd)) continue;
            var x = sd.X; var R = w.Defs.Tactics.Behaviour;
            foreach (var g in s.Groups)
            {
                if (g.Kind != TacKind.Line || g.Alive <= 0 || w.FindCompany(g.CompanyId) is not { } co) continue;
                var dr = co.Drill;
                byte pb = x.PrevBoundT.GetValueOrDefault(g.Id), pm = x.PrevManT.GetValueOrDefault(g.Id);
                byte nb = (byte)(dr.BoundSquad > 0 ? 1 : 0), nm = (byte)(dr.Man is 1 or 2 ? 1 : 0);
                x.PrevBoundT[g.Id] = nb; x.PrevManT[g.Id] = nm;
                bool bs = nb == 1 && pb == 0, ms = nm == 1 && pm == 0;
                if (!bs && !ms) continue;
                var mem = Members(w, g); if (mem.Count == 0) continue;
                float rng = 0; foreach (var u in mem) if (!u.IsOfficer && u.Weapon != null && u.Role is UnitRole.Rifle or UnitRole.Manoeuvre or UnitRole.None) rng = MathF.Max(rng, u.Weapon.Range);
                if (rng <= 0) foreach (var u in mem) if (!u.IsOfficer && u.Weapon != null) rng = MathF.Max(rng, u.Weapon.Range);
                if (rng <= 0) rng = 16f;
                var cen = new Vector2(mem.Average(u => u.Pos.X), mem.Average(u => u.Pos.Y));
                var (es, ec) = VisEnemyAround(w, f, cen, rng + R.SuperiorityRadius);
                double age = dr.Phase == DrillPhase.Engage ? (w.Tick - dr.PhaseTick) * Config.SimDt : 0;
                bool sup = ec > 0 && es >= R.SuperioritySupp;
                bool odds = ec > 0 && ec <= R.SuperiorityFew && mem.Count >= R.SuperiorityOdds * ec && age > R.SuperiorityDelaySec;
                string cls = sup ? "신호" : odds ? "수우세" : age > R.PatienceSec ? "타이머" : "그외";
                string k = (bs ? "약진" : "기동조") + ":" + cls;
                x.TickStart[k] = x.TickStart.GetValueOrDefault(k) + 1;
            }
        }
    }
    internal static void ApplyVariant(Defs d, string v)
    {
        static float? E(string n) => float.TryParse(Environment.GetEnvironmentVariable(n), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : null;
        if (E("KD_OBS_OFFBACK") is { } ob) d.CombatRules.OfficerBack = ob;
        if (E("KD_OBS_OFFSPREAD") is { } os) d.CombatRules.OfficerSpread = os;
        if (E("KD_OBS_NCOH") is { } nh) d.CombatRules.NcoHesitateMul = nh;
        if (E("KD_OBS_NCOD") is { } nd) d.CombatRules.NcoDecayMul = nd;
        switch (v)
        {
            case "V1": d.Tactics.Behaviour.SuperioritySupp = 0.30f; break;
            case "V2": d.Tactics.Behaviour.SuperioritySupp = 0.15f; break;
            case "V3": d.Tactics.Behaviour.SuperioritySupp = 0.10f; break;
            case "V4": d.CombatRules.SuppressDecay = 35f; break;
            case "V6": d.Tactics.Behaviour.SuperioritySupp = 0.15f; d.CombatRules.SuppressDecay = 35f; break;
            case "V7": d.Tactics.Behaviour.SuperioritySupp = 0.20f; d.CombatRules.SuppressDecay = 50f; break;
            case "G2HI": d.CombatRules.CloseHoleFrac = 0.65f; d.CombatRules.CloseHoleGap = 8; break;   // gapfill2 민감도: 앞줄 구멍 문턱을 높임 (명령 줄이기)
            case "NOCLOSE": d.CombatRules.CloseOnFrac = 99f; d.CombatRules.CloseHoles = d.CombatRules.CloseHoleGap = 1 << 20; d.CombatRules.CloseHoleFrac = 0; d.CombatRules.CloseBehindMen = 0; d.CombatRules.CloseChargeRange = 0; break;   // gapfill 기준선: 장교 판단 끔 (= N12 동작, 대열 지표만 잼)
            case "NoNco": d.CombatRules.SquadLeaderRank = 0; break;
            case "OFFOLD": d.CombatRules.OfficerAvoid = false; d.CombatRules.ScoutAvoid = false; break;   // N35 기준선: 장교 교전 회피 끔 (옛 동작)                 // 9차 분리: 부사관 기본 편성 끔
            case "NoTA": d.Tactics.TrenchAssault.AiEnabled = false; break;   // N32 기준선: 전술 AI 참호 돌격 끔 (같은 빌드 끔 대 켬)
            case "TA12": d.Tactics.TrenchAssault.BoundEndDist = 12f; d.Tactics.TrenchAssault.FarDist = 40f; break;   // N32 민감도: 약진을 사격 거리 안 (사거리 24) 까지, 집결 40칸
            case "TA8": d.Tactics.TrenchAssault.BoundEndDist = 8f; d.Tactics.TrenchAssault.FarDist = 40f; break;
            case "TA30": d.Tactics.TrenchAssault.BoundEndDist = 30f; d.Tactics.TrenchAssault.FarDist = 60f; break;   // 사양 원문 값 (참고)
            case "TA18": d.Tactics.TrenchAssault.BoundEndDist = 18f; d.Tactics.TrenchAssault.FarDist = 45f; break;
            case "NoGate": d.Tactics.Behaviour.AssaultGate = false; break;
            case "OLD": d.Tactics.Pursuit.NoChargeFleeing = false; d.Tactics.Pursuit.Enabled = false; d.Tactics.Pursuit.CavalryChargeFleeing = true; d.Tactics.Cavalry.Legacy = true; break;   // 12차 기준선: pursuit 전 동작 (돌격 금지 · 사격 추격 · 새 기병 AI 끔)           // 9차 분리: 돌격 허가 끔 (옛 조건)
            case "P1": d.Tactics.Pursuit.RetreatFire = false; d.Tactics.Regroup.Enabled = false; d.CombatRules.ArtilleryFriendlyFire = false; d.Tactics.Cavalry.FlankOddsMul = 0.5f; d.Tactics.Cavalry.FlankMinSupp = 0; d.Tactics.Cavalry.FormedMinN = 0; break;   // 13차 기준선: N22 (pursuit) 동작 — 후퇴 사격 · 재정비 · 포병 오사 끔, 기병 옛 수치
            // engage (2026-10-19) 민감도: ENG<백분율>[F] = combat.engageRangeFrac, F = 전술 AI engageFrac 도 같은 비율로 (프로필 값 × frac / 기준). OLDDEF = 방어 지형 활용 끔
            case var e when e.StartsWith("ENG"):
            {
                string num = e[3..].TrimEnd('F', 'O');
                float f = float.Parse(num, CultureInfo.InvariantCulture) / 100f;
                float baseF = d.CombatRules.EngageRangeFrac;
                d.CombatRules.EngageRangeFrac = f;
                if (e.Contains('F')) AiProfiles.Tweak(p => p.Tactics.EngageFrac *= f / MathF.Max(0.01f, baseF));
                if (e.EndsWith("O")) AiProfiles.Tweak(p => { p.Tactics.DefendCover = false; p.Tactics.DefendSpacing = 0; });
                break;
            }
            case "NOFORT": AiProfiles.Tweak(p => p.Tactics.TrenchSearch = -1); break;   // 진지 점유 끔 (옛 LineDefend 깃발 경로)
            case "FMOFF": d.CombatRules.FormRelax = false; break;             // 대형 완화 끔 (formation 2026-10-21)
            case "LWOFF": d.Formations.LineShape = ""; break;                 // 횡대 산개 파상 끔 (옛 2열)
            case "RTOL0": d.CombatRules.RelaxMoveTolerance = 0.5f; break;     // 대형 완화 부분 끔: 행군 오차 옛 0.5
            case "RDB0": d.CombatRules.FormDeadband = 0; break;               // 데드밴드 끔
            case "RSH0": d.CombatRules.ShortMoveDist = 0; break;              // 평행이동 끔
            case "RAZ0": d.CombatRules.FormArriveZone = 0; break;             // 구역 도착 끔
            case "RRA0": d.CombatRules.RestAlignSec = 0; break;               // 휴식 정렬 끔
            case "RST0": d.CombatRules.FormStandHold = false; break;          // 깃발 정지 중 제자리 서기 끔
            case "RNX": d.CombatRules.NarrowExitFrac = d.CombatRules.NarrowBlockFrac; break;   // 종대 히스테리시스 끔
            case "LWON": d.Formations.LineShape = "line_waves"; break;        // 횡대 산개 파상 켬
            case "LWX":   // 산개 파상 수치 시험: KD_OBS_LW = "간격,줄사이,최대열"
            {
                var parts = (Environment.GetEnvironmentVariable("KD_OBS_LW") ?? "").Split(',');
                if (parts.Length == 3 && d.Formations.Company.TryGetValue("line_waves", out var lw))
                { lw.Spacing = float.Parse(parts[0], CultureInfo.InvariantCulture); lw.RankGap = float.Parse(parts[1], CultureInfo.InvariantCulture); lw.MaxCols = int.Parse(parts[2]); }
                break;
            }
            case "OLDFORM": d.CombatRules.FormRelax = false; d.Formations.LineShape = ""; break;   // 둘 다 끔 = main
            case "CHOKE": d.CombatRules.ChokeEnabled = true; break;           // 길목 통과 큐 켬
            case "F1OFF": d.CombatRules.ContactLoose = false; break;          // F1 끔 (접적 상태만 셈)
            case "F1CHASE": d.CombatRules.ContactHoldChase = false; break;    // F1 중 대기 병사 추격 제한 끔
            case "F1ARR": d.CombatRules.ContactArriveZone = false; break;     // F1 중 도착 깃발 구역 도착 끔
            case "F1ATK": d.CombatRules.SpotAttackCover = true; break;        // F1 공격 자세도 엄폐 칸으로
            case "F1SPOT": d.CombatRules.SpotEnabled = false; break;          // F1 중 구역 · 사격 위치 끔
            case "F2OFF": d.CombatRules.RotateInPlace = false; d.CombatRules.LooseAdvance = false; d.CombatRules.AiContactReorderDist = 0; d.CombatRules.AiContactReorderSec = 0; break;   // F1 2차 전부 끔 (= F1 1차)
            case "F2ROT0": d.CombatRules.RotateInPlace = false; break;        // F1 2차 제자리 방향 전환 끔
            case "F2LOOSE0": d.CombatRules.LooseAdvance = false; break;       // F1 2차 선까지 전진 끔
            case "F2AIL0": d.CombatRules.LooseAdvanceAi = false; break;      // F1 2차 선까지 전진은 플레이어만 (기본)
            case "F2AIL1": d.CombatRules.LooseAdvanceAi = true; break;       // AI 에도 선까지 전진
            case "F2RE0": d.CombatRules.AiContactReorderDist = 0; d.CombatRules.AiContactReorderSec = 0; break;   // F1 2차 AI 재명령 제한 끔
            case "F2TPL0": d.CombatRules.AiContactTemplates = false; break;   // 접적 뒤 화력-기동 틀 끔 (틀은 접근만)
            case "F2TPL1": d.CombatRules.AiContactTemplates = true; break;
            case "NOSEEK": d.CombatRules.CoverSeekRadius = 0; break;                    // 엄폐 찾기 끔
            case "SPACE0": AiProfiles.Tweak(p => p.Tactics.DefendSpacing = 0); break;   // 방어 정면 상한 끔
            case "OLDDEF": AiProfiles.Tweak(p => { p.Tactics.DefendCover = false; p.Tactics.DefendSpacing = 0; }); break;
            case "V5": foreach (var k in new[] { "rifle", "rifle_long" }) if (d.Weapons.TryGetValue(k, out var wd)) wd.Suppress *= 2f; break;
        }
    }

    static readonly int TacDbgEvery = Environment.GetEnvironmentVariable("KD_TAC_DBG") == "1"
        ? (int.TryParse(Environment.GetEnvironmentVariable("KD_TAC_DBG_SEC"), out int ds) && ds > 0 ? ds : 15) : 0;

    public static int RunCli(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        var defs = Defs.Load();
        string variant = Environment.GetEnvironmentVariable("KD_OBS_VARIANT") ?? "";   // 4차: 시험용 메모리 안 덮어쓰기 (V0..V5), 파일 · 정상 게임은 영향 없음
        foreach (var part in variant.Split('+', StringSplitOptions.RemoveEmptyEntries)) ApplyVariant(defs, part);   // engage: '+' 로 여러 덮어쓰기
        if (variant.Length == 0) ApplyVariant(defs, "");   // offnco: 변형 없어도 KD_OBS_OFFBACK 등 덮어쓰기는 적용
        SupThr = defs.Tactics.Behaviour.SuperioritySupp;
        PatThr = defs.Tactics.Behaviour.PatienceSec;
        string id = args[1]; float seconds = 0; int? seed = null; string? outPath = null;
        bool tacDbg = Environment.GetEnvironmentVariable("KD_TAC_DBG") == "1";
        if (tacDbg) TacticalAi.PlanTrace = l => Console.Error.WriteLine(l);
        for (int i = 2; i < args.Length; i++)
            switch (args[i])
            {
                case "--seconds": seconds = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seed": seed = int.Parse(args[++i]); break;
                case "--out": outPath = args[++i]; break;
            }
        var def = File.Exists(id) ? MissionDef.Parse(File.ReadAllText(id)) : MissionDef.Load(id);
        if (seconds <= 0) seconds = (KoreanDivision.UI.MissionText.TimeLimit(def) is float tl and > 0 ? tl : 1800) + 30;
        AmmoObs.On = true;   // 읽기 전용 탄약 사용량 측정
        var w = World.CreateMissionWorld(def, defs, seed, new List<DeployEntry>());
        F1Obs.Reset(); HeavyObs.Reset();
        World.AttachMissionAi(w, def.Player, true);
        if (ArtyFx.On) ArtyFx.Init(w);
        if (BnCamp.On) BnCamp.Init(w);   // 대대 캠페인 측정기 (읽기 전용, tools/bn_campaign)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long max = (long)(seconds / Config.SimDt);
        int every = (int)MathF.Round(1f / Config.SimDt);
        var sides = new Dictionary<int, Side>();
        var gs = new Dictionary<(int, int), GState>();
        var prevCnt = new Dictionary<int, int[]>();
        var pend = new List<PendCav>();
        var startAlive = new Dictionary<int, int>();
        var offAll = new List<Unit>(); var offDist = new List<double>(); var offFar = 0;   // N35: 시작 장교 · 1초마다 장교 ↔ 중대 무게중심 거리 표본
        bool killDone = false; double killT = -1; string killLog = "";
        double t50 = -1; long kf50 = 0, km50 = 0;   // 12차: 한쪽 50% 미만 (추격 국면 시작) 시각 · 그때까지 사살 (사격 / 근접)
        string[] cname = { "Orders", "Charges", "CavCharges", "TrenchMoves", "Fallbacks", "ArtMoves", "FireHolds", "ReserveCommits", "Preps", "Lifts", "SquadBounds", "Outposts", "OutpostWithdraw", "Counterattacks", "Reinforced", "Culminations", "AmmoRot", "PlugGap", "PlugBreach", "PlugFlank", "ResRedesig", "FixPush" };
        while (w.Winner == 0 && w.Tick < max)
        {
            if (BnCamp.On) BnCamp.BeforeStep();
            w.Step();
            if (NoFireProbe.On && w.Tick % every == 0) NoFireProbe.Tick(w);
            if (BnCamp.On) BnCamp.Tick(w);
            if (tacDbg && w.Tick % 300 == 0)   // KD_TAC_DBG=1: 5초마다 전술 AI 상태 (stage 3A 계획 · 제대 확인용, 읽기 전용)
                foreach (var (f, ai) in w.Controllers) if (ai is TacticalAi ta && ta.StateOf(f) is { } ts) Console.Error.WriteLine($"  t={w.Tick / 60} F{f} " + TacticalAi.Describe(ts));
            if (ArtyFx.On) ArtyFx.Tick(w);
            TickStarts(w, sides);
            if (w.Tick == 2 && Environment.GetEnvironmentVariable("KD_OBS_NCO") == "1")
                foreach (var co in w.Companies) if (co.Owner == int.Parse(Environment.GetEnvironmentVariable("KD_OBS_KILLSIDE") ?? "1")) foreach (var q in co.Squads) if (q.Leader is { IsOfficer: false } l) l.GrantKills(30);
            KillHarness(w, sides, ref killDone, ref killT, ref killLog);
            if (w.Tick % every != 0) continue;
            HeavyObs.Sample(w); F1Obs.Sample(w);   // F1 접적 지표 (읽기 전용)
            double t = w.Time;
            if (offAll.Count == 0) foreach (var u in w.Units) if (u.IsOfficer && u.Alive) offAll.Add(u);
            foreach (var u in w.Units) if (u.IsOfficer && u.Alive && u.Company != null && w.OfficerBodyDist(u) is float od and >= 0) { offDist.Add(od); if (od > 8) offFar++; }
            if (TacDbgEvery > 0 && w.Tick % (every * TacDbgEvery) == 0)   // KD_TAC_DBG=1 (간격 KD_TAC_DBG_SEC, 기본 15초): 전술 AI 층 상태 한 줄 (읽기 전용)
                foreach (var (f, ai) in w.Controllers)
                    if (ai is TacticalAi ta0 && ta0.StateOf(f) is { } ts0)
                    {
                        Console.WriteLine($"   tac{f} t={t:0} alive={w.Units.Count(u => u.Owner == f && u.Alive)} " + TacticalAi.Describe(ts0));
                        foreach (var g in ts0.Groups)   // 도하 중 · 교두보 중대: 병사 위치 (y 4칸 띠별 수, 평균 제압)
                            if (g.CrossStage != 0 && g.CompanyId != 0 && w.FindCompany(g.CompanyId) is { } co0)
                            {
                                var al = co0.Members.Where(u => u.Alive).ToList();
                                Console.WriteLine($"      L{g.Id} 도{g.CrossStage}: " + string.Join(" ", al.GroupBy(u => (int)(u.Pos.Y / 4) * 4).OrderBy(k => k.Key).Select(k => $"y{k.Key}:{k.Count()}"))
                                    + $" supp {(al.Count > 0 ? al.Average(u => u.SuppressionRatio) : 0):0.00} x {(al.Count > 0 ? al.Min(u => u.Pos.X) : 0):0}-{(al.Count > 0 ? al.Max(u => u.Pos.X) : 0):0} 표적 {al.Count(u => u.Target != null)} 최근사격 {al.Count(u => u.LastShotTick > 0 && w.Tick - u.LastShotTick < 180)} 목표 ({g.CrossPoint.X:0},{g.CrossPoint.Y:0}) {g.State}");
                            }
                    }
            if (t50 < 0 && startAlive.Count >= 2 && startAlive.Any(kv => w.Units.Count(u => u.Owner == kv.Key && u.Alive) * 2 < kv.Value)) { t50 = t; kf50 = w.Stats.KillsFire; km50 = w.Stats.KillsMelee; }
            foreach (var (f, ai) in w.Controllers)
            {
                if (ai is not TacticalAi ta || ta.StateOf(f) is not { } s || s.Phase < 2) continue;
                if (!sides.TryGetValue(f, out var sd)) { sides[f] = sd = new Side(); foreach (var n in cname) sd.Counters[n] = new List<double>(); startAlive[f] = w.Units.Count(u => u.Owner == f && u.Alive); }
                Sample(w, ta, s, sd, gs, pend, t);
                var c = new[] { s.Orders, s.Charges, s.CavalryCharges, s.TrenchMoves, s.Fallbacks, s.ArtilleryMoves, s.FireHolds, s.ReserveCommits, s.Preps, s.Lifts, s.SquadBounds, s.Outposts, s.OutpostWithdrawals, s.Counterattacks, s.Reinforced, s.Culminations, s.AmmoRotations, s.PlugGaps, s.PlugBreaches, s.PlugFlanks, s.ReserveRedesignations, s.FixPushes };
                var p = prevCnt.GetValueOrDefault(f) ?? new int[c.Length];
                for (int i = 0; i < c.Length; i++) for (int k = p[i]; k < c[i]; k++) sd.Counters[cname[i]].Add(t);
                prevCnt[f] = c;
                if (w.Tick % (every * 30) == 0) sd.Curve.Add((t, w.Units.Count(u => u.Owner == f && u.Alive)));
            }
            for (int i = pend.Count - 1; i >= 0; i--)
                {
                    var pc = pend[i];
                    int cnt = 0; foreach (var u in w.Units) if (u.Owner != pc.F && u.Alive && u.Def.IsInfantry && Vector2.Distance(u.Pos, pc.C) <= 6 && u.SuppressionRatio >= 0.25f) cnt++;
                    pc.Peak = Math.Max(pc.Peak, cnt);
                    if (t - pc.T0 >= 8 && sides.TryGetValue(pc.F, out var sd2))
                    {
                        sd2.Cav.Add($"t={pc.T0:0}s 기병 돌격 #{pc.Id} (기수 {pc.Riders}): 반경 6 적 보병 중 제압>=0.25 {pc.Base}기 -> 8초 안 최대 {pc.Peak}기");
                        pend.RemoveAt(i);
                    }
                }
        }
        var res = MissionResults.From(w);
        var sb = new StringBuilder();
        sb.AppendLine($"VARIANT {(variant == "" ? "V0" : variant)} thr {SupThr}");
        sb.AppendLine($"# {def.Id} seed {seed?.ToString() ?? "기본"}: {res.Result} at {res.DurationSeconds:0.0}s (wall {sw.Elapsed.TotalSeconds:0}s)");
        foreach (var fl in res.Factions) sb.AppendLine($"faction {fl.Id} {fl.Name}: spawned {fl.Spawned}, lost {fl.Lost}, alive {fl.Alive}");
        sb.AppendLine($"[대열 정비] 장교 명령 {w.Stats.CloseOrders}, 플레이어 명령 {w.Stats.CloseManual}, 슬롯으로 보낸 병사 {w.Stats.CloseMovers}, 교전 중이라 미룸 {w.Stats.CloseLate}, 빈자리 점검 재배치 {w.Stats.GapRefills}, 공격 이동 그대로 보냄 {w.Stats.CloseMoveAttack}, 돌격 감지 판단 {w.Stats.CloseChargeSeen}, 돌격 감지 명령 {w.Stats.CloseChargeOrders}");
        sb.AppendLine(F1Obs.Report(w));
        sb.AppendLine($"[F6소품] 소품 {w.Props.Count}개, 부서짐 {w.Props.Destroyed}, 보병 표적 사격 {w.Stats.PropShots} 중 소품 엄폐 {w.Stats.PropCoverShots} ({(w.Stats.PropShots > 0 ? 100.0 * w.Stats.PropCoverShots / w.Stats.PropShots : 0):0.0}%)");
        foreach (var (f, sd) in sides.OrderBy(k => k.Key)) Print(sb, f, sd);
        foreach (var f in sides.Keys.OrderBy(k => k).Where(k => (uint)k < MoveStats.MaxF))   // [제압2] supp 2026-10-20 (읽기 전용): 엎드림 · 고정 · 붕괴 병-초, 붕괴 · 회복 수, 받은 제압 출처 (천 점)
        {
            var st = w.Stats; double ds = Config.SimDt;
            sb.AppendLine($"[제압2] 세력{f} 엎드림 {st.ProneTicks[f] * ds:0}병-초 고정 {st.PinTicksF[f] * ds:0}병-초 붕괴 {st.Breaks[f]} 회복 {st.Rallies[f]} 도주 {st.BrokenTicks[f] * ds:0}병-초 | 출처 " + string.Join(" ", Enumerable.Range(0, SupSrc.Count).Select(i => $"{SupSrc.Names[i]} {st.SuppBy[f * SupSrc.Count + i] / 1000.0:0.0}")));
        }
        foreach (var f in sides.Keys.OrderBy(k => k).Where(k => (uint)k < MoveStats.MaxF)) sb.AppendLine($"[항복] 세력{f} 항복 {w.Stats.Surrenders[f]}");   // N79b
        sb.AppendLine($"[돌격허가] 신호 {w.Stats.AssaultSignal} 엄호 {w.Stats.AssaultOverwatch} 예비타이머 {w.Stats.AssaultFallback} 막힘(판단수) {w.Stats.AssaultBlocked}");   // 결정 4 (양측 합, 엔진 기동조 + 전술 AI 중대 돌격)
        { var st = w.Stats; double end = res.DurationSeconds; var ps = sides.ToDictionary(k => k.Key, k => w.Controllers.Select(c => c.Ai).OfType<TacticalAi>().Select(a => a.StateOf(k.Key)).FirstOrDefault(x => x != null));
          sb.AppendLine($"[추격] 국면 시작 t50={(t50 < 0 ? -1 : t50):0} 길이 {(t50 < 0 ? 0 : end - t50):0}s, 사살 사격 {st.KillsFire} 근접 {st.KillsMelee}, 추격 국면 사살 사격 {(t50 < 0 ? 0 : st.KillsFire - kf50)} 근접 {(t50 < 0 ? 0 : st.KillsMelee - km50)}, 후퇴 중 희생 사격 {st.KillsFleeingFire} 근접 {st.KillsFleeingMelee}, 후퇴하는 적 돌격 보병 {st.InfChargeFleeing} 기병 {st.CavChargeFleeing}, 막은 판단 {st.FleeingChargeBlocked}, AI 추격 {string.Join("/", ps.OrderBy(k => k.Key).Select(k => k.Value?.Pursuits ?? 0))} 한계 정지 {string.Join("/", ps.OrderBy(k => k.Key).Select(k => k.Value?.PursuitHalts ?? 0))}");
          sb.AppendLine($"[F4기병] 돌격 개시 {st.CavChargeStarts} 근접 사살 {st.CavMeleeKills} 무의미 정면 {st.CavChargePointless} 자동 막음 {st.CavAutoBlocked} 꺾인 {st.ChargesBroken} 사유(후퇴/제압/승무원/고립/측면) {string.Join("/", st.CavWhy)}");
          sb.AppendLine($"[기병AI] 돌격 정면 {st.CavChargeFront} 측면 {st.CavChargeFlank} 후방 {st.CavChargeRear}, 측면 경유 {st.CavFlankRoutes}, 소규모 보류 {st.CavSmallHeld}, 교착 물러남 {st.CavScreens}, 장교 뒤 따라감 {st.CavOfficerHeldBack}, 무모한 정면 (N55) {st.CavRashFront}");
          sb.AppendLine($"[N32 참호돌격] 시작 {st.TaStarts} (AI {st.TaAiStarts}) 성공 {st.TaDone} 실패 {st.TaFail} 취소 {st.TaCancel} | 단계 진입 제압 {st.TaStageEntered[1]} 접근 {st.TaStageEntered[2]} 돌파 {st.TaStageEntered[3]} 소탕 {st.TaStageEntered[4]} 확보 {st.TaStageEntered[5]} | 약진 {st.TaBounds} 수류탄 {st.TaGrenades} 돌격 {st.TaCharges} | 취소 원인 {(st.TaCancelWhy.Count == 0 ? "없음" : string.Join(", ", st.TaCancelWhy.Select(kv => kv.Key + " " + kv.Value)))}");
          sb.AppendLine($"[N23/N55] 돌격보병 소탕 명령 {st.DrillSweeps}, 돌격 사격 전진 (경험 적은 지휘관) {st.DrillMarchAdvances}, 분대 교대 약진 {st.DrillBounds}"); }
        { var ps2 = sides.Keys.OrderBy(k => k).Select(k => w.Controllers.Select(c => c.Ai).OfType<TacticalAi>().Select(a => a.StateOf(k)).FirstOrDefault(x => x != null)).ToList();
          sb.AppendLine($"[후퇴사격] 발수 {w.Stats.RetreatShots} 사살 {w.Stats.KillsRetreatFire} | [재정비] 시작 {string.Join("/", ps2.Select(x => x?.Regroups ?? 0))} 사수판단 {string.Join("/", ps2.Select(x => x?.RegroupHolds ?? 0))} 재전진 {string.Join("/", ps2.Select(x => x?.Readvances ?? 0))} 후퇴 {string.Join("/", ps2.Select(x => x?.Fallbacks ?? 0))}"); }
        sb.AppendLine("[아군오사] " + (w.Stats.FriendlyFire.Count == 0 ? "없음" : string.Join(", ", w.Stats.FriendlyFire.Select(kv => $"{kv.Key} 명중 {kv.Value.Hits} 사살 {kv.Value.Kills} 피해 {kv.Value.Damage:0.0}")) + $" | 합계 명중 {w.Stats.FriendlyFire.Values.Sum(v => v.Hits)} 사살 {w.Stats.FriendlyFire.Values.Sum(v => v.Kills)}"));
        { var st = w.Stats; offDist.Sort();
          string L(SortedDictionary<string, MoveStats.OfficerLine> d) => d.Count == 0 ? "없음" : string.Join(", ", d.Select(kv => $"{kv.Key} {kv.Value.N}(나쁨 {kv.Value.Bad}, 중대거리 {kv.Value.BodyDist / kv.Value.N:0.0} 최대 {kv.Value.BodyMax:0.0}, 적거리 {kv.Value.EnemyDist / kv.Value.N:0.0})"));
          sb.AppendLine($"[장교교전] 돌격 개시 {L(st.OffCharge)} | 혼자 돌격 {st.OffCharge.Values.Sum(v => v.Bad)}, 막은 명령 돌격 {st.OfficerChargeBlocked}, 중대 돌격 끝나 그침 {st.OfficerChargeEnded}, 돌격 중 앞줄 대기 틱 {st.OfficerChargeHeld}");
          sb.AppendLine($"[장교사격] {L(st.OffShot)} | 합계 {st.OffShot.Values.Sum(v => v.N)}, 자위 밖 {st.OffShot.Values.Sum(v => v.Bad)}");
          sb.AppendLine($"[정찰사격] {L(st.ScoutShot)} | 합계 {st.ScoutShot.Values.Sum(v => v.N)}, 자위 밖 {st.ScoutShot.Values.Sum(v => v.Bad)}");
          sb.AppendLine($"[장교위치] 시작 장교 {offAll.Count}, 전사 {offAll.Count(u => !u.Alive)} ({string.Join("/", offAll.GroupBy(u => u.Owner).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count(u => !u.Alive)}/{g.Count()}"))}), 중대 무게중심 거리 표본 {offDist.Count}: 평균 {(offDist.Count > 0 ? offDist.Average() : 0):0.0} 중앙 {(offDist.Count > 0 ? offDist[offDist.Count / 2] : 0):0.0} p90 {(offDist.Count > 0 ? offDist[offDist.Count * 9 / 10] : 0):0.0} 최대 {(offDist.Count > 0 ? offDist[^1] : 0):0.0}, 8칸 넘음 {offFar}"); }
        sb.Append(AmmoObs.Report(w)); sb.Append(HeavyObs.Report(w));
        sb.AppendLine($"checksum {w.ComputeChecksum():X16} tick {w.Tick}");
        Console.Write(sb.ToString());
        if (ArtyFx.On) ArtyFx.Report(w);
        if (NoFireProbe.On) NoFireProbe.Report($"{def.Id} seed {seed?.ToString() ?? "기본"}");
        if (BnCamp.On) BnCamp.Finish(w, Environment.GetEnvironmentVariable("KD_BN_OUT")!, def.Id, seed, res.Result.ToString(), res.DurationSeconds);
        if (outPath != null) File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        return 0;
    }

    /// <summary>7차: 장교 상실 시험 (도구 전용). KD_OBS_KILL=all|cmd, KD_OBS_KILLSIDE=1|2 (기본 1), KD_OBS_KILLDELAY=30. 첫 교전 + delay 초에 중대 장교 전부(all) 또는 중대 지휘관만(cmd) 를 World.Damage 로 사살.</summary>
    static void KillHarness(World w, Dictionary<int, Side> sides, ref bool done, ref double t0, ref string log)
    {
        string? mode = Environment.GetEnvironmentVariable("KD_OBS_KILL");
        if (string.IsNullOrEmpty(mode) || done) return;
        int side = int.Parse(Environment.GetEnvironmentVariable("KD_OBS_KILLSIDE") ?? "1");
        double delay = double.Parse(Environment.GetEnvironmentVariable("KD_OBS_KILLDELAY") ?? "30", CultureInfo.InvariantCulture);
        if (t0 < 0) { foreach (var sd in sides.Values) if (sd.FirstEngage >= 0) { t0 = sd.FirstEngage; break; } if (t0 < 0) return; }
        if (w.Time < t0 + delay) return;
        done = true; int n = 0, nc = 0;
        var victims = new List<Unit>();
        foreach (var co in w.Companies)
        {
            if (co.Owner != side) continue;
            var mem = co.Members.Where(u => u.Alive && u.Owner == side).ToList();
            if (mem.Count(u => !u.IsOfficer && u.Def.IsInfantry) < 20) continue;   // 소총 중대만 (기관총 중대 · 관측반 제외)
            nc++;
            if (mode == "all") victims.AddRange(mem.Where(u => u.IsOfficer));
            else if (co.Commander is { Alive: true } c) victims.Add(c);
        }
        foreach (var v in victims.Distinct()) { w.Damage(v, v.Hp + 1000f, null); n++; }
        log = $"t={w.Time:0} 장교 상실 시험 ({mode}, 세력 {side}): 소총 중대 {nc}개, 사살 {n}명 (첫 교전 {t0:0}s + {delay:0}s)";
        foreach (var sd in sides.Values) sd.Log.Add(log);
    }

    static List<Unit> Members(World w, TacGroup g)
    {
        if (g.CompanyId > 0 && w.FindCompany(g.CompanyId) is { } co) return co.Members.Where(u => u.Alive).ToList();
        var l = new List<Unit>();
        foreach (var id in g.Units) if (w.Find(id) is Unit u) l.Add(u);
        return l;
    }

    static float NearestFoe(World w, int f, Vector2 c)
    {
        float b = 999;
        foreach (var u in w.Units) if (u.Owner != f && u.Alive && u.Weapon != null) b = MathF.Min(b, Vector2.Distance(u.Pos, c));
        return b;
    }

    static (double avg, int n, double engAvg, int engN, double engMax) EnemySupX(World w, int f, Vector2 c, float r)
    {
        double sum = 0, es = 0, em = 0; int n = 0, en = 0; long win = (long)(5f / Config.SimDt);
        foreach (var u in w.Units)
            if (u.Owner != f && u.Alive && u.Weapon != null && Vector2.Distance(u.Pos, c) <= r)
            {
                sum += u.SuppressionRatio; n++;
                if (w.Tick - u.LastFiredAtTick < win) { es += u.SuppressionRatio; en++; em = Math.Max(em, u.SuppressionRatio); }
            }
        return (n > 0 ? sum / n : 0, n, en > 0 ? es / en : 0, en, em);
    }

    static (double avg, int n) EnemySup(World w, int f, Vector2 c, float r)
    {
        double sum = 0; int n = 0;
        foreach (var u in w.Units)
            if (u.Owner != f && u.Alive && u.Weapon != null && Vector2.Distance(u.Pos, c) <= r) { sum += u.SuppressionRatio; n++; }
        return (n > 0 ? sum / n : 0, n);
    }

    static void Sample(World w, TacticalAi ta, TacFaction s, Side sd, Dictionary<(int, int), GState> gs, List<PendCav> pend, double t)
    {
        int f = s.Faction;
        {   // S3: 관측반 (TacKind.Scout 팀) 유닛별 사망 시각 · 사망 때 가장 가까운 적 거리 (읽기만, 시뮬 영향 없음)
            var seen = new HashSet<int>();
            foreach (var g in s.Groups)
            {
                if (g.Kind != TacKind.Scout) continue;
                foreach (var u in Members(w, g))
                {
                    if (!u.Alive) continue;
                    double nd = double.MaxValue;
                    foreach (var e in w.Units) if (e.Owner != f && e.Alive) nd = Math.Min(nd, Vector2.Distance(e.Pos, u.Pos));
                    sd.SpotLast[u.Id] = (g.Id, nd); seen.Add(u.Id);
                    (sd.SpotTeam.TryGetValue(g.Id, out var hs) ? hs : sd.SpotTeam[g.Id] = new HashSet<int>()).Add(u.Id);
                }
            }
            foreach (var (uid, v) in sd.SpotLast.ToList())
                if (!seen.Contains(uid) && !(w.Find(uid) is { Alive: true })) { sd.SpotDeaths.Add((t, v.gid, uid, v.dist)); sd.SpotLast.Remove(uid); }
        }
        float rr = MathF.Max(8, s.RifleRange);
        // 전선 축: 우리 → 적 무게중심
        var mine = w.Units.Where(u => u.Owner == f && u.Alive && u.Weapon != null).ToList();
        var foes = w.Units.Where(u => u.Owner != f && u.Alive && u.Weapon != null).ToList();
        if (mine.Count == 0 || foes.Count == 0) return;
        var cm = new Vector2(mine.Average(u => u.Pos.X), mine.Average(u => u.Pos.Y));
        var cf = new Vector2(foes.Average(u => u.Pos.X), foes.Average(u => u.Pos.Y));
        var axis = Vector2.Normalize(cf - cm);
        // 소총 전선 (소총 중대 구성원 90% 전진 분위)
        var rifles = new List<float>();
        foreach (var g in s.Groups) if (g.Kind == TacKind.Line && g.Alive > 0) foreach (var u in Members(w, g)) if (u.Def.IsInfantry && !u.Def.Scout && !u.IsOfficer) rifles.Add(Vector2.Dot(u.Pos, axis));
        rifles.Sort();
        float front = rifles.Count > 10 ? rifles[(int)(rifles.Count * 0.9f)] : float.MaxValue;
        foreach (var u in mine) if (u.Def.IsInfantry && u.Target is { Alive: true } tg && w.Tick - u.LastShotTick < 30) { float dd = Vector2.Distance(u.Pos, tg.Pos); sd.RngSum += dd; sd.RngN++; sd.Rng.Add(dd); }
        foreach (var co in w.Companies)   // gapfill: 교전 중 (구성원 표적) · 정지 중대의 대열 흐트러짐 (읽기만)
        {
            if (co.Owner != f || co.Members.Count < 4 || co.Marching || co.Facing == Vector2.Zero || !co.Members.Exists(u => u.Target != null)) continue;
            var rd = w.MeasureRanks(co);
            if (rd.Men < 4) continue;
            sd.RankDisp.Add(rd.DispFrac); sd.RankFront.Add(rd.FrontEmpty); sd.RankGap.Add(rd.FrontGap);
            sd.RankHoles.Add(rd.Holes); sd.RankHoleGap.Add(rd.HoleGap); sd.RankBehind.Add(rd.Behind); sd.RankSlots.Add(rd.FrontSlots);
            if (w.RanksBroken(rd, 1f)) sd.RankBroken++;
            if (co.CloseAlarm) sd.RankAlarmSec++;
            void D(string k) => sd.RankDiag[k] = sd.RankDiag.GetValueOrDefault(k) + 1;
            D(co.Anchor == null ? "co:anchorNone" : co.Anchor.Arrived ? "co:anchorArrived" : "co:anchorMarch");
            if (co.Drill.Phase != DrillPhase.Idle) D("co:drill");
            foreach (var u in co.Members)
            {
                if (u.IsOfficer) continue;
                string g = u.Group == null ? "noG" : co.Anchor != null && ReferenceEquals(co.Anchor.Group, u.Group) ? "anchG" : "othG";
                D($"u:{u.Order}/{g}" + (u.Charging ? "/chg" : "") + (u.InTrench || u.HasTrenchSpot ? "/tr" : "") + (u.Def.Heavy ? "/crew" : ""));
            }
        }
        foreach (var co in w.Companies)   // offnco: 장교 위치 (교전 중 정지 중대, 읽기만)
        {
            if (co.Owner != f || co.Members.Count < 4 || co.Marching || co.Facing == Vector2.Zero || !co.Members.Exists(u => u.Target != null)) continue;
            Unit? off = null; foreach (var u in co.Members) if (u.Alive && u.IsOfficer) { off = u; break; }
            if (off == null) continue;
            var men = co.Members.Where(u => u.Alive && !u.IsOfficer && u.Def.IsInfantry).ToList(); if (men.Count < 4) continue;
            var dots = men.Select(u => Vector2.Dot(u.Pos, co.Facing)).OrderBy(q => q).ToList(); float fm = dots[(int)(dots.Count * 0.95f)];   // 최전열 = 95분위 (낙오 병사 제외)
            sd.OffBackSum += fm - Vector2.Dot(off.Pos, co.Facing); sd.OffBackN++;
            foreach (var u in men) { bool near = w.NearOfficer(u); sd.OffAllN++; if (near) sd.OffAllIn++; if (fm - Vector2.Dot(u.Pos, co.Facing) <= 2f) { sd.OffFrontN++; if (near) sd.OffFrontIn++; } }
        }
        {   // offnco: 지휘 종류별 (O 장교 범위 · N 부사관 분대장 · L 지휘 없음) 교전 중 병사-초 · 제압 · 사망 (사망 = 직전 표본 때 교전 중이었고 지금 살아 있는 유닛에 없음)
            var aliveIds = new HashSet<int>();
            foreach (var u in w.Units)
            {
                if (u.Owner != f || !u.Alive) continue;
                aliveIds.Add(u.Id);
                if (u.IsOfficer || !u.Def.IsInfantry || u.Target == null) { sd.LastCls.Remove(u.Id); continue; }
                char k = w.NearOfficer(u) ? 'O' : w.NcoLeads(u) ? 'N' : 'L';
                sd.LastCls[u.Id] = k; sd.ClsSec[k] = sd.ClsSec.GetValueOrDefault(k) + 1; sd.ClsSupp[k] = sd.ClsSupp.GetValueOrDefault(k) + u.SuppressionRatio;
            }
            foreach (var (id, k) in sd.LastCls.ToList()) if (!aliveIds.Contains(id)) { sd.ClsDeath[k] = sd.ClsDeath.GetValueOrDefault(k) + 1; sd.LastCls.Remove(id); }
        }
        int heavyAhead = 0, heavyN = 0;
        foreach (var g in s.Groups)
        {
            if (g.Alive <= 0) continue;
            var key = (f, g.Id);
            if (!gs.TryGetValue(key, out var st)) gs[key] = st = new GState { S = g.State, Since = w.Tick };
            string who = $"{g.Kind.ToString()[0]}{g.Id}";
            var mem = Members(w, g);
            if (g.Kind == TacKind.Fire)
                foreach (var u in mem) if (u.CanDeploy && !u.Def.IsOfficer) { heavyN++; if (Vector2.Dot(u.Pos, axis) > front + 3) heavyAhead++; }
            if (g.Kind == TacKind.Artillery)
            {
                sd.ArtSec++;
                if (g.FireTargetId != 0) sd.ArtFireSec++;
                if (mem.Any(u => w.Tick - u.LastShotTick < 180)) sd.ArtShootSec++;
            }
            if (g.Kind != TacKind.Line && g.Kind != TacKind.Cavalry) continue;
            if (g.Kind == TacKind.Cavalry && w.FindCompany(g.CompanyId) is { } cco)   // cavcmd: 기병 중대 지휘 능력 (장교 · 부사관 대리 · 없음)
            {
                var cl = w.CommandLevelOf(cco);
                if (cco.HandoverUntil > w.Tick) sd.CavHandoverSec++;
                if (cl == CommandLevel.Officer) sd.CavOfficerSec++; else if (cl == CommandLevel.Nco) sd.CavNcoSec++; else sd.CavNoneSec++;
                int ccid = cco.Commander?.Id ?? 0;
                if (st.CmdId != -1 && ccid != st.CmdId) { sd.CavSuccessions++; sd.Log.Add($"t={t:0} 기병 {who} 지휘관 교체 {st.CmdId}->{ccid}"); }
                st.CmdId = ccid;
            }
            // 상태 시간 · 전환
            if (g.Kind == TacKind.Line)
            {
                sd.StateSec[g.State] = sd.StateSec.GetValueOrDefault(g.State) + 1;
                sd.RoleSec[g.Role.ToString()] = sd.RoleSec.GetValueOrDefault(g.Role.ToString()) + 1;
                if (w.FindCompany(g.CompanyId) is { } co)
                {
                    sd.TemplateSec[co.Tactic.ToString()] = sd.TemplateSec.GetValueOrDefault(co.Tactic.ToString()) + 1;
                    bool march = co.MarchUntil > w.Tick;
                    if (march && !st.March) sd.MarchActs++;
                    st.March = march;
                    sd.ActiveTacSec[co.ActiveTactic.ToString()] = sd.ActiveTacSec.GetValueOrDefault(co.ActiveTactic.ToString()) + 1;
                    var dr = co.Drill;
                    if (dr.Phase != st.DPhase) { string k0 = $"Phase {st.DPhase}->{dr.Phase}"; sd.DrillEv[k0] = sd.DrillEv.GetValueOrDefault(k0) + 1; st.DPhase = dr.Phase; }
                    if (dr.Man != st.DMan)
                    {
                        string k = $"Man {st.DMan}->{dr.Man} ({co.ActiveTactic})"; sd.DrillEv[k] = sd.DrillEv.GetValueOrDefault(k) + 1;
                        if (dr.Man == 4) { var ex2 = EnemySupX(w, f, g.Center, rr * 1.5f); if (sd.DrillLog.Count < 60) sd.DrillLog.Add($"t={t:0} {who} 돌격 개시(Man4, {co.ActiveTactic}): 사격받는 적 {ex2.engN}기 평균 제압 {ex2.engAvg:0.00}"); }
                        st.DMan = dr.Man;
                    }
                    if (dr.LiftUntil > w.Tick && dr.LiftUntil != st.DLift) { sd.DrillEv["연신(Lift) 발동"] = sd.DrillEv.GetValueOrDefault("연신(Lift) 발동") + 1; st.DLift = dr.LiftUntil; }
                    if (dr.BoundSquad != st.DBoundSq)
                    {
                        if (st.DBoundSq != 0 && st.DBoundSince > 0) sd.DrillBoundLegs.Add((w.Tick - st.DBoundSince) * Config.SimDt);
                        if (dr.BoundSquad != 0) { st.DBoundSince = w.Tick; sd.DrillEv["Bound 시작"] = sd.DrillEv.GetValueOrDefault("Bound 시작") + 1; }
                        st.DBoundSq = dr.BoundSquad;
                    }
                    int cid = co.Commander?.Id ?? 0;
                    if (st.CmdId != -1 && cid != st.CmdId) { sd.Successions++; sd.Log.Add($"t={t:0} 중대 {who} 지휘관 교체 {st.CmdId}->{cid}"); }
                    st.CmdId = cid;
                    var lvl = w.CommandLevelOf(co);
                    if (co.HandoverUntil > w.Tick) sd.HandoverSec++;
                    if (lvl == CommandLevel.Officer) sd.OfficerSec++; else if (lvl == CommandLevel.Nco) sd.NcoSec++; else sd.NoneSec++;
                }
                if (g.State != st.S)
                {
                    double dw = (w.Tick - st.Since) * Config.SimDt;
                    if (!sd.Dwell.TryGetValue(st.S, out var dl)) sd.Dwell[st.S] = dl = new List<double>();
                    dl.Add(dw);
                    st.Since = w.Tick;
                    if (st.S == TacState.Engage && g.State == TacState.Approach && sd.Flips.Count < 40) sd.Flips.Add($"t={t:0} {who} Engage({dw:0}s)->Approach (역할 {g.Role}, 병력 {g.Alive}/{g.Initial}, 적과 거리 {NearestFoe(w, f, g.Center):0})");
                    sd.Trans[$"{st.S}->{g.State}"] = sd.Trans.GetValueOrDefault($"{st.S}->{g.State}") + 1;
                    if (g.State == TacState.Engage && sd.FirstEngage < 0) sd.FirstEngage = t;
                    if (g.State is TacState.Bound or TacState.Assault or TacState.Charge)
                    {
                        var e = EnemySup(w, f, g.Center, rr * 1.5f);
                        var ex = EnemySupX(w, f, g.Center, rr * 1.5f);
                        if (ex.engN > 0) { sd.ManSupEng.Add((float)ex.engAvg); sd.ManSupMax.Add((float)ex.engMax); }
                        if (g.State is TacState.Assault or TacState.Charge) { sd.ChargeLaunches++; if (ex.engN > 0 && ex.engAvg < 0.2) { sd.PrematureCharges++; if (sd.ChargeLog.Count < 20) sd.ChargeLog.Add($"t={t:0} {who} {g.State} 시작: 사격받는 적 {ex.engN}기 평균 제압 {ex.engAvg:0.00} 최대 {ex.engMax:0.00}, 병력 {g.Alive}/{g.Initial}"); } }
                        bool fb = s.Groups.Any(o => o != g && o.Kind != TacKind.Cavalry && o.Alive > 0 && (o.State == TacState.Engage || o.Role == TacRole.BaseOfFire) && Vector2.Distance(o.Center, g.Center) < rr * 3);
                        double gap = sd.FirstEngage >= 0 ? t - sd.FirstEngage : -1;
                        sd.Manoeuvre.Add((t, ex.engN > 0 ? ex.engAvg : e.avg, e.n, $"{who}:{g.State}", fb, gap));
                        if (sd.FirstManoeuvre < 0) sd.FirstManoeuvre = t;
                    }
                    if (sd.Log.Count < 400 && g.State is TacState.Assault or TacState.Charge or TacState.Fallback or TacState.Bound) sd.Log.Add($"t={t:0} {who} {st.S}->{g.State} (병력 {g.Alive}/{g.Initial}, 역할 {g.Role})");
                }
                // 교대 약진: 엄호 비율
                if (g.SquadBound != 0)
                {
                    int shooting = mem.Count(u => w.Tick - u.LastShotTick < 120 && !u.Walking);
                    sd.OverwatchSum += mem.Count > 0 ? shooting / (double)mem.Count : 0; sd.OverwatchN++;
                    if (st.Sq != g.SquadBound && st.SqSince > 0) sd.BoundLegs.Add((w.Tick - st.SqSince) * Config.SimDt);
                    if (st.Sq != g.SquadBound) st.SqSince = w.Tick;
                    if (st.Sq != g.SquadBound) sd.Log.Add($"t={t:0} {who} 교대 약진 {st.Sq}->{g.SquadBound}");
                }
                st.Sq = g.SquadBound;
                // 유휴: 30초 넘게 명령도 사격도 이동도 없음
                bool quiet = mem.Count > 0 && mem.All(u => w.Tick - u.LastShotTick > 1800 && w.Tick - u.LastOrderTick > 1800 && !u.Walking && u.Target == null);
                bool near = foes.Any(e => Vector2.Distance(e.Pos, g.Center) < 45);
                if (quiet) { if (st.IdleSince < 0) st.IdleSince = w.Tick; }
                else { if (st.IdleLogged) sd.IdleEpisodes.Add($"{who} 유휴 종료 t={t:0}"); st.IdleSince = -1; st.IdleLogged = false; }
                if (quiet) { sd.IdleSec++; if (near) sd.IdleNearSec++; if (!st.IdleLogged && w.Tick - st.IdleSince > 0) { st.IdleLogged = true; sd.IdleEpisodes.Add($"{who} 유휴 시작 t={(st.IdleSince * Config.SimDt):0} (적 45칸 {(near ? "안" : "밖")}, 상태 {g.State}/{g.Role}/{g.Task})"); } }
            }
            else   // 기병
            {
                foreach (var pc in pend) if (pc.F == f && pc.Id == g.Id) pc.C = g.Center;   // 돌격 중 기병 위치 따라감
                if (g.State != st.S && g.State == TacState.Charge)
                {
                    int b0 = 0; foreach (var u in w.Units) if (u.Owner != f && u.Alive && u.Def.IsInfantry && Vector2.Distance(u.Pos, g.Center) <= 6 && u.SuppressionRatio >= 0.25f) b0++;
                    pend.Add(new PendCav(t, f, g.Center, g.Id, b0, g.Alive));
                }
            }
            st.S = g.State;
        }
        Sample3(w, s, sd, t, foes);
        if (w.Tick % 1800 == 0) { var ar = mine.Where(u => u.Def.IsInfantry).ToList(); if (ar.Count > 0) sd.Supp.Add((t, ar.Average(u => u.SuppressionRatio), ar.Count(u => u.SuppressionRatio > 0.3f) / (double)ar.Count)); }
        { long win = (long)(5f / Config.SimDt); foreach (var u in mine) if (u.Def.IsInfantry) { sd.SuppAllInf.Add(u.SuppressionRatio); if (w.Tick - u.LastFiredAtTick < win) sd.SuppEng.Add(u.SuppressionRatio); } }
        { var ar = mine.Where(u => u.Def.IsInfantry).ToList(); if (ar.Count > 0) { double m = ar.Average(u => u.SuppressionRatio), fr = ar.Count(u => u.SuppressionRatio > 0.3f) / (double)ar.Count; sd.SuppSum += m; sd.SuppN++; if (m > sd.SuppPeak) { sd.SuppPeak = m; sd.SuppPeakT = t; } if (fr > sd.SuppFracPeak) { sd.SuppFracPeak = fr; sd.SuppFracPeakT = t; } sd.SuppUnitMax = Math.Max(sd.SuppUnitMax, ar.Max(u => u.SuppressionRatio)); sd.SuppOver30 += ar.Count(u => u.SuppressionRatio > 0.3f); } }
        sd.HeavySec += heavyN; sd.HeavyAheadSec += heavyAhead; sd.HeavyAheadMax = Math.Max(sd.HeavyAheadMax, heavyAhead);
        // 연대 · 대대 역할 변경 기록
        foreach (var r in s.Regiments)
        {
            if (r.HasFireTarget) sd.RegFireTargetSec++;
            if (r.Lifted) sd.RegLiftSec++;
            var key = (f, -1000 - r.Id);
            if (!gs.TryGetValue(key, out var rs)) gs[key] = rs = new GState { S = (TacState)255 };
            if ((int)r.Phase != rs.Sq) { sd.Log.Add($"t={t:0} 연대 R{r.Id} 국면 {(RegPhase)rs.Sq}->{r.Phase} ({r.Task}, 병력 {r.Alive}/{r.Initial})"); rs.Sq = (int)r.Phase; }
        }
        foreach (var b in s.Battalions)
        {
            var key = (f, -2000 - b.Id);
            if (!gs.TryGetValue(key, out var bs)) gs[key] = bs = new GState { Sq = -1 };
            int code = (int)b.Role * 10 + (b.Split ? 1 : 0) + (b.FlankGroup != 0 ? 2 : 0) + (b.ReserveCommitted ? 4 : 0);
            if (code != bs.Sq) { if (bs.Sq != -1) sd.Log.Add($"t={t:0} 대대 B{b.Id} 역할 {b.Role}{(b.Split ? "/분할" : "")}{(b.FlankGroup != 0 ? "/측면연장" : "")}{(b.ReserveCommitted ? "/예비투입" : "")} (병력 {b.Alive}/{b.Initial})"); bs.Sq = code; }
        }
    }

    static bool Shot(World w, Unit u, int ticks = 120) => w.Tick - u.LastShotTick < ticks;


    static void ArtMetrics(World w, TacFaction s, int f, C3 x, double t)
    {
        var m = x.AM;
        if (m.FirstInf < 0 || m.FirstContact < 0)
            foreach (var u in w.Units)
            {
                if (u.Owner != f || !u.Alive || !u.Def.IsInfantry) continue;
                if (m.FirstInf < 0 && Shot(w, u, 30)) m.FirstInf = t;
                if (m.FirstContact < 0 && u.Weapon != null)
                {
                    foreach (var e in w.Units) if (e.Owner != f && e.Alive && e.Def.IsInfantry && Vector2.Distance(e.Pos, u.Pos) <= u.Weapon.Range * 1.2f && w.Fog.CanSee(f, e)) { m.FirstContact = t; break; }
                }
            }
        var gunSeen = new HashSet<int>();
        foreach (var g in s.Groups)
        {
            if (g.Alive <= 0 || g.Kind == TacKind.Line || g.Kind == TacKind.Cavalry) continue;
            foreach (var u in Members(w, g))
            {
                if (u.Weapon == null || !u.Def.Key.StartsWith("heavy_") || !(u.Def.Key.EndsWith("gun") || u.Def.Key.EndsWith("howitzer")) || u.Def.Key.EndsWith("machinegunner") || !gunSeen.Add(u.Id)) continue;
                m.GunSec++;
                if (g.FireTargetId != 0 || u.Target != null) m.ArtSecTarget++;
                var prev = m.Prev.GetValueOrDefault(u.Id, (u.Pos, u.Facing));
                bool moved = Vector2.Distance(prev.Item1, u.Pos) > 0.4f;
                if (moved) m.MoveSec++;
                else if (MathF.Abs(MathF.IEEERemainder(u.Facing - prev.Item2, MathF.Tau)) > 0.1f) m.Traverse++;
                m.Prev[u.Id] = (u.Pos, u.Facing);
                long seen = m.Seen.GetValueOrDefault(u.Id, -1);
                if (u.LastShotTick > seen && u.LastShotTick >= 0 && w.Tick - u.LastShotTick < 60)
                {
                    m.Seen[u.Id] = u.LastShotTick;
                    string k = u.Def.Key;
                    Entity? tg = u.Target ?? (g.FireTargetId != 0 ? w.Find(g.FireTargetId) : null);
                    m.Shots[k] = m.Shots.GetValueOrDefault(k) + 1; m.ArtSecShoot++;
                    if (tg == null) { m.NoTgtShots++; continue; }
                    float d = Vector2.Distance(u.Pos, tg.Pos);
                    m.DistSum[k] = m.DistSum.GetValueOrDefault(k) + d;
                    if (d > u.Weapon.Range * 0.8f) m.Far[k] = m.Far.GetValueOrDefault(k) + 1;
                    bool seenTgt = tg is Unit tu ? (u.Weapon.SpotterOnly ? w.SeenBySpotter(f, tu) : w.Fog.CanSee(f, tu)) : true;
                    if (!seenTgt) m.Unseen[k] = m.Unseen.GetValueOrDefault(k) + 1;
                    if (m.FirstArt < 0) m.FirstArt = t;
                    if (m.FirstInf < 0 || t < m.FirstInf) m.ArtBeforeInf++;
                    if (m.FirstContact < 0 || t < m.FirstContact) m.ArtBeforeContact++;
                    if (m.Pend.Count < 600)
                    {
                        var ids = new List<int>(); float sp = 0;
                        foreach (var e in w.Units) if (e.Owner != f && e.Alive && e.Def.IsInfantry && Vector2.Distance(e.Pos, tg.Pos) <= 6f) { ids.Add(e.Id); sp += e.SuppressionRatio; }
                        m.Pend.Add((t + 12, tg.Pos, ids, ids.Count > 0 ? sp / ids.Count : 0));
                    }
                }
            }
        }
        for (int i = m.Pend.Count - 1; i >= 0; i--)
        {
            var pe = m.Pend[i]; if (t < pe.t) continue;
            m.EffN++; float sp = 0; int al = 0;
            foreach (var id in pe.ids) if (w.Find(id) is Unit e && e.Alive) { al++; sp += e.SuppressionRatio; } else m.EffKills++;
            if (al > 0) { m.EffSupN++; m.EffSupSum += sp / al - pe.sup; }
            m.Pend.RemoveAt(i);
        }
    }

    static (float sup, int cnt) VisEnemyAround(World w, int f, Vector2 pos, float r)
    {
        float sum = 0; int n = 0;
        foreach (var e in w.Units)
            if (e.Owner != f && e.Alive && !e.Def.IsArmored && Vector2.Distance(e.Pos, pos) <= r && w.Fog.CanSee(f, e)) { sum += e.SuppressionRatio; n++; }
        return (n > 0 ? sum / n : 0, n);
    }

    static void SupEvent(C3 x, string kind, double t, string who, float sup, int cnt, double age)
    {
        if (!x.SupEv.TryGetValue(kind, out var l)) x.SupEv[kind] = l = new();
        l.Add((sup, cnt, age));
        if (x.SupLog.Count < 120) x.SupLog.Add($"t={t:0} {who} {kind}: 가시 적 {cnt}기 평균 제압 {sup:0.00} (우세 {(cnt > 0 && sup >= SupThr ? "O" : "X")}, 교전 {age:0}s)");
    }

    static void Sample3(World w, TacFaction s, Side sd, double t, List<Unit> foes)
    {
        var x = sd.X; int f = s.Faction;
        int lineAlive = 0, fallbackN = 0;
        foreach (var g in s.Groups) if (g.Kind == TacKind.Line && g.Alive > 0) { lineAlive += g.Alive; if (g.State == TacState.Fallback) fallbackN++; }
        x.AliveHist.Enqueue(lineAlive); if (x.AliveHist.Count > 31) x.AliveHist.Dequeue();
        double loss30 = x.AliveHist.Count >= 31 ? (x.AliveHist.Peek() - lineAlive) / (double)Math.Max(1, x.AliveHist.Peek()) : 0;
        bool trouble = fallbackN > 0 || loss30 >= 0.20;
        bool anyEng = s.Groups.Any(g => g.Kind == TacKind.Line && g.Alive > 0 && g.State == TacState.Engage);
        if (anyEng) { x.TotalSec++; if (trouble) x.TroubleSec++; }
        if (s.ReserveCommits > x.PrevReserve)
        {
            for (int k = x.PrevReserve; k < s.ReserveCommits; k++) { x.ResTotal++; if (trouble) x.ResTrouble++; x.ResLog.Add($"t={t:0} 예비 투입: 전선 중대 후퇴 {fallbackN}개, 30초 손실 {loss30:P0} (곤경 {(trouble ? "O" : "X")}), 전선 병력 {lineAlive}"); }
            x.PrevReserve = s.ReserveCommits;
        }
        var aims = new List<Vector2>();
        foreach (var g in s.Groups)
            if (g.Kind == TacKind.Artillery && g.Alive > 0)
                foreach (var u in Members(w, g))
                    if (Shot(w, u, 180))
                    {
                        Entity? tg = u.Target ?? (g.FireTargetId != 0 ? w.Find(g.FireTargetId) : null);
                        if (tg != null) aims.Add(tg.Pos);
                    }
        foreach (var a in aims) x.ArtAim.Enqueue((t, a));
        while (x.ArtAim.Count > 0 && t - x.ArtAim.Peek().t > 10) x.ArtAim.Dequeue();
        var engEnemies = new List<Vector2>();
        foreach (var g in s.Groups) if (g.Kind == TacKind.Line && g.Alive > 0 && w.FindCompany(g.CompanyId) is { } cc && cc.Drill.Phase == DrillPhase.Engage && cc.Drill.Enemy != Vector2.Zero) engEnemies.Add(cc.Drill.Enemy);
        foreach (var a in aims) { x.ArtAimN++; if (engEnemies.Any(e => Vector2.Distance(e, a) <= 15)) x.ArtAimOnEngaged++; }
        ArtMetrics(w, s, f, x, t);
        for (int i = x.ArtP.Count - 1; i >= 0; i--)
        {
            var p = x.ArtP[i];
            foreach (var a in aims) { p.Best = MathF.Min(p.Best, Vector2.Distance(a, p.P)); p.Fired = true; }
            if (t - p.T0 >= 15)
            {
                x.ArtAssaults++; if (p.Best <= 12) x.ArtOnTarget++; if (p.Fired) x.ArtFiredAny++;
                if (x.ArtLog.Count < 60) x.ArtLog.Add($"t={p.T0:0} {p.Who} 돌격 표적 둘레 포병: 사격 {(p.Fired ? "O" : "X")}, 조준점 최근접 {(p.Best < 900 ? p.Best.ToString("0") : "-")}칸 ({(p.Best <= 12 ? "같은 표적" : "다른 표적/없음")})");
                x.ArtP.RemoveAt(i);
            }
        }
        for (int i = x.CavP.Count - 1; i >= 0; i--)
        {
            var p = x.CavP[i];
            int sh = 0; foreach (var u in w.Units) if (u.Owner == f && u.Alive && u.Def.IsInfantry && Shot(w, u, 180) && Vector2.Distance(u.Pos, p.P) <= 25) sh++;
            p.Peak = Math.Max(p.Peak, sh);
            if (t - p.T0 >= 15)
            {
                p.AssaultNear = x.AssaultTimes.Any(a => Math.Abs(a - p.T0) <= 15);
                x.CavN++; if (p.Peak >= 10) x.CavShoot10++; if (p.AssaultNear) x.CavAssaultNear++;
                if (x.CavLog.Count < 60) x.CavLog.Add($"t={p.T0:0} {p.Who} 기병 돌격: 시작 때 표적 25칸 안 사격 보병 {p.Shooters}, 15초 안 최대 {p.Peak}, 보병 돌격 ±15초 {(p.AssaultNear ? "O" : "X")}");
                x.CavP.RemoveAt(i);
            }
        }
        var reg = new Dictionary<int, int>(); foreach (var bb in s.Battalions) reg[bb.Id] = bb.Regiment;
        int RegOf(TacGroup gg) => reg.GetValueOrDefault(gg.Battalion);
        var bnRatio = new Dictionary<int, double>();
        foreach (var grp in s.Groups.Where(g => g.Kind == TacKind.Line && g.Alive > 0 && g.Role == TacRole.BaseOfFire).GroupBy(g => RegOf(g)))
        {
            int n = 0, sh = 0;
            foreach (var g in grp) foreach (var u in Members(w, g)) if (u.Weapon != null && !u.IsOfficer) { n++; if (Shot(w, u)) sh++; }
            double r = n > 0 ? sh / (double)n : 0; bnRatio[grp.Key] = r;
            if (r >= 0.15 && !x.BnFireOpen.ContainsKey(grp.Key)) x.BnFireOpen[grp.Key] = t;
        }
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Line || g.Alive <= 0 || w.FindCompany(g.CompanyId) is not { } co) continue;
            if (!x.Co.TryGetValue(g.Id, out var cs)) x.Co[g.Id] = cs = new CoSt { PrevState = g.State };
            var dr = co.Drill; var mem = Members(w, g);
            float rng = 0; foreach (var u in mem) if (!u.IsOfficer && u.Weapon != null && u.Role is UnitRole.Rifle or UnitRole.Manoeuvre or UnitRole.None) rng = MathF.Max(rng, u.Weapon.Range);
            if (rng <= 0) rng = 16f;
            var cen = mem.Count > 0 ? new Vector2(mem.Average(u => u.Pos.X), mem.Average(u => u.Pos.Y)) : g.Center;
            var (esupp, ecnt) = VisEnemyAround(w, f, cen, rng + 4f);
            bool superior = ecnt > 0 && esupp >= SupThr;
            double age = dr.Phase == DrillPhase.Engage ? (w.Tick - dr.PhaseTick) * Config.SimDt : 0;
            string who = $"L{g.Id}";
            if (dr.Phase == DrillPhase.Engage)
            {
                if (ecnt > 0) { x.SupAllEngage.Add(esupp); x.SupAllN++; if (superior) x.SupAllSuperior++; }
                if (cs.PhaseSeen != dr.PhaseTick) { cs.PhaseSeen = dr.PhaseTick; cs.FireOpenT = -1; cs.ManLogged = false; }
                if (cs.FireOpenT < 0 && mem.Any(u => u.Role != UnitRole.Manoeuvre && u.LastShotTick >= dr.PhaseTick && u.LastShotTick > 0)) cs.FireOpenT = t;
            }
            else cs.PhaseSeen = -1;
            var fireTeam = mem.Where(u => !u.IsOfficer && u.Weapon != null && u.Role != UnitRole.Manoeuvre).ToList();
            double fireRatio = fireTeam.Count > 0 ? fireTeam.Count(u => Shot(w, u)) / (double)fireTeam.Count : 0;
            if (dr.Phase == DrillPhase.Engage && ecnt > 0)
            {
                if (dr.BoundSquad > 0)
                {
                    var part = mem.Where(u => !u.IsOfficer && u.Weapon != null && u.Role != UnitRole.Manoeuvre && u.Squad?.Id != dr.BoundSquad).ToList();
                    var mov = mem.Where(u => !u.IsOfficer && u.Weapon != null && u.Squad?.Id == dr.BoundSquad).ToList();
                    if (part.Count > 0)
                    {
                        double r = part.Count(u => Shot(w, u)) / (double)part.Count;
                        x.BoundPartnerSum += r; x.BoundN++; if (r == 0) x.BoundPartnerSilent++; if (r >= 0.5) x.BoundPartnerHalf++;
                        if (mov.Count > 0) x.BoundMoverSum += mov.Count(u => Shot(w, u)) / (double)mov.Count;
                    }
                }
                else if (dr.Man is not (1 or 2 or 4)) { if (fireTeam.Count > 0) { x.BasePartnerSum += fireRatio; x.BaseN++; } }
                if (dr.Man is 1 or 2 or 4 && fireTeam.Count > 0)
                {
                    x.ManPartnerSum += fireRatio; x.ManN++; if (fireRatio == 0) x.ManPartnerSilent++; if (fireRatio >= 0.5) x.ManPartnerHalf++;
                    if (g.Role != TacRole.BaseOfFire && bnRatio.TryGetValue(RegOf(g), out var br)) { x.ManSisterSum += br; x.ManSisterN++; }
                }
            }
            bool boundStart = dr.BoundSquad > 0 && cs.PrevBound == 0;
            bool manStart = dr.Man is 1 or 2 && cs.PrevMan == 0;
            bool man4 = dr.Man == 4 && cs.PrevMan != 4;
            bool stAssault = g.State is TacState.Assault or TacState.Charge && cs.PrevState != g.State;
            if (boundStart) SupEvent(x, "약진 시작", t, who, esupp, ecnt, age);
            if (manStart) SupEvent(x, "기동조 출발", t, who, esupp, ecnt, age);
            if (man4) SupEvent(x, "Drill 돌격(Man4)", t, who, esupp, ecnt, age);
            if (stAssault) SupEvent(x, "상태 돌격(Assault/Charge)", t, who, esupp, ecnt, age);
            if (manStart || boundStart || man4)
            {
                double lag = cs.FireOpenT >= 0 ? t - cs.FireOpenT : -1;
                string kind = manStart ? "기동조 출발" : man4 ? "돌격" : "약진";
                if (!cs.ManLogged || man4) { x.Lag.Add((lag, fireRatio, kind)); if (x.LagLog.Count < 120) x.LagLog.Add($"t={t:0} {who} {kind}: 화력조 첫 사격 후 {(lag >= 0 ? lag.ToString("0") + "s" : "사격 전")}, 출발 때 화력조 사격 비율 {fireRatio:P0}, 교전 {age:0}s"); }
                if (manStart) cs.ManLogged = true;
                if ((manStart || man4) && g.Role != TacRole.BaseOfFire)
                {
                    if (x.BnFireOpen.TryGetValue(RegOf(g), out var bt)) x.BnLag.Add((t - bt, bnRatio.GetValueOrDefault(RegOf(g))));
                    else x.BnLag.Add((-1, 0));
                }
            }
            if (man4 || stAssault)
            {
                x.AssaultTimes.Add(t);
                { int fa = g.Alive; int ea = 0; Vector2 pp = g.Center; foreach (var e in w.Units) if (e.Owner != f && e.Alive && Vector2.Distance(e.Pos, pp) <= 20) ea++; x.AsP.Add(new AsPend { T0 = t, Gid = g.Id, F = fa, E = ea, P = pp, Who = who, Kind = man4 ? "Man4" : "State" }); }
                Vector2 p = dr.Enemy != Vector2.Zero && man4 ? dr.Enemy : (foes.Count > 0 ? foes.OrderBy(e => Vector2.Distance(e.Pos, cen)).First().Pos : cen);
                var ap = new ArtPend { T0 = t, P = p, Who = who };
                foreach (var (at, apos) in x.ArtAim) { ap.Best = MathF.Min(ap.Best, Vector2.Distance(apos, p)); ap.Fired = true; }
                x.ArtP.Add(ap);
            }
            cs.PrevBound = dr.BoundSquad; cs.PrevMan = dr.Man; cs.PrevState = g.State;
        }
        for (int ai = x.AsP.Count - 1; ai >= 0; ai--)
        {
            var a = x.AsP[ai]; var gg = s.Groups.FirstOrDefault(q => q.Id == a.Gid);
            if (gg != null && gg.State == TacState.Fallback) a.Retreat = true;
            if (t - a.T0 < 40) continue;
            int fa2 = gg?.Alive ?? 0; int ea2 = 0; foreach (var e in w.Units) if (e.Owner != f && e.Alive && Vector2.Distance(e.Pos, a.P) <= 20) ea2++;
            int fl = a.F - fa2, el = a.E - ea2;
            string oc = a.Retreat ? "후퇴" : (el > fl ? "우세" : "열세");
            x.AsDone.Add($"{a.Kind} {oc} 아군손실 {fl} 적손실(반경20) {el} 시작적 {a.E}"); x.AsP.RemoveAt(ai);
        }
        foreach (var g in s.Groups)
        {
            if (g.Kind != TacKind.Cavalry || g.Alive <= 0) continue;
            if (!x.Co.TryGetValue(10000 + g.Id, out var cs)) x.Co[10000 + g.Id] = cs = new CoSt { PrevState = g.State };
            if (g.State == TacState.Charge && cs.PrevState != TacState.Charge)
            {
                var p = g.OrderPoint; int sh = 0;
                foreach (var u in w.Units) if (u.Owner == f && u.Alive && u.Def.IsInfantry && Shot(w, u, 180) && Vector2.Distance(u.Pos, p) <= 25) sh++;
                x.CavP.Add(new CavPend { T0 = t, P = p, Id = g.Id, Shooters = sh, Peak = sh, Who = $"C{g.Id}" });
            }
            cs.PrevState = g.State;
        }
    }

    static string Dist(string name, List<float> l)
    {
        if (l.Count == 0) return name + " (표본 없음)";
        var c = l.OrderBy(x => x).ToList();
        return $"{name} n={c.Count} 평균 {c.Average():0.000} 중앙 {c[c.Count / 2]:0.000} p90 {c[(int)(c.Count * 0.9)]:0.000} p99 {c[(int)(c.Count * 0.99)]:0.000} 최대 {c[^1]:0.000} (>0.3 {c.Count(x => x > 0.3f) * 100.0 / c.Count:0.0}%)";
    }

    static double Med(List<double> l) { if (l.Count == 0) return 0; var c = l.OrderBy(x => x).ToList(); return c[c.Count / 2]; }

    static void Print(StringBuilder sb, int f, Side sd)
    {
        sb.AppendLine($"\n## 세력 {f}");
        double tot = sd.StateSec.Values.Sum();
        sb.AppendLine("중대 상태 시간(중대-초): " + string.Join(", ", sd.StateSec.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value:0} ({k.Value / Math.Max(1, tot):P0})")));
        sb.AppendLine("역할 시간: " + string.Join(", ", sd.RoleSec.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value:0}")));
        sb.AppendLine("중대 전술 틀 시간: " + string.Join(", ", sd.TemplateSec.Select(k => $"{k.Key} {k.Value:0}")));
        sb.AppendLine("상태 전환: " + string.Join(", ", sd.Trans.OrderByDescending(k => k.Value).Take(20).Select(k => $"{k.Key} x{k.Value}")));
        sb.AppendLine("트리거 누계(횟수 / 시각): " + string.Join(" | ", sd.Counters.Where(k => k.Value.Count > 0).Select(k => $"{k.Key} {k.Value.Count} [{string.Join(",", k.Value.Take(12).Select(x => x.ToString("0")))}{(k.Value.Count > 12 ? ",..." : "")}]")));
        sb.AppendLine("상태 체류(초, 평균/중앙/횟수): " + string.Join(", ", sd.Dwell.Select(k => $"{k.Key} {k.Value.Average():0.0}/{Med(k.Value):0.0}/{k.Value.Count}")));
        sb.AppendLine($"교대 약진 한 번(다리) 길이: 횟수 {sd.BoundLegs.Count}, 중앙 {Med(sd.BoundLegs):0.0}s, 3초 미만 {sd.BoundLegs.Count(x => x < 3)}회");
        sb.AppendLine("자군 보병 제압(30초): " + string.Join(" ", sd.Supp.Select(x => $"{x.t:0}:{x.mean:0.00}/{x.frac30:P0}")));
        sb.AppendLine($"자군 보병 제압 요약: 시간 평균 {(sd.SuppN > 0 ? sd.SuppSum / sd.SuppN : 0):0.000}, 평균 최고 {sd.SuppPeak:0.000} (t={sd.SuppPeakT:0}), 제압>0.3 비율 최고 {sd.SuppFracPeak:P1} (t={sd.SuppFracPeakT:0}), 개인 최고 {sd.SuppUnitMax:0.00}, 제압>0.3 기-초 {sd.SuppOver30}");
        sb.AppendLine("제압 분포 (Suppression/1000 = SuppressionRatio, 이중 나눗셈 없음): " + Dist("전체 보병", sd.SuppAllInf) + " | " + Dist("교전 중(최근 5초 안 피격) 보병", sd.SuppEng));
        sb.AppendLine("기동 개시 때 사격받는 적 제압: " + Dist("평균", sd.ManSupEng) + " | " + Dist("최대값", sd.ManSupMax));
        sb.AppendLine($"돌격 개시 {sd.ChargeLaunches}회, 그중 사격받는 적 평균 제압 < 0.2 (조기 돌격) {sd.PrematureCharges}회");
        foreach (var l in sd.ChargeLog) sb.AppendLine("  " + l);
        sb.AppendLine("중대 활성 전술 틀(ActiveTactic) 시간: " + string.Join(", ", sd.ActiveTacSec.Select(k => $"{k.Key} {k.Value:0}")));
        sb.AppendLine("Drill 사건: " + string.Join(", ", sd.DrillEv.OrderByDescending(k => k.Value).Select(k => $"{k.Key} x{k.Value}")));
        sb.AppendLine($"Drill 약진 다리: 횟수 {sd.DrillBoundLegs.Count}, 중앙 {Med(sd.DrillBoundLegs):0.0}s, 평균 {(sd.DrillBoundLegs.Count > 0 ? sd.DrillBoundLegs.Average() : 0):0.0}s, <3s {sd.DrillBoundLegs.Count(x => x < 3)}회");
        foreach (var l in sd.DrillLog.Take(30)) sb.AppendLine("  " + l);
        { var am = sd.Counters["ArtMoves"]; var gaps = new List<double>(); for (int i = 1; i < am.Count; i++) gaps.Add(am[i] - am[i - 1]); sb.AppendLine($"포병 이동 {am.Count}회, 간격(세력 합산) 중앙 {Med(gaps):0.0}s 평균 {(gaps.Count > 0 ? gaps.Average() : 0):0.0}s"); }
        sb.AppendLine("Engage->Approach 되돌림 예:");
        foreach (var l in sd.Flips.Take(12)) sb.AppendLine("  " + l);
        sb.AppendLine($"첫 교전 t={sd.FirstEngage:0}, 첫 기동(약진/돌격) t={sd.FirstManoeuvre:0}");
        sb.AppendLine($"교대 약진 엄호 비율(엄호조 사격 중 / 중대원) 평균 {(sd.OverwatchN > 0 ? sd.OverwatchSum / sd.OverwatchN : 0):0.00} (표본 {sd.OverwatchN}초)");
        sb.AppendLine($"기동 개시 {sd.Manoeuvre.Count}회, 평균 적 제압 {(sd.Manoeuvre.Count > 0 ? sd.Manoeuvre.Average(m => m.sup) : 0):0.00}, 화력 기반 존재 {sd.Manoeuvre.Count(m => m.fireBase)}회");
        foreach (var m in sd.Manoeuvre.Take(40)) sb.AppendLine($"  기동 t={m.t:0} {m.who} 적제압 {m.sup:0.00} (적 {m.n}) 화력기반 {(m.fireBase ? "O" : "X")} 첫교전 후 {m.gap:0}s");
        sb.AppendLine($"행진 사격 발동 {sd.MarchActs}, 지휘관 교체 {sd.Successions}, 지휘 능력(중대-초): 장교 {sd.OfficerSec} / 부사관 대리 {sd.NcoSec} / 없음 {sd.NoneSec}, 인계 중 {sd.HandoverSec}");
        sb.AppendLine($"포병 {sd.ArtSec:0} 포대-초, 강제 표적 지정 중 {sd.ArtFireSec:0} ({(sd.ArtSec > 0 ? sd.ArtFireSec / sd.ArtSec : 0):P0}), 최근 3초 안 사격한 포대-초 {sd.ArtShootSec:0}, 연대 화력표적 있음 {sd.RegFireTargetSec:0}초, 연신 상태 {sd.RegLiftSec:0}초");
        sb.AppendLine($"중화기 앞섬(소총 전선+3칸, 거치형 중화기 기-초): {sd.HeavyAheadSec:0} / {sd.HeavySec:0} ({(sd.HeavySec > 0 ? sd.HeavyAheadSec / sd.HeavySec : 0):P0}), 한 시점 최대 {sd.HeavyAheadMax}기");
        sb.AppendLine($"유휴 중대(30초+ 명령 · 사격 · 이동 없음): {sd.IdleSec:0} 중대-초, 그중 적 45칸 안 {sd.IdleNearSec:0}");
        foreach (var e in sd.IdleEpisodes.Take(20)) sb.AppendLine("  " + e);
        foreach (var (gid, ids) in sd.SpotTeam.OrderBy(k => k.Key))
        {
            var ds = sd.SpotDeaths.Where(d => d.gid == gid).ToList();
            sb.AppendLine($"[관측반] 팀 S{gid}: 편성 {ids.Count}명, 종료 생존 {ids.Count - ds.Count}" + (ds.Count > 0 ? ", 사망 " + string.Join(" ", ds.Select(d => $"t={d.t:0}/적{d.dist:0.0}칸")) : ""));
        }
        sb.AppendLine($"[기병지휘] 중대-초: 장교 {sd.CavOfficerSec} / 부사관 대리 {sd.CavNcoSec} / 없음 {sd.CavNoneSec}, 인계 중 {sd.CavHandoverSec}, 지휘관 교체 {sd.CavSuccessions}");
        sb.AppendLine("기병: " + (sd.Cav.Count == 0 ? "돌격 없음" : ""));
        foreach (var c in sd.Cav) sb.AppendLine("  " + c);
        {
            var x = sd.X;
            sb.AppendLine($"[돌격결과] 완료 {x.AsDone.Count}: 후퇴 {x.AsDone.Count(q => q.Contains(" 후퇴 "))} 우세 {x.AsDone.Count(q => q.Contains(" 우세 "))} 열세 {x.AsDone.Count(q => q.Contains(" 열세 "))}; 미완 {x.AsP.Count}");
            foreach (var q in x.AsDone.Take(30)) sb.AppendLine("  돌격결과: " + q);
            sb.AppendLine("== 3차 지표 ==");
            sb.AppendLine("[틱시작] " + string.Join(", ", sd.X.TickStart.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
            sb.AppendLine($"[엄호] 약진 중 (적 가시) 파트너 사격 비율 평균 {(x.BoundN > 0 ? x.BoundPartnerSum / x.BoundN : 0):0.00} (표본 {x.BoundN}초, 무사격 {x.BoundPartnerSilent}, >=50% {x.BoundPartnerHalf}), 약진 분대 자신 {(x.BoundN > 0 ? x.BoundMoverSum / x.BoundN : 0):0.00} | 기동조 이동 중 화력조 사격 비율 평균 {(x.ManN > 0 ? x.ManPartnerSum / x.ManN : 0):0.00} (표본 {x.ManN}초, 무사격 {x.ManPartnerSilent}, >=50% {x.ManPartnerHalf}), 같은 대대 화력기반 {(x.ManSisterN > 0 ? x.ManSisterSum / x.ManSisterN : 0):0.00} (표본 {x.ManSisterN}) | 기준: 교전 중 비약진 사격조 {(x.BaseN > 0 ? x.BasePartnerSum / x.BaseN : 0):0.00} (표본 {x.BaseN}초)");
            sb.AppendLine($"[화력 우세] 교전 중 (적 가시) 중대-초 {x.SupAllN}, 우세 (평균 제압>={SupThr}) {x.SupAllSuperior} ({(x.SupAllN > 0 ? x.SupAllSuperior * 100.0 / x.SupAllN : 0):0.0}%), 가시 적 평균 제압 {Dist("", x.SupAllEngage)}");
            foreach (var (k, l) in x.SupEv) sb.AppendLine($"  {k}: {l.Count}회, 평균 적 제압 {l.Average(e => e.sup):0.000}, 우세 {l.Count(e => e.cnt > 0 && e.sup >= SupThr)}회, 우세 아님+{PatThr}초 넘는 교전(인내) {l.Count(e => !(e.cnt > 0 && e.sup >= SupThr) && e.age > PatThr)}회, 그 외 {l.Count(e => !(e.cnt > 0 && e.sup >= SupThr) && e.age <= PatThr)}회, 가시 적 0 {l.Count(e => e.cnt == 0)}회");
            foreach (var l in x.SupLog.Take(40)) sb.AppendLine("  " + l);
            var lg = x.Lag.Where(e => e.lag >= 0).Select(e => e.lag).ToList();
            sb.AppendLine($"[시간차] 화력조 첫 사격 -> 기동 출발: 표본 {x.Lag.Count}, 사격 전 출발 {x.Lag.Count(e => e.lag < 0)}, 2초 미만 {x.Lag.Count(e => e.lag >= 0 && e.lag < 2)}, 중앙 {Med(lg):0.0}s, 평균 {(lg.Count > 0 ? lg.Average() : 0):0.0}s, 출발 때 화력조 사격 비율 평균 {(x.Lag.Count > 0 ? x.Lag.Average(e => e.ratio) : 0):0.00}, 비율>=30% {x.Lag.Count(e => e.ratio >= 0.3)}");
            var bl = x.BnLag.Where(e => e.lag >= 0).Select(e => e.lag).ToList();
            sb.AppendLine($"[대대 시간차] 화력기반 중대 점화 -> 기동 중대 출발: 표본 {x.BnLag.Count}, 점화 전 출발 {x.BnLag.Count(e => e.lag < 0)}, 중앙 {Med(bl):0.0}s, 출발 때 화력기반 사격 비율 평균 {(x.BnLag.Count > 0 ? x.BnLag.Average(e => e.ratio) : 0):0.00}");
            foreach (var l in x.LagLog.Take(30)) sb.AppendLine("  " + l);
            sb.AppendLine($"[예비] 투입 {x.ResTotal}회, 그중 곤경 (전선 후퇴 중대 있음 또는 30초 손실>=20%) {x.ResTrouble}회; 기준 곤경 비율 {(x.TotalSec > 0 ? x.TroubleSec * 100.0 / x.TotalSec : 0):0.0}% ({x.TroubleSec}/{x.TotalSec}초)");
            foreach (var l in x.ResLog) sb.AppendLine("  " + l);
            sb.AppendLine($"[포병] 돌격 {x.ArtAssaults}회 판정: 돌격 표적 둘레 (앞10초~뒤15초) 포병 사격 있음 {x.ArtFiredAny}, 조준점이 돌격 표적 12칸 안 {x.ArtOnTarget}; 포병 조준점 {x.ArtAimN}건 중 교전 중 보병 표적 15칸 안 {x.ArtAimOnEngaged} ({(x.ArtAimN > 0 ? x.ArtAimOnEngaged * 100.0 / x.ArtAimN : 0):0}%)");
            { var m = x.AM; sb.AppendLine($"[포병2] 포-초 {m.GunSec}, 표적 보유 {m.ArtSecTarget} ({(m.GunSec > 0 ? m.ArtSecTarget * 100.0 / m.GunSec : 0):0}%), 이동 중 {m.MoveSec}, 제자리 선회 {m.Traverse}, 사격 {m.ArtSecShoot} (표적 보유 중 사격률 {(m.ArtSecTarget > 0 ? m.ArtSecShoot * 100.0 / m.ArtSecTarget : 0):0.0}%/포-초), 표적없는 사격 {m.NoTgtShots}; 첫 포병 사격 t={m.FirstArt:0}, 첫 보병 사격 t={m.FirstInf:0}, 첫 접촉(가시 적 사거리x1.2 안) t={m.FirstContact:0}, 보병 첫 사격 전 포병 사격 {m.ArtBeforeInf}, 접촉 전 {m.ArtBeforeContact}; 착탄 12초 뒤 평가 {m.EffN}건: 6칸 안 적 보병 사망 {m.EffKills}, 생존자 제압 평균 변화 {(m.EffSupN > 0 ? m.EffSupSum / m.EffSupN : 0):+0.000;-0.000}"); foreach (var k in m.Shots.Keys.OrderBy(q => q)) sb.AppendLine($"  [포종] {k}: 사격 {m.Shots[k]}, 평균 거리 {m.DistSum.GetValueOrDefault(k) / Math.Max(1, m.Shots[k] - 0):0.0}, 사거리 80% 초과 {m.Far.GetValueOrDefault(k)}, 관측 안 되는 표적 {m.Unseen.GetValueOrDefault(k)}"); }
            foreach (var l in x.ArtLog.Take(25)) sb.AppendLine("  " + l);
            sb.AppendLine($"[대열] 교전 중 정지 중대-초 {sd.RankDisp.Count}: 2칸 넘게 벗어난 병사 비율 평균 {(sd.RankDisp.Count > 0 ? sd.RankDisp.Average() : 0):0.000} 중앙 {Med(sd.RankDisp.Select(q => (double)q).ToList()):0.000}, 앞줄 빈 슬롯 평균 {(sd.RankFront.Count > 0 ? sd.RankFront.Average() : 0):0.00}, 앞줄 연속 빈 슬롯 평균 {(sd.RankGap.Count > 0 ? sd.RankGap.Average() : 0):0.00}, 장교 경보 켜진 중대-초 {sd.RankAlarmSec}");
            sb.AppendLine($"[장교위치] 교전 중 정지 중대-초 {sd.OffBackN}: 장교가 병사 최전열보다 뒤 {(sd.OffBackN > 0 ? sd.OffBackSum / sd.OffBackN : 0):0.00}칸, 지휘 범위 안 병사 비율 앞줄(최전열 2칸) {(sd.OffFrontN > 0 ? sd.OffFrontIn / sd.OffFrontN : 0):0.000} ({sd.OffFrontN}) 전체 {(sd.OffAllN > 0 ? sd.OffAllIn / sd.OffAllN : 0):0.000} ({sd.OffAllN})");
            sb.AppendLine("[지휘별] " + string.Join(" ", "ONL".Select(k => $"{k}: 병사-초 {sd.ClsSec.GetValueOrDefault(k):0} 사망 {sd.ClsDeath.GetValueOrDefault(k)} 사망/천병사-초 {(sd.ClsSec.GetValueOrDefault(k) > 0 ? sd.ClsDeath.GetValueOrDefault(k) * 1000.0 / sd.ClsSec[k] : 0):0.00} 평균제압 {(sd.ClsSec.GetValueOrDefault(k) > 0 ? sd.ClsSupp.GetValueOrDefault(k) / sd.ClsSec[k] : 0):0.000}")));
            static string Pct(List<int> l) { if (l.Count == 0) return "-"; var o = l.OrderBy(q => q).ToList(); return $"평균 {l.Average():0.00} p50 {o[o.Count / 2]} p75 {o[o.Count * 3 / 4]} p90 {o[o.Count * 9 / 10]}"; }
            sb.AppendLine($"[대열2] 앞줄 슬롯 {Pct(sd.RankSlots)} | 구멍 {Pct(sd.RankHoles)} | 연속 구멍 {Pct(sd.RankHoleGap)} | 뒤처짐 {Pct(sd.RankBehind)} | 판단 문턱 넘음 {sd.RankBroken}/{sd.RankHoles.Count}");
            sb.AppendLine("[대열진단] " + string.Join(", ", sd.RankDiag.Select(k => $"{k.Key}={k.Value}")));
            sb.AppendLine($"[사거리] 보병 사격 중 표적 거리: 표본 {sd.Rng.Count}, 평균 {(sd.RngN > 0 ? sd.RngSum / sd.RngN : 0):0.0}, 중앙 {Med(sd.Rng.Select(q => (double)q).ToList()):0.0}, 20칸 이상 {sd.Rng.Count(q => q >= 20)}, 10칸 미만 {sd.Rng.Count(q => q < 10)}");
            sb.AppendLine($"[기병] 돌격 {x.CavN}회: 표적 25칸 안 사격 보병 15초 안 최대 >=10 {x.CavShoot10}, 보병 돌격 ±15초 동시 {x.CavAssaultNear}");
            foreach (var l in x.CavLog.Take(25)) sb.AppendLine("  " + l);
        }
        sb.AppendLine("생존 곡선(30초): " + string.Join(" ", sd.Curve.Select(c => $"{c.t:0}:{c.alive}")));
        sb.AppendLine("사건 로그:");
        foreach (var l in sd.Log.Take(150)) sb.AppendLine("  " + l);
    }
}
