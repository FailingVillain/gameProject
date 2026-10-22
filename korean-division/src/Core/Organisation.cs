using System.Numerics;

namespace KoreanDivision.Core;

/// <summary>대대 대형 (중대 블록 배치). 중대 대형(병사 배치)은 Company.Form (FormType) — ROADMAP §2-4b '대형 2단계 분리'.</summary>
public enum BatForm { Line = 0, Column = 1, Echelon = 2, TwoFrontReserve = 3, Waves = 4 }

/// <summary>
/// 중대 (소부대, ROADMAP §2-4b 부대 편제 2단계): 최대 100기, 유닛은 한 중대에만. 시뮬레이션 상태 (명령으로만 바뀌고 저장 · 체크섬에 들어간다).
/// 번호 = 세력 안에서 비어 있는 가장 작은 번호 (제N중대). 분대 (≤12, 고정 편성 — 자동 합치기 · 재편성만) 로 나뉜다 (Squads.cs). 빈자리는 장교 점검이 메운다.
/// </summary>
public sealed partial class Company
{
    public const int MaxSize = 100;
    public int Id { get; internal set; }
    public int Owner { get; internal set; }
    public int Number { get; internal set; }
    /// <summary>중대 대형 (병사 배치). Free · Wedge 외 격자형 — Free 는 Box 로 취급.</summary>
    public FormType Form { get; internal set; } = FormType.Line;
    /// <summary>중대 전술 틀 (역할군 배치, CombatFormations.cs). None = 중대 대형으로 전체 배치.</summary>
    public CompanyTactic Tactic { get; internal set; }
    /// <summary>실제로 실행 중인 틀 = Tactic 을 지휘 능력으로 거른 것 (부사관 대리 = 화력-기동, 지휘 없음 = 없음; 인계 중에는 그대로). 배치 · 행동이 쓴다 (TacticDrill.cs). 저장 · 체크섬.</summary>
    public CompanyTactic ActiveTactic { get; internal set; }
    /// <summary>틀 2단계 행동 상태 (TacticDrill.cs). 저장 · 체크섬.</summary>
    internal readonly DrillState Drill = new();
    /// <summary>[조회] 틀 행동 국면 (Idle = 행동 없음).</summary>
    public DrillPhase DrillPhase => Drill.Phase;
    /// <summary>편성 당시 병력 (카드의 병력 막대 분모).</summary>
    public int InitialStrength { get; internal set; }
    /// <summary>마지막 이동의 도착 방향 (0 = 아직 없음). 다음 명령의 '현재 방향 유지' 기준.</summary>
    public Vector2 Facing { get; internal set; }
    /// <summary>구성원 (Id 오름차순).</summary>
    public readonly List<Unit> Members = new();
    public Battalion? Battalion { get; internal set; }
    /// <summary>분대 (번호 순, 장교 제외 구성원 전원이 정확히 한 분대에). Squads.cs.</summary>
    public readonly List<Squad> Squads = new();
    /// <summary>정찰조 (Id 오름차순, Members 와 별개 — 분대 · 대형 밖, 자율 행동은 Scouts.cs).</summary>
    public readonly List<Unit> Scouts = new();
    /// <summary>마지막 대형 배치 이후 빠진 구성원 수 (장교 빈자리 점검 기준, Squads.cs StepGapChecks).</summary>
    internal int Holes;
    /// <summary>길목 통과 큐 (Choke.cs): 앞 중대가 길목을 빠지기를 기다리며 선 연속 틱 수. 저장 · 체크섬.</summary>
    internal int ChokeHold;
    /// <summary>장교가 명령한 행진 사격이 이 틱 전까지 유지 (MarchCommand.cs, 0 = 아님).</summary>
    internal long MarchUntil;
    /// <summary>돌격 사격 허용 (중대 명령 카드 토글, 기본 꺼짐 — 켜면 공격 이동 중 적이 보일 때만 장교가 판단, MarchCommand.cs). 저장 · 체크섬.</summary>
    internal bool MarchFireOn;
    /// <summary>돌격 사격 켠 중대의 지휘 장교가 건재한 동안 (이 틱 전까지) 보병 구성원은 공격 이동 중 표적을 잡아도 멈춰 서지 않고 전진한다 — 적이 행진 사격 반경에 들면 장교가 행진 사격을 명령 (MarchCommand.cs). 저장 · 체크섬.</summary>
    internal long MarchAdvUntil;
    /// <summary>행군 중인 깃발 (없으면 null).</summary>
    internal CompanyAnchor? Anchor;
    /// <summary>중대 지휘관 (장교, 없으면 null — 부사관 대리 · 지휘 없음). 서열 = 계급 → 경험치 → Id, 대대장은 다른 장교가 없을 때만 (CommandRanks.cs). 저장 · 체크섬.</summary>
    public Unit? Commander { get; internal set; }
    /// <summary>지휘관을 잃은 뒤 인계가 끝나는 틱 (그 전까지 지휘 능력 None). 저장 · 체크섬.</summary>
    internal long HandoverUntil;
    /// <summary>대열 정비 (CloseRanks.cs): 장교의 '대열이 크게 흐트러짐' 경보 (히스테리시스 — 문턱 위에서 켜지고 문턱 × closeHysteresis 아래에서 꺼짐). 저장 · 체크섬.</summary>
    internal bool CloseAlarm;
    /// <summary>대열 정비 명령 틱 (장교 · 플레이어, 0 = 없음 — 병사 지연 · 장교 재명령 간격 기준) · 명령이 유효한 마지막 틱 (0 = 없음). 저장 · 체크섬.</summary>
    internal long CloseAt, CloseUntil;
    /// <summary>중대 자세 (tweak1, CoverSeek.cs): 피격 엄폐 찾기 여부 — 전술 AI 가 SetPostureCommand 로 지정. 저장 · 체크섬.</summary>
    public CompanyPosture Posture { get; internal set; }
    /// <summary>F1 접적 (Contact.cs): 마지막 접적 트리거 틱 · 이번 접적이 시작된 틱. 저장 · 체크섬.</summary>
    internal long ContactTick = -1_000_000, ContactSince = -1_000_000;
    /// <summary>F1 2차 (ContactF2.cs): 대형 유지 — 접적 중에도 이동 명령을 깃발 대형으로 (선까지 전진 안 함). 플레이어 SetKeepFormationCommand. 저장 · 체크섬.</summary>
    public bool KeepFormation { get; internal set; }
    /// <summary>집결 지점 (N79a, Rally.cs): 플레이어 · 전술 AI 가 SetRallyCommand 로 정한 후퇴 위치. 저장 · 체크섬.</summary>
    public bool HasRally { get; internal set; }
    public Vector2 RallyPoint { get; internal set; }

    /// <summary>[표시] 깃발이 행군 중인가 · 깃발 위치.</summary>
    public bool Marching => Anchor != null && !Anchor.Arrived;
    public Vector2? FlagPos => Anchor?.Pos;
    public bool FlagColumn => Anchor?.Column ?? false;
}

/// <summary>대대 (대부대): 중대 최대 10개. 번호 1~9 (0 = 번호 없음 — 0 목록으로만 고른다).</summary>
public sealed class Battalion
{
    public const int MaxCompanies = 10;
    public int Id { get; internal set; }
    public int Owner { get; internal set; }
    public int Number { get; internal set; }
    public BatForm Form { get; internal set; } = BatForm.Line;
    public readonly List<Company> Companies = new();
    /// <summary>대대장 (대대 안 최고 서열 장교, CommandRanks.cs). 저장 · 체크섬.</summary>
    public Unit? Commander { get; internal set; }
    /// <summary>대대장을 잃은 뒤 인계가 끝나는 틱. 저장 · 체크섬.</summary>
    internal long HandoverUntil;
}

/// <summary>
/// 중대 깃발 (가상 기준점): 경로를 하나만 계산해 따라가고, 구성원은 깃발 기준 슬롯(회전)을 따라간다 (OrgFormation.cs).
/// Flag 는 World.Units 에 없는 가상 유닛 — 기존 MoveToward/RouteDirection 을 그대로 재사용한다.
/// </summary>
internal sealed class CompanyAnchor
{
    public Vector2 Pos, Heading, Goal, FinalDir;
    public bool Arrived, Column;
    /// <summary>대열 정비 깃발 (gapfill2, CloseRanks.cs): 공격 이동 병사가 표적에 멈추지 않고 슬롯까지 걸어감 (행진 사격). 저장 = ORG_ 끝 WriteCloseRanks (FormatVersion 28).</summary>
    public bool Close;
    public int ClearTicks;          // 종대 모드에서 길이 연속으로 트인 틱 수
    public int WaitTicks;           // 낙오자를 기다린 연속 틱 수 (상한 넘으면 느리게라도 출발)
    public float HalfWidth, Spacing;
    /// <summary>깃발 속도 배수 (0~1, 가속 제한으로 목표값을 따라감 — OrgFormation.StepAnchors). 새 깃발 = 0 (정지에서 출발).</summary>
    public float SpeedMul;
    /// <summary>유닛 Id → 로컬 오프셋 (X = 오른쪽, Y = 앞). 행군 대형 · 좁은 길 종대.</summary>
    public readonly Dictionary<int, Vector2> Slots = new(), ColumnSlots = new();
    /// <summary>유닛 Id → 도착 세계 오프셋 (Goal 기준, 설 수 있는 칸으로 옮김).</summary>
    public readonly Dictionary<int, Vector2> FinalOff = new();
    /// <summary>깃발이 지나온 자취 (오래된 → 최신, 0.5칸 간격). 종대 병사가 이 위에 선다.</summary>
    public readonly List<Vector2> Trail = new();
    /// <summary>[작업값 — 저장 안 함] 종대 줄별 자취 위 위치 · 접선. 매 틱 StepAnchors 가 Trail 에서 다시 계산한다.</summary>
    public Vector2[] RowPos = Array.Empty<Vector2>(), RowTan = Array.Empty<Vector2>();
    /// <summary>[작업값 — 저장 안 함] 자취 · 외삽으로 닿는 줄 수. 이 줄부터는 마지막 닿는 점 둘레로 흩어 선다 (OrgFormation.UpdateColumnRows · MarchSlot).</summary>
    public int RowReach = int.MaxValue;
    public MoveGroup Group = null!;
    public Unit Flag = null!;
}

public sealed partial class Unit
{
    /// <summary>소속 중대 (없으면 null). Company.Members 의 역참조 — ORG_ 구역에서 구성원 목록으로 복원.</summary>
    public Company? Company { get; internal set; }
}

/// <summary>편제 명령 (WorldCommands.Execute 가 실행). BattalionNumber: Alt+숫자 — 그 번호의 대대에 넣는다 (없으면 그 번호로 새 대대, 0 = 대대 없음).</summary>
public sealed record FormCompanyCommand(int Faction, int[] Units, int BattalionNumber = 0) : GameCommand(Faction);
public sealed record DisbandCompanyCommand(int Faction, int CompanyId) : GameCommand(Faction);
/// <summary>분대 단위로 앞 절반 (분대 번호 순) 유지, 뒤 절반은 새 중대 (같은 대대에 자리가 있으면 편입). 장교도 절반씩. 분대가 1개 이하면 Id 순 절반 + 분대 다시 나눔.</summary>
public sealed record SplitCompanyCommand(int Faction, int CompanyId) : GameCommand(Faction);
/// <summary>중대들을 하나의 대대로 (기존 대대에서 빼낸 뒤). 번호 = 비어 있는 1~9, 없으면 0.</summary>
public sealed record FormBattalionCommand(int Faction, int[] Companies) : GameCommand(Faction);
/// <summary>대대 번호 변경 (1~9, 이미 쓰는 번호면 교환).</summary>
public sealed record SetBattalionNumberCommand(int Faction, int BattalionId, int Number) : GameCommand(Faction);
/// <summary>중대를 다른 대대로 (BattalionId 0 = 대대에서 빼기). 대상이 10개면 거부.</summary>
public sealed record MoveCompanyCommand(int Faction, int CompanyId, int BattalionId) : GameCommand(Faction);
public sealed record SetCompanyFormCommand(int Faction, int[] Companies, int Form) : GameCommand(Faction);
/// <summary>중대 돌격 사격 켜기 / 끄기.</summary>
public sealed record SetMarchFireCommand(int Faction, int[] Companies, bool On) : GameCommand(Faction);
public sealed record SetBattalionFormCommand(int Faction, int BattalionId, int Form) : GameCommand(Faction);

/// <summary>부대 편제 (중대 · 대대) — 소속 규칙 · 명령 실행 · 조회 · 저장 · 체크섬. 깃발 이동은 OrgFormation.cs.</summary>
public sealed partial class World
{
    public readonly List<Company> Companies = new();
    public readonly List<Battalion> Battalions = new();
    int nextCompanyId = 1, nextBattalionId = 1;

    // ---------------------------------------------------------------- 조회 (UI · AI)

    public IEnumerable<Company> CompaniesOf(int faction) => Companies.Where(c => c.Owner == faction);
    public IEnumerable<Battalion> BattalionsOf(int faction) => Battalions.Where(r => r.Owner == faction);
    public Battalion? BattalionByNumber(int faction, int number) => number <= 0 ? null : Battalions.FirstOrDefault(r => r.Owner == faction && r.Number == number);
    public Company? FindCompany(int id) => Companies.FirstOrDefault(c => c.Id == id);
    public Battalion? FindBattalion(int id) => Battalions.FirstOrDefault(r => r.Id == id);

    // ---------------------------------------------------------------- 공개 API (검증 + 대기열)

    public void FormCompany(int faction, IEnumerable<Unit> units, int battalionNumber = 0) =>
        IssueUnits(faction, units, ids => new FormCompanyCommand(faction, ids, Math.Clamp(battalionNumber, 0, 9)));
    public void DisbandCompany(int faction, Company c) { if (c.Owner == faction) Issue(new DisbandCompanyCommand(faction, c.Id)); }
    public void SplitCompany(int faction, Company c) { if (c.Owner == faction && c.Members.Count >= 2) Issue(new SplitCompanyCommand(faction, c.Id)); }
    public void FormBattalion(int faction, IEnumerable<Company> companies)
    {
        var ids = companies.Where(c => c.Owner == faction).Select(c => c.Id).Distinct().ToArray();
        if (ids.Length > 0) Issue(new FormBattalionCommand(faction, ids));
    }
    public void SetBattalionNumber(int faction, Battalion r, int number) { if (r.Owner == faction && number >= 1 && number <= 9) Issue(new SetBattalionNumberCommand(faction, r.Id, number)); }
    public void MoveCompany(int faction, Company c, Battalion? r) { if (c.Owner == faction && (r == null || r.Owner == faction)) Issue(new MoveCompanyCommand(faction, c.Id, r?.Id ?? 0)); }
    public void SetCompanyForm(int faction, IEnumerable<Company> companies, FormType form)
    {
        var ids = companies.Where(c => c.Owner == faction).Select(c => c.Id).Distinct().ToArray();
        if (ids.Length > 0) Issue(new SetCompanyFormCommand(faction, ids, (int)form));
    }
    /// <summary>units 의 중대들에 돌격 사격 허용을 맞춘다 (이미 같으면 명령 없음).</summary>
    public void SetMarchFire(int faction, IEnumerable<Unit> units, bool on)
    {
        var ids = units.Select(u => u.Company).Where(c => c != null && c.Owner == faction && c.MarchFireOn != on).Select(c => c!.Id).Distinct().OrderBy(i => i).ToArray();
        if (ids.Length > 0) Issue(new SetMarchFireCommand(faction, ids, on));
    }
    public void SetBattalionForm(int faction, Battalion r, BatForm form) { if (r.Owner == faction) Issue(new SetBattalionFormCommand(faction, r.Id, (int)form)); }

    // ---------------------------------------------------------------- 실행

    /// <summary>편제 명령이면 실행하고 true (Execute 의 default 분기에서 부른다).</summary>
    bool ExecuteOrganisation(GameCommand command)
    {
        int f = command.Faction;
        switch (command)
        {
            case FormCompanyCommand c: ExecFormCompany(f, Resolve(f, c.Units), c.BattalionNumber); return true;
            case DisbandCompanyCommand c: if (OwnCompany(f, c.CompanyId) is { } dc) DisbandCompanyCore(dc); return true;
            case SplitCompanyCommand c: if (OwnCompany(f, c.CompanyId) is { } sc) ExecSplit(sc); return true;
            case FormBattalionCommand c: ExecFormBattalion(f, c.Companies); return true;
            case SetBattalionNumberCommand c: if (OwnBattalion(f, c.BattalionId) is { } nr && c.Number >= 1 && c.Number <= 9) ExecRenumber(nr, c.Number); return true;
            case MoveCompanyCommand c:
                if (OwnCompany(f, c.CompanyId) is { } mc)
                {
                    if (c.BattalionId == 0) { DetachCompany(mc); return true; }
                    if (OwnBattalion(f, c.BattalionId) is { } mr && mr != mc.Battalion && mr.Companies.Count < Battalion.MaxCompanies) { DetachCompany(mc); AttachCompany(mc, mr); }
                    else if (OwnBattalion(f, c.BattalionId) is { } full && full != mc.Battalion) Notify(f, CommandResult.NotAllowed);
                }
                return true;
            case SetCompanyFormCommand c:
                if (!Enum.IsDefined(typeof(FormType), c.Form)) return true;
                foreach (int id in c.Companies)
                    if (OwnCompany(f, id) is { } fc)
                    {
                        if (FormationRules.IsEchelon((FormType)c.Form) && !FormationRules.CavalryOnly(fc.Members)) { Notify(f, CommandResult.NotAllowed); continue; }   // 사선 = 기병 중대만
                        fc.Form = (FormType)c.Form;
                    }
                return true;
            case SetMarchFireCommand c:
                foreach (int id in c.Companies) if (OwnCompany(f, id) is { } mf) { mf.MarchFireOn = c.On; if (!c.On) { mf.MarchUntil = 0; mf.MarchAdvUntil = 0; } }
                return true;
            case SetBattalionFormCommand c:
                if (OwnBattalion(f, c.BattalionId) is { } fr && Enum.IsDefined(typeof(BatForm), c.Form)) fr.Form = (BatForm)c.Form;
                return true;
        }
        return ExecuteFormationCommand(command);   // 전투 대형 · 역할 (CombatFormations.cs)
    }

    Company? OwnCompany(int f, int id) => FindCompany(id) is { } c && c.Owner == f ? c : null;
    Battalion? OwnBattalion(int f, int id) => FindBattalion(id) is { } r && r.Owner == f ? r : null;

    /// <summary>중대 편성. spawn = 배치 단계 (시작 병력 · 미션 병력): 인계 지연 없음, Auto 면 대대장 직책 계급을 정식으로 · Explicit (미션이 계급 지정) 이면 임시 진급 없이 (CommandRanks.cs).</summary>
    void ExecFormCompany(int f, List<Unit> units, int regNumber, SpawnRanks spawn = SpawnRanks.None)
    {
        var list = units.Distinct().OrderBy(u => u.Id).ToList();
        // N12: 정찰병 묶음에는 장교 없음 — 장교 · 정찰병만 고른 편성 (관측반 등) 은 장교를 빼고 정찰병만으로 (장교는 소속 그대로 · 낱으로 남는다).
        if (list.Exists(u => u.Def.Scout) && list.TrueForAll(u => u.Def.Scout || u.IsOfficer)) list = list.Where(u => u.Def.Scout).ToList();
        // 정찰병은 중대 구성원이 아니라 정찰조 (분대 밖). 구성원이 한 명도 없으면 (정찰병뿐) 예전처럼 평범한 중대.
        var scouts = list.Where(u => u.Def.Scout).Take(Math.Max(0, Defs.ScoutAi.PerCompany)).ToList();
        if (list.Exists(u => !u.Def.Scout)) list = list.Where(u => !u.Def.Scout).ToList(); else scouts.Clear();
        list = list.Take(Company.MaxSize).ToList();
        if (list.Count == 0) return;
        foreach (var u in list) LeaveCompany(u);
        foreach (var u in scouts) LeaveCompany(u);
        var c = new Company { Id = nextCompanyId++, Owner = f, Number = FreeCompanyNumber(f), InitialStrength = list.Count };
        foreach (var u in list) { c.Members.Add(u); u.Company = c; }
        c.MarchFireOn = list.Count(u => u.Def.MarchFireDefault) * 2 > list.Count(u => !u.IsOfficer);   // 돌격보병 중대는 돌격 사격 기본 ON (N21)
        foreach (var u in scouts) AttachScout(c, u);
        BuildSquads(c);   // 분대 (≤12, 공간 순)
        AssignRoles(c);   // 역할군 자동 배정 (CombatFormations.cs)
        AssignCompanyRanks(c);   // 계급 미정 장교 = 직책 계급 (대위 · 중위 · 소위, CommandRanks.cs)
        // 같은 깃발 그룹으로 행군 중이 아니므로 방향은 아직 없음 — 다음 이동 명령이 정한다
        Companies.Add(c);
        ResolveCompanyCommand(c);
        if (regNumber >= 1 && regNumber <= 9)
        {
            var r = BattalionByNumber(f, regNumber);
            if (r == null) { r = new Battalion { Id = nextBattalionId++, Owner = f, Number = regNumber }; Battalions.Add(r); }
            if (r.Companies.Count < Battalion.MaxCompanies) AttachCompany(c, r);
            else { RemoveEmptyBattalion(r); Notify(f, CommandResult.NotAllowed); }
        }
        if (spawn != SpawnRanks.None) ConfirmSpawnPosts(c, spawn == SpawnRanks.Auto);
    }

    int FreeCompanyNumber(int f)
    {
        var used = new HashSet<int>(Companies.Where(c => c.Owner == f).Select(c => c.Number));
        int n = 1;
        while (used.Contains(n)) n++;
        return n;
    }

    int FreeBattalionNumber(int f, Battalion? except = null)
    {
        for (int n = 1; n <= 9; n++)
            if (!Battalions.Any(r => r.Owner == f && r.Number == n && r != except)) return n;
        return 0;
    }

    void ExecSplit(Company c)
    {
        int n = c.Members.Count;
        if (n < 2) return;
        var nc = new Company { Id = nextCompanyId++, Owner = c.Owner, Number = FreeCompanyNumber(c.Owner), Form = c.Form, Facing = c.Facing };
        List<Unit> moved;
        if (c.Squads.Count >= 2)
        {
            // 분대째로: 앞 절반 분대 유지 (번호 순), 뒤 절반은 새 중대로 (분대 구성 · 번호 그대로). 장교는 Id 순 앞 절반 유지.
            int keepSq = (c.Squads.Count + 1) / 2;
            var movedSquads = c.Squads.Skip(keepSq).ToList();
            c.Squads.RemoveRange(keepSq, c.Squads.Count - keepSq);
            var offs = c.Members.Where(u => u.IsOfficer).ToList();
            moved = movedSquads.SelectMany(q => q.Members).Concat(offs.Skip((offs.Count + 1) / 2)).OrderBy(u => u.Id).ToList();
            foreach (var q in movedSquads) { q.Company = nc; nc.Squads.Add(q); }
            var set = new HashSet<Unit>(moved);
            c.Members.RemoveAll(set.Contains);
            foreach (var u in moved) { nc.Members.Add(u); u.Company = nc; }
        }
        else
        {
            int keep = (n + 1) / 2;
            moved = c.Members.Skip(keep).ToList();
            c.Members.RemoveRange(keep, n - keep);
            foreach (var u in moved) { nc.Members.Add(u); u.Company = nc; }
            BuildSquads(c);
            BuildSquads(nc);
        }
        nc.Tactic = c.Tactic;
        nc.ActiveTactic = c.ActiveTactic;
        AssignRoles(c);
        AssignRoles(nc);
        nc.InitialStrength = moved.Count;
        c.InitialStrength = Math.Max(c.Members.Count, c.InitialStrength - moved.Count);
        Companies.Add(nc);
        ResolveCompanyCommand(c);    // 지휘관이 새 중대로 갔으면 남은 중대는 승계 (인계 지연)
        ResolveCompanyCommand(nc);
        if (c.Battalion is { } r)
        {
            if (r.Companies.Count < Battalion.MaxCompanies) AttachCompany(nc, r);
            else ResolveBattalionCommand(r);
        }
    }

    void ExecFormBattalion(int f, int[] ids)
    {
        var list = ids.Distinct().Select(id => OwnCompany(f, id)).Where(c => c != null).Select(c => c!).OrderBy(c => c.Id).Take(Battalion.MaxCompanies).ToList();
        if (list.Count == 0) return;
        foreach (var c in list) DetachCompany(c);
        var r = new Battalion { Id = nextBattalionId++, Owner = f };
        r.Number = FreeBattalionNumber(f);
        Battalions.Add(r);
        foreach (var c in list) AttachCompany(c, r);
    }

    void ExecRenumber(Battalion r, int number)
    {
        if (r.Number == number) return;
        var other = BattalionByNumber(r.Owner, number);
        if (other != null) other.Number = r.Number;   // 교환 (r 이 번호 없음이면 상대도 번호 없음)
        r.Number = number;
    }

    void AttachCompany(Company c, Battalion r)
    {
        c.Battalion = r;
        r.Companies.Add(c);
        r.Companies.Sort((a, b) => a.Id.CompareTo(b.Id));
        ResolveBattalionCommand(r);   // 대대장 · 중대 지휘관 (CommandRanks.cs)
    }

    /// <summary>중대를 대대에서 뺀다 (빈 대대는 즉시 제거).</summary>
    void DetachCompany(Company c)
    {
        if (c.Battalion is not { } r) return;
        r.Companies.Remove(c);
        c.Battalion = null;
        RemoveEmptyBattalion(r);
        if (r.Companies.Count > 0) ResolveBattalionCommand(r);
        if (Companies.Contains(c)) ResolveCompanyCommand(c);
    }

    void RemoveEmptyBattalion(Battalion r)
    {
        if (r.Companies.Count == 0) Battalions.Remove(r);
    }

    void DisbandCompanyCore(Company c)
    {
        c.Commander = null;
        foreach (var u in c.Members) { u.Company = null; u.Squad = null; u.Role = UnitRole.None; }
        c.Squads.Clear();
        c.Members.Clear();
        ReleaseScouts(c, true);
        RemoveCompany(c);
    }

    void RemoveCompany(Company c)
    {
        ReleaseScouts(c, false);
        c.Anchor = null;
        DetachCompany(c);
        Companies.Remove(c);
    }

    /// <summary>유닛을 소속 중대에서 뺀다 (사망 · 세력 이전 · 재편성). 빈 중대 · 빈 대대는 즉시 제거. 자동 재편성 없음.</summary>
    internal void LeaveCompany(Unit u)
    {
        DetachScout(u);   // 정찰조에서도 뺀다 (Scouts.cs)
        if (u.Company is not { } c) return;
        var bat = c.Battalion;
        LeaveSquad(u);   // 분대 자동 합치기 · 재편성 (Squads.cs)
        c.Members.Remove(u);
        u.Company = null;
        u.Role = UnitRole.None;
        if (c.Facing != Vector2.Zero) c.Holes++;   // 대형의 빈자리 (장교 점검)
        if (c.Anchor is { } a) { a.Slots.Remove(u.Id); a.ColumnSlots.Remove(u.Id); a.FinalOff.Remove(u.Id); }
        if (c.Members.Count == 0) RemoveCompany(c);
        OnCommandMemberLeft(u, c, bat);   // 지휘관 · 대대장 승계 (CommandRanks.cs)
    }

    // ---------------------------------------------------------------- 저장 (ORG_ 구역) · 체크섬

    /// <summary>ORG_ 구역 쓰기. WriteLinks 뒤 · WriteGroups 앞에 불러야 깃발의 그룹 · 경로 참조가 표에 번호를 받는다.</summary>
    void WriteOrganisation(SnapWriter w, SnapCtx ctx)
    {
        w.I(nextCompanyId); w.I(nextBattalionId); w.I(nextSquadId);
        w.I(Battalions.Count);
        foreach (var r in Battalions) { w.I(r.Id); w.I(r.Owner); w.I(r.Number); w.I((int)r.Form); }
        w.I(Companies.Count);
        foreach (var c in Companies)
        {
            w.I(c.Id); w.I(c.Owner); w.I(c.Number); w.I((int)c.Form); w.I(c.InitialStrength); w.Vec(c.Facing);
            w.I(c.Battalion?.Id ?? 0);
            w.I(c.Members.Count);
            foreach (var u in c.Members) w.I(u.Id);
            w.I(c.Holes);
            w.L(c.MarchUntil); w.Bool(c.MarchFireOn); w.L(c.MarchAdvUntil);
            w.I(c.Squads.Count);
            foreach (var q in c.Squads)
            {
                w.I(q.Id); w.I(q.Number); w.I(q.Members.Count);
                foreach (var u in q.Members) w.I(u.Id);
                w.L(q.MarchUntil);
            }
            w.Bool(c.Anchor != null);
            if (c.Anchor is { } a) WriteAnchor(w, ctx, a);
        }
        WriteScouts(w);   // 정찰조 · 자율 상태 (Scouts.cs, FormatVersion 16)
        WriteFormationState(w);   // 전술 틀 · 역할 · 분대 대형 (CombatFormations.cs, FormatVersion 22)
        WriteCommandState(w);     // 지휘관 · 인계 (CommandRanks.cs, FormatVersion 23)
        WriteCloseRanks(w);       // 대열 정비 경보 · 명령 (CloseRanks.cs, FormatVersion 27)
        WritePostures(w);         // 중대 자세 (CoverSeek.cs, FormatVersion 34 — tweak1)
        WriteContact(w);          // F1 접적 · 구역 · 사격 위치 (Contact.cs, FormatVersion 37)
        WriteFireControl(w);      // N73 사격 통제 (FireControl.cs, FormatVersion 38)
        WriteProps(w);            // F6 소품 상태 (Props.cs, FormatVersion 42)
        WriteTrenchAssault(w);    // N32 참호 돌격 상태 (TrenchAssault.cs, FormatVersion 43)
        WriteRally(w);            // N79a 집결 지점 · 포격 종류 (Rally.cs, FormatVersion 43)
        WriteChoke(w);            // 길목 통과 큐 (Choke.cs, FormatVersion 45)
        WriteComms(w);            // 통신 · 난이도 (Comms.cs, comms)
        WriteTrains(w);           // 열차 (Train.cs, T30a, FormatVersion 58)
    }

    static void WriteSlots(SnapWriter w, Dictionary<int, Vector2> d)
    {
        var list = d.OrderBy(kv => kv.Key).ToList();
        w.I(list.Count);
        foreach (var (k, v) in list) { w.I(k); w.Vec(v); }
    }

    static void ReadSlots(SnapReader r, Dictionary<int, Vector2> d)
    {
        d.Clear();
        int n = r.Count(3);
        for (int i = 0; i < n; i++) d[r.I()] = r.Vec();
    }

    void WriteAnchor(SnapWriter w, SnapCtx ctx, CompanyAnchor a)
    {
        w.Vec(a.Pos); w.Vec(a.Heading); w.Vec(a.Goal); w.Vec(a.FinalDir);
        w.Bool(a.Arrived); w.Bool(a.Column); w.I(a.ClearTicks); w.I(a.WaitTicks); w.F(a.HalfWidth); w.F(a.Spacing); w.F(a.SpeedMul);
        WriteSlots(w, a.Slots); WriteSlots(w, a.ColumnSlots); WriteSlots(w, a.FinalOff);
        w.I(a.Trail.Count);
        foreach (var p in a.Trail) w.Vec(p);
        w.I(ctx.GroupId(a.Group));
        // 깃발 유닛: 길찾기 상태만 (전투 · 보급 상태는 쓰지 않음 — 월드 밖 가상 유닛)
        var u = a.Flag;
        w.I(u.Id); w.Str(u.Def.Key); w.Vec(u.Pos); w.Vec(u.Velocity); w.F(u.Facing);
        byte tok = u.NavToken == null ? (byte)0 : ReferenceEquals(u.NavToken, a.Group) ? (byte)1 : (byte)6;
        w.Byte(tok);
        w.I(ctx.RouteId(u.NavRoute));
        w.I(u.NavSeg); w.I(u.NavEpoch); w.I(u.NavRawX); w.I(u.NavRawY); w.I(u.NavGX); w.I(u.NavGY);
        w.L(u.NextLosTick); w.L(u.NextPersonalTick); w.Bool(u.LosOk); w.Vec(u.LosGoal);
        w.Byte(u.NavKind);
        w.Vec(u.ProgPos); w.L(u.ProgTick); w.L(u.ProgLast); w.L(u.NudgeUntil); w.I(u.StuckLevel); w.Vec(u.NudgeDir);
    }

    void ReadOrganisation(SnapReader r, SnapCtx ctx, Defs defs)
    {
        Companies.Clear(); Battalions.Clear();
        nextCompanyId = r.I(); nextBattalionId = r.I(); nextSquadId = r.I();
        int nr = r.Count(4);
        for (int i = 0; i < nr; i++)
        {
            var rg = new Battalion { Id = r.I(), Owner = r.I(), Number = r.I() };
            int form = r.I();
            if (!Enum.IsDefined(typeof(BatForm), form)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "battalion form"));
            rg.Form = (BatForm)form;
            Battalions.Add(rg);
        }
        int nc = r.Count(8);
        for (int i = 0; i < nc; i++)
        {
            var c = new Company { Id = r.I(), Owner = r.I(), Number = r.I() };
            int form = r.I();
            if (!Enum.IsDefined(typeof(FormType), form)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company form"));
            c.Form = (FormType)form;
            c.InitialStrength = r.I();
            c.Facing = r.Vec();
            int rid = r.I();
            if (rid != 0)
            {
                var rg = Battalions.FirstOrDefault(x => x.Id == rid) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "company battalion"));
                c.Battalion = rg;
                rg.Companies.Add(c);
            }
            int nm = r.Count(1);
            for (int k = 0; k < nm; k++)
            {
                int uid = r.I();
                if (FindAny(uid) is not Unit u || u.Company != null) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company member " + uid));
                c.Members.Add(u);
                u.Company = c;
            }
            c.Holes = r.I();
            if (c.Holes < 0) throw new SnapshotException(string.Format(SnapMsg.Invalid, "company holes"));
            c.MarchUntil = r.L(); c.MarchFireOn = r.Bool(); c.MarchAdvUntil = r.L();
            int nq = r.Count(3);
            for (int q = 0; q < nq; q++)
            {
                var sq = new Squad { Id = r.I(), Number = r.I(), Company = c };
                int nsm = r.Count(1);
                for (int k = 0; k < nsm; k++)
                {
                    int uid = r.I();
                    if (FindAny(uid) is not Unit u || u.Company != c || u.Squad != null) throw new SnapshotException(string.Format(SnapMsg.Invalid, "squad member " + uid));
                    sq.Members.Add(u);
                    u.Squad = sq;
                }
                sq.MarchUntil = r.L();
                c.Squads.Add(sq);
            }
            if (r.Bool()) c.Anchor = ReadAnchor(r, ctx, defs, c);
            Companies.Add(c);
        }
        foreach (var rg in Battalions) rg.Companies.Sort((a, b) => a.Id.CompareTo(b.Id));
        ReadScouts(r);
        ReadFormationState(r);
        ReadCommandState(r);
        ReadCloseRanks(r);
        ReadPostures(r);
        ReadContact(r);
        ReadFireControl(r);
        ReadProps(r);
        ReadTrenchAssault(r);
        ReadRally(r);
        ReadChoke(r);
        ReadComms(r);
        ReadTrains(r);
    }

    CompanyAnchor ReadAnchor(SnapReader r, SnapCtx ctx, Defs defs, Company c)
    {
        var a = new CompanyAnchor { Pos = r.Vec(), Heading = r.Vec(), Goal = r.Vec(), FinalDir = r.Vec() };
        a.Arrived = r.Bool(); a.Column = r.Bool(); a.ClearTicks = r.I(); a.WaitTicks = r.I(); a.HalfWidth = r.F(); a.Spacing = r.F(); a.SpeedMul = r.F();
        if (!(a.SpeedMul >= 0 && a.SpeedMul <= 1)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "anchor speed"));
        ReadSlots(r, a.Slots); ReadSlots(r, a.ColumnSlots); ReadSlots(r, a.FinalOff);
        int nt = r.Count(2);
        for (int i = 0; i < nt; i++) a.Trail.Add(r.Vec());
        a.Group = ctx.ReadGroup(r) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "anchor group"));
        int id = r.I();
        string key = r.Str() ?? "";
        if (!defs.Units.TryGetValue(key, out var ud)) throw new SnapshotException(string.Format(SnapMsg.MissingDef, SnapMsg.DefUnit, key));
        var u = new Unit(id, ud, c.Owner, r.Vec());
        u.Velocity = r.Vec(); u.Facing = r.F();
        u.Group = a.Group;
        byte tok = r.Byte();
        u.NavToken = tok switch { 0 => null, 1 => a.Group, 6 => new object(), _ => throw new SnapshotException(string.Format(SnapMsg.Invalid, "flag nav token")) };
        u.NavRoute = ctx.ReadRoute(r);
        u.NavSeg = r.I(); u.NavEpoch = r.I(); u.NavRawX = r.I(); u.NavRawY = r.I(); u.NavGX = r.I(); u.NavGY = r.I();
        u.NextLosTick = r.L(); u.NextPersonalTick = r.L(); u.LosOk = r.Bool(); u.LosGoal = r.Vec();
        u.NavKind = r.Byte();
        u.ProgPos = r.Vec(); u.ProgTick = r.L(); u.ProgLast = r.L(); u.NudgeUntil = r.L(); u.StuckLevel = r.I(); u.NudgeDir = r.Vec();
        a.Flag = u;
        return a;
    }

    void HashOrganisation(ref Hasher h)
    {
        h.Add(nextCompanyId); h.Add(nextBattalionId); h.Add(nextSquadId);
        foreach (var r in Battalions) { h.Add(r.Id); h.Add(r.Owner); h.Add(r.Number); h.Add((int)r.Form); h.Add(r.Companies.Count); }
        foreach (var c in Companies)
        {
            h.Add(c.Id); h.Add(c.Owner); h.Add(c.Number); h.Add((int)c.Form); h.Add(c.InitialStrength);
            h.Add(c.Facing.X); h.Add(c.Facing.Y); h.Add(c.Battalion?.Id ?? 0);
            foreach (var u in c.Members) h.Add(u.Id);
            h.Add(c.Holes); h.Add(c.MarchUntil); h.Add(c.MarchFireOn ? 1 : 0); h.Add(c.MarchAdvUntil);
            foreach (var q in c.Squads) { h.Add(q.Id); h.Add(q.Number); h.Add(q.Members.Count); foreach (var u in q.Members) h.Add(u.Id); h.Add(q.MarchUntil); }
            foreach (var u in c.Scouts) h.Add(u.Id);
            if (c.Anchor is { } a)
            {
                h.Add(a.Pos.X); h.Add(a.Pos.Y); h.Add(a.Heading.X); h.Add(a.Heading.Y); h.Add(a.Goal.X); h.Add(a.Goal.Y);
                h.Add(a.Arrived ? 1 : 0); h.Add(a.Column ? 1 : 0); h.Add(a.WaitTicks); h.Add(a.Trail.Count); h.Add(a.SpeedMul);
            }
        }
        HashScouts(ref h);   // 정찰병 자율 상태 (Scouts.cs)
        HashFormationState(ref h);   // 전술 틀 · 역할 · 분대 대형 (쓰는 중대만)
        HashCommandState(ref h);     // 지휘관 · 인계 (CommandRanks.cs)
        HashCloseRanks(ref h);       // 대열 정비 (CloseRanks.cs)
        HashPostures(ref h);         // 중대 자세 (CoverSeek.cs, tweak1)
        HashContact(ref h);          // F1 접적 (Contact.cs)
        HashFireControl(ref h);      // N73 사격 통제 (FireControl.cs)
        HashProps(ref h);            // F6 소품 (Props.cs)
        HashTrenchAssault(ref h);    // N32 참호 돌격 (TrenchAssault.cs)
        HashRally(ref h);            // N79a 집결 지점 · 포격 종류 (Rally.cs)
        HashChoke(ref h);            // 길목 통과 큐 (Choke.cs)
        HashComms(ref h);            // 통신 · 난이도 (Comms.cs, 기본 상태면 생략)
    }
}
