using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.UI;

namespace KoreanDivision;

/// <summary>
/// Game 의 미션 부분: 배치 단계 (부대 끌기 · 회전 · 작전 개시), 미션 HUD (목표 · 시간 · 사건 메시지), 결과 화면 · 결과 json.
/// 배치는 World.ApplyDeployment (첫 틱 전 시작 상태 변경 — 리플레이 머리말에 최종 목록이 남는다), 나머지는 표시 전용.
/// </summary>
public sealed partial class Game
{
    bool deployDone;
    string? dragForce, deploySel;
    Vector2 dragOffset;
    MissionResults? missionResults;
    string? missionResultsPath;
    bool missionResultsDone, noResultsFile, deployCamDone, campaignSaved;
    bool discardArmed;   // 저장 안 된 결과를 버리겠다는 첫 번째 확인
    CampaignState? campaignUnsaved;   // 정산했지만 저장 실패 — 결과 창에서 다시 시도 (T19a)

    /// <summary>배치 단계인가: 미션 · 첫 틱 전 · 사람이 조작 · 아직 '작전 개시' 전.</summary>
    public bool DeployPhase => World.Mission is { Started: false } && World.Tick == 0 && !Observer && !deployDone;
    /// <summary>[퍼즈 · 테스트] 배치를 끝내고 시작한다.</summary>
    public void StartMission() { if (DeployPhase) { deployDone = true; Paused = false; ShowNotice(S.DpStart); } }
    public MissionResults? MissionResult => missionResults;

    /// <summary>미션 월드를 만든다 (생성자에서). 사람 세력 = 미션의 player.</summary>
    static World CreateMissionGameWorld(GameOptions opt, MissionDef def, int human, bool spectate)
    {
        var w = World.CreateMissionWorld(def, Defs.Current, opt.MissionSeed);
        w.Fog.Enabled = !opt.NoFog;
        ApplyDifficultyOption(w, opt, CampaignState.IsCampaignMission(def.Id));   // 난이도 (GameComms.cs)
        if (opt.Ai != "none" || spectate) World.AttachMissionAi(w, human, spectate);
        return w;
    }

    // ---------------------------------------------------------------- 배치 단계 입력

    IEnumerable<MissionForce> DeployableForces() =>
        World.Mission is { } m ? m.Def.Forces.Where(f => World.IsDeployable(f)) : Enumerable.Empty<MissionForce>();

    /// <summary>마우스 아래 병력 (중심에서 가장 먼 유닛 + 1칸 안, 가장 가까운 중심).</summary>
    MissionForce? ForceAt(Vector2 world, bool deployableOnly)
    {
        if (World.Mission is not { } m) return null;
        MissionForce? best = null;
        float bd = float.MaxValue;
        foreach (var f in m.Def.Forces)
        {
            if (f.Faction != Me || (deployableOnly && !World.IsDeployable(f))) continue;
            if (World.ForceCentroid(f.Id) is not Vector2 c || !m.ForceUnits.TryGetValue(f.Id, out var ids)) continue;
            float rad = 0;
            foreach (int id in ids) if (World.Find(id) is Unit u) rad = MathF.Max(rad, Vector2.Distance(u.Pos, c));
            float d = Vector2.Distance(world, c);
            if (d <= rad + 1.5f && d < bd) { bd = d; best = f; }
        }
        return best;
    }

    Rectangle DeployBar => new(Hud.Viewport.X + Hud.Viewport.Width / 2 - MathF.Min(470, Hud.Viewport.Width / 2 - 100), Hud.Viewport.Y + Hud.Viewport.Height - 74, MathF.Min(940, Hud.Viewport.Width - 200), 64);
    Rectangle DeployStartRect { get { var b = DeployBar; return new(b.X + b.Width - 200, b.Y + 12, 188, 40); } }

    void UpdateDeploy()
    {
        var m = World.Mission!;
        Paused = true;
        if (!deployCamDone && m.Def.DeployZones.FirstOrDefault(z => z.Faction == Me) is { } zone)
        {
            // 처음 한 번: 배치 구역 전체가 보이게 (구역 + 여유 6칸)
            deployCamDone = true;
            float zw = zone.Rect is { Length: 4 } zr ? zr[2] : zone.Radius * 2, zh = zone.Rect is { Length: 4 } zr2 ? zr2[3] : zone.Radius * 2;
            var vp = Hud.Viewport;
            Camera.SetZoom(MathF.Min(1.5f, MathF.Min(vp.Width / ((zw + 12) * Config.TileSize), vp.Height / ((zh + 12) * Config.TileSize))));
            Camera.CenterOn(zone.Center);
        }
        if (Input.IsKeyPressed(KeyboardKey.Enter) || Input.IsKeyPressed(KeyboardKey.KpEnter) || (Input.IsMouseButtonPressed(MouseButton.Left) && Ui.Contains(DeployStartRect, mouse)))
        {
            StartMission();
            return;
        }
        if (Input.IsMouseButtonPressed(MouseButton.Left) && mouseInViewport)
        {
            var f = ForceAt(mouseWorld, false);
            if (f != null && !World.IsDeployable(f)) ShowNotice(S.DpLocked);
            else if (f != null && World.ForceCentroid(f.Id) is Vector2 c)
            {
                dragForce = deploySel = f.Id;
                dragOffset = World.ForceAnchor(f.Id) - mouseWorld;
            }
        }
        if (dragForce != null && Input.IsMouseButtonReleased(MouseButton.Left))
        {
            var target = mouseWorld + dragOffset;
            if (!World.InDeployZone(Me, target)) ShowNotice(S.DpOutside);
            else if (World.ApplyDeployment(dragForce, target, World.ForceFacing(dragForce)))
                ShowNotice(string.Format(S.DpMoved, m.Def.Forces.First(x => x.Id == dragForce).Name ?? dragForce));
            dragForce = null;
        }
        if (dragForce == null && (Input.IsKeyPressed(KeyboardKey.R) || (Input.IsMouseButtonPressed(MouseButton.Right) && mouseInViewport)))
        {
            var f = (Input.IsMouseButtonPressed(MouseButton.Right) ? ForceAt(mouseWorld, true) : null) ?? (deploySel != null ? m.Def.Forces.FirstOrDefault(x => x.Id == deploySel) : null);
            if (f != null && World.IsDeployable(f))
            {
                deploySel = f.Id;
                World.ApplyDeployment(f.Id, World.ForceAnchor(f.Id), World.ForceFacing(f.Id) + 45);
            }
        }
    }

    // ---------------------------------------------------------------- 결과

    /// <summary>결과 확정 한 곳. 리플레이 · 결과 파일 없음(연습 출격 · 퍼즈) 이면 아무것도 쓰지 않는다 = 캠페인 상태 · 경제 · 기록 불변 (T38i).</summary>
    internal static string? SettleMission(MissionResults r, bool isReplay, bool noResultsFile, bool cheats, out bool saved, out CampaignState? unsaved)
    {
        saved = false; unsaved = null;
        if (isReplay || noResultsFile) return null;
        string path = r.Save();
        // 저장 트랜잭션: 읽기 실패 (깨진 · 더 새 버전) 파일은 새 상태로 덮어쓰지 않는다. 확정 (Apply → Save) 은 결과가 정해진 이 한 곳에서만.
        if (!cheats) saved = CampaignState.CommitResult(r, out unsaved);   // 정산 직후 자동 저장 (T19a) · 실패하면 [다시 시도] 용으로 보관
        return path;
    }

    void HandleMissionOver()
    {
        if (!missionResultsDone)
        {
            missionResultsDone = true;
            missionResults = MissionResults.From(World);
            missionResultsPath = SettleMission(missionResults, IsReplay, noResultsFile, World.CheatsUsed, out campaignSaved, out campaignUnsaved);
        }
        if (campaignUnsaved != null && Input.IsMouseButtonPressed(MouseButton.Left) && Ui.Contains(CampaignRetryRect, mouse))
        {   // 저장 실패 → 다시 시도 (같은 정산 결과를 다시 쓴다; 정산을 두 번 하지 않음)
            campaignSaved = CampaignSaves.SaveWithAuto(campaignUnsaved, CampaignSaves.Auto.Settle);
            if (campaignSaved) campaignUnsaved = null;
            return;
        }
        if (campaignUnsaved == null) discardArmed = false;
        bool wantRestart = Input.IsKeyPressed(KeyboardKey.Enter) && !IsReplay, wantQuit = false;
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (!IsReplay && Ui.Contains(ResultButton(0), mouse)) wantRestart = true;
            else if (Ui.Contains(ResultButton(1), mouse)) wantQuit = true;
        }
        if ((wantRestart || wantQuit) && campaignUnsaved != null && !discardArmed) { discardArmed = true; return; }   // 저장 전에는 두 번 눌러야 떠난다
        if (wantRestart) RestartRequested = true;
        else if (wantQuit) QuitRequested = true;
    }

    Rectangle ResultPanel
    {
        get
        {
            var vp = Hud.Viewport;
            float w = MathF.Min(760, vp.Width - 40), h = MathF.Min(660, vp.Height - 30);
            return new Rectangle(vp.X + (vp.Width - w) / 2, vp.Y + (vp.Height - h) / 2, w, h);
        }
    }

    /// <summary>캠페인 저장 실패 시 [다시 시도] 버튼 (결과 창 아래 두 번째 줄, T19a).</summary>
    Rectangle CampaignRetryRect { get { var p = ResultPanel; return new Rectangle(p.X + p.Width - 24 - 260, p.Y + p.Height - 100, 260, 36); } }

    Rectangle ResultButton(int i)
    {
        var p = ResultPanel;
        return new Rectangle(p.X + p.Width - 2 * 190 - 24 + i * 190, p.Y + p.Height - 54, 180, 38);
    }

    /// <summary>콘솔: mission win | lose (치트 — 명령으로 기록).</summary>
    bool TryMissionConsole(string[] words)
    {
        if (words.Length != 2 || words[0] != "mission" || World.Mission == null) return false;
        if (words[1] is "win" or "lose") { World.Issue(new MissionEndCommand(Me, words[1] == "win")); if (DeployPhase) StartMission(); }
        return true;
    }

    // ---------------------------------------------------------------- 그리기 (월드 좌표)

    static void DrawArea(MissionArea a, Color fill, Color line, float th)
    {
        float ts = Config.TileSize;
        if (a.Rect is { Length: 4 } r)
        {
            var rr = new Rectangle(r[0] * ts, r[1] * ts, r[2] * ts, r[3] * ts);
            Raylib.DrawRectangleRec(rr, fill);
            Raylib.DrawRectangleLinesEx(rr, th, line);
        }
        else if (a.Circle is { Length: 3 } c)
        {
            Raylib.DrawCircleV(new Vector2(c[0], c[1]) * ts, c[2] * ts, fill);
            Raylib.DrawRing(new Vector2(c[0], c[1]) * ts, c[2] * ts - th, c[2] * ts, 0, 360, 48, line);
        }
    }

    void DrawMissionWorld()
    {
        if (World.Mission is not { } m) return;
        float th = MathF.Max(1.5f, 2f / Camera.Zoom);
        var objAreas = new HashSet<string>();
        for (int i = 0; i < m.Def.Objectives.Count; i++)
            if (m.ObjRevealed[i] && m.ObjState[i] == MissionState.Active && m.Def.Objectives[i].Area is { } an) objAreas.Add(an);
        foreach (var an in objAreas.OrderBy(x => x, StringComparer.Ordinal))
            if (m.Def.Area(an) is { } a) DrawArea(a, Palette.Alpha(Palette.Accent, 26), Palette.Alpha(Palette.Accent, 170), th);
        if (!DeployPhase) return;
        foreach (var z in m.Def.DeployZones)
            if (z.Faction == Me) DrawArea(z, new Color(70, 140, 255, 30), new Color(90, 160, 255, 210), th);
        // 배치 가능한 부대 표시 (선택 = 밝게) · 끌고 있는 부대의 도착 미리보기
        foreach (var f in DeployableForces())
            if (World.ForceCentroid(f.Id) is Vector2 c)
            {
                Color col = f.Id == deploySel ? Palette.Select : Palette.Alpha(Palette.Select, 120);
                Raylib.DrawRing(c * Config.TileSize, 10 / Camera.Zoom, 12 / Camera.Zoom + 1, 0, 360, 24, col);
                var dir = MissionDef.DirOf(World.ForceFacing(f.Id));
                Raylib.DrawLineEx(c * Config.TileSize, (c + dir * 4) * Config.TileSize, th, col);
            }
        if (dragForce != null && m.Def.Forces.FirstOrDefault(x => x.Id == dragForce) is { } df)
        {
            var target = mouseWorld + dragOffset;
            bool ok = World.InDeployZone(Me, target);
            Color gc = ok ? Palette.Alpha(Palette.Select, 200) : Palette.Alpha(Palette.HpLow, 200);
            float r = MathF.Max(2f, 3f / Camera.Zoom);
            foreach (var (_, pos, _) in World.LayoutForce(df, target, MissionDef.DirOf(World.ForceFacing(df.Id))))
                Raylib.DrawCircleV(pos * Config.TileSize, r, gc);
        }
    }

    // ---------------------------------------------------------------- 그리기 (화면)

    void DrawMissionOverlay()
    {
        if (World.Mission is not { } m) return;
        var vp = Hud.Viewport;
        // 목표 패널 (왼쪽 위)
        float pw = 340, x = vp.X + 8, y = vp.Y + 8;
        int shown = 0;
        for (int i = 0; i < m.Def.Objectives.Count; i++) if (m.ObjRevealed[i]) shown++;
        float limit = MissionText.TimeLimit(m.Def);
        float ph = 34 + shown * 20 + (limit > 0 ? 0 : 0);
        Ui.Panel(new Rectangle(x, y, pw, ph), Palette.Alpha(Palette.PanelDark, 210), Palette.Alpha(Palette.HudTrim, 200));
        Ui.Text(m.Def.Title, x + 8, y + 6, 16, Palette.Accent);
        string clock = limit > 0 ? string.Format(S.MsHudLeft, MissionText.Clock(limit - World.Time)) : string.Format(S.MsHudTime, MissionText.Clock(World.Time));
        Ui.Text(clock, x + pw - 8 - Ui.Measure(clock, 16).X, y + 6, 16, Palette.TextDim);
        float ly = y + 28;
        for (int i = 0; i < m.Def.Objectives.Count; i++)
        {
            if (!m.ObjRevealed[i]) continue;
            var o = m.Def.Objectives[i];
            Color c = m.ObjState[i] == MissionState.Done ? Palette.HpGood : m.ObjState[i] == MissionState.Failed ? Palette.HpLow : o.Primary ? Palette.Text : Palette.TextDim;
            string mark = m.ObjState[i] == MissionState.Done ? "[v] " : m.ObjState[i] == MissionState.Failed ? "[x] " : o.Primary ? "[ ] " : "( ) ";
            Ui.Text(mark + MissionText.Objective(o), x + 8, ly, 16, c);
            string pr = MissionText.Progress(World, m, i);
            Ui.Text(pr, x + pw - 8 - Ui.Measure(pr, 16).X, ly, 16, c);
            ly += 20;
        }
        // 사건 메시지 (최근 3개, 게임 시간 12초)
        float my = y + ph + 8;
        for (int k = Math.Max(0, m.Messages.Count - 3); k < m.Messages.Count; k++)
        {
            var (tick, text) = m.Messages[k];
            if (World.Tick - tick > 12 * 60) continue;
            Vector2 sz = Ui.Measure(text, 16);
            Ui.Panel(new Rectangle(x, my, sz.X + 16, 24), Palette.Alpha(Palette.PanelDark, 220), Palette.Alpha(Palette.Accent, 200));
            Ui.Text(text, x + 8, my + 4, 16, Palette.Accent);
            my += 28;
        }

        if (DeployPhase)
        {
            var bar = DeployBar;
            Ui.Panel(bar, Palette.Alpha(Palette.PanelDark, 225), Palette.HudTrim);
            Ui.Text(S.DpTitle, bar.X + 12, bar.Y + 8, 16, Palette.Accent);
            Ui.Text(S.DpHint, bar.X + 12, bar.Y + 34, 16, Palette.TextDim);
            var sr = DeployStartRect;
            bool hov = Ui.Contains(sr, mouse);
            Ui.Button(sr, S.DpStart, hov, false);
        }

        if (World.Winner != 0) DrawMissionResults(m);
    }

    /// <summary>전과 보고 (읽기 전용 집계): 사상 원인 비율 · 중대별 요약. 공간이 모자라면 아래 줄을 자른다.</summary>
    void DrawBattleReport(MissionResults r, Rectangle p, float x1, float x2, float y0)
    {
        float limit = p.Y + p.Height - 62;
        float yl = y0;
        if (yl + 22 <= limit) { Ui.Text(S.RsCauses, x1, yl, 16, Palette.Accent); yl += 22; }
        foreach (var f in r.Factions)
        {
            if (yl + 20 > limit) break;
            if (!r.LossCause.TryGetValue(f.Id, out var c)) continue;
            long tot = c[0] + c[1] + c[2];
            if (tot <= 0) continue;
            Ui.Text(string.Format(S.RsCauseFmt, f.Name, c[0] * 100 / tot, c[1] * 100 / tot, c[2] * 100 / tot), x1, yl, 16, Palette.Player(f.Id));
            yl += 20;
        }
        float yr = y0;
        if (r.Companies.Count == 0 || yr + 22 > limit) return;
        Ui.Text(S.RsCompanies, x2, yr, 16, Palette.Accent); yr += 22;
        foreach (var c in r.Companies)
        {
            if (yr + 20 > limit) break;
            Ui.Text(string.Format(S.RsCompanyFmt, c.Number, c.Alive, c.Initial, c.Kills), x2, yr, 16, Palette.Text);
            yr += 20;
        }
    }

    void DrawMissionResults(MissionState m)
    {
        var r = missionResults ??= MissionResults.From(World);
        var p = ResultPanel;
        Raylib.DrawRectangleRec(Hud.Viewport, Palette.Alpha(Color.Black, 120));
        Ui.Panel(p, Palette.Alpha(Palette.PanelDark, 245), Palette.HudTrim);
        bool won = m.Result == MissionState.Won;
        Ui.Text(won ? S.RsVictory : S.RsDefeat, p.X + 24, p.Y + 16, 26, won ? Palette.HpGood : Palette.HpLow);
        Ui.Text(m.Def.Title + (World.CheatsUsed ? "  " + S.RsCheat : ""), p.X + 24, p.Y + 50, 16, Palette.TextDim);
        float y = p.Y + 80, x1 = p.X + 24, x2 = p.X + p.Width / 2 + 8;
        Ui.Text($"{S.RsDuration}  {MissionText.Clock(r.DurationSeconds)}", x1, y, 16, Palette.Text);
        if (!won && m.FailReason >= 0) Ui.Text(string.Format(S.RsFailReason, MissionText.Failure(m.Def.Failures[m.FailReason])), x2, y, 16, Palette.Warning);
        y += 28;
        Ui.Text(S.RsLosses, x1, y, 16, Palette.Accent);
        Ui.Text(S.RsObjectives, x2, y, 16, Palette.Accent);
        float yl = y + 22, yr = y + 22;
        foreach (var f in r.Factions)
        {
            Ui.Text($"{f.Name}{(f.Player ? " " + S.PlayerTag : "")}  {f.Spawned} / {f.Lost}", x1, yl, 16, Palette.Player(f.Id));
            yl += 20;
        }
        Ui.Text($"{S.RsKills}  {r.EnemyKills}", x1, yl, 16, Palette.Text); yl += 28;
        Ui.Text(S.RsTopUnits, x1, yl, 16, Palette.Accent); yl += 22;
        if (r.TopUnits.Count == 0) { Ui.Text(S.RsNone, x1, yl, 16, Palette.TextDim); yl += 20; }
        foreach (var u in r.TopUnits) { Ui.Text($"#{u.Id} {u.Name}  {u.Kills}", x1, yl, 16, Palette.Text); yl += 20; }
        yl += 8;
        Ui.Text($"{S.RsPromotions}  {string.Format(S.RsPromotionsN, r.Promotions.Count)}", x1, yl, 16, Palette.Text);
        DrawBattleReport(r, p, x1, x2, yl + 28);
        for (int i = 0; i < m.Def.Objectives.Count; i++)
        {
            var o = m.Def.Objectives[i];
            if (!m.ObjRevealed[i] && m.ObjState[i] != MissionState.Done) continue;
            string st = m.ObjState[i] == MissionState.Done ? S.ObjStateDone : m.ObjState[i] == MissionState.Failed ? S.ObjStateFailed : S.ObjStateActive;
            Color c = m.ObjState[i] == MissionState.Done ? Palette.HpGood : m.ObjState[i] == MissionState.Failed ? Palette.HpLow : Palette.TextDim;
            Ui.Text($"{(o.Primary ? S.MsPrimary : S.MsSecondary)}: {MissionText.Objective(o)} — {st}", x2, yr, 16, c);
            yr += 20;
        }
        if (missionResultsPath != null) Ui.Text(string.Format(S.RsSaved, Path.GetFileName(missionResultsPath)), x1, p.Y + p.Height - 44, 16, Palette.TextDim);
        if (campaignSaved) Ui.Text(S.CmSaved, x2, p.Y + p.Height - 44, 16, Palette.HpGood);
        if (campaignUnsaved != null) Ui.Button(CampaignRetryRect, S.CmSaveFailedResult, Ui.Contains(CampaignRetryRect, mouse), false);
        if (campaignUnsaved != null && discardArmed) Ui.Text(S.CmDiscardUnsaved, x1, p.Y + p.Height - 92, 15, Palette.Warning);
        if (!IsReplay) Ui.Button(ResultButton(0), S.RsRetry, Ui.Contains(ResultButton(0), mouse), false);
        Ui.Button(ResultButton(1), S.RsMenu, Ui.Contains(ResultButton(1), mouse), false);
    }
}
