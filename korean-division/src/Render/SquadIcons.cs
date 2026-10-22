using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;

namespace KoreanDivision.Render;

/// <summary>
/// 분대 상태 아이콘 (표시 전용 — 시뮬 상태를 바꾸지 않음, 저장 · 체크섬 무관): 분대마다 아이콘 하나를 분대 위에 그린다.
/// 여러 상태가 겹치면 <see cref="Pick"/> 우선순위로 하나만. 아이콘은 코드로 만든 7x7 도트 (검은 윤곽 자동) 를 아틀라스 한 장에 올려
/// 분대당 1 draw. 상태는 6틱마다만 다시 계산해 분대 id 로 캐시.
/// 보이는 범위: 중간 · 근접 줌에서 우리 편(동맹 포함) 분대, 선택한 분대는 항상 (축소 점 화면에서도). 축소 점 화면의 나머지는 중대 휘장이 대신한다.
/// </summary>
public static class SquadIcons
{
    public enum Status : byte { None = 0, Pinned, Suppressed, Retreating, OutOfAmmo, Charging, Leaderless, Holding, LowAmmo, HoldFire, ReturnFire }

    /// <summary>켜기 · 끄기 (I 키, 상단 단추).</summary>
    public static bool Enabled = true;
    static readonly bool Demo = Environment.GetEnvironmentVariable("KD_ICONDEMO") == "1";   // 확인용: 분대 번호로 상태를 돌려 가며 보여 줌 (접촉표 촬영, 표시만)

    /// <summary>분대에서 읽은 사실 (우선순위 판정 입력). Pinned = 살아 있는 분대원 절반 이상이 고정 (엎드림, Unit.Pinned — 제압 재설계 N25).</summary>
    public struct Facts { public bool Pinned, Suppressed, Retreating, OutOfAmmo, Charging, Leaderless, Holding, LowAmmo, HoldFire, ReturnFire; }

    public const float SuppressedThreshold = 0.35f;   // 분대 평균 제압 비율 (SuppressionRatio) 이 이 이상이면 '제압됨'

    /// <summary>'탄약 부족' 단계: 탄약 보유 병사 절반 이상이 한도의 이 비율 미만 (렌더 전용).</summary>
    public const float LowAmmoThreshold = 0.30f;

    /// <summary>우선순위: 고정 > 제압 > 후퇴 > 탄약 없음 > 돌격 > 지휘관 없음 > 참호 점거.</summary>
    public static Status Pick(in Facts f) =>
        f.Pinned ? Status.Pinned : f.Suppressed ? Status.Suppressed : f.Retreating ? Status.Retreating : f.OutOfAmmo ? Status.OutOfAmmo
        : f.LowAmmo ? Status.LowAmmo
        : f.Charging ? Status.Charging : f.HoldFire ? Status.HoldFire : f.ReturnFire ? Status.ReturnFire : f.Leaderless ? Status.Leaderless : f.Holding ? Status.Holding : Status.None;

    /// <summary>분대 → 사실. retreating = 소속 중대의 전술 AI 상태가 후퇴 (플레이어 중대는 전술 AI 가 없어 항상 false — 훅 자리).</summary>
    public static Facts Read(World w, Squad q, bool retreating)
    {
        var f = new Facts { Retreating = retreating };
        int alive = 0, charging = 0, trench = 0, ammoUsers = 0, ammoEmpty = 0, ammoLow = 0, pinned = 0, armed = 0, fhold = 0, fret = 0; float supp = 0;
        foreach (var u in q.Members)
        {
            if (!u.Alive) continue;
            alive++; supp += u.SuppressionRatio;
            if (u.Charging) charging++;
            if (u.Pinned) pinned++;
            if (u.Weapon != null) { armed++; if (u.FireMode == FireMode.Hold) fhold++; else if (u.FireMode == FireMode.ReturnFire) fret++; }   // N73 사격 통제
            if (u.InTrench) trench++;
            if (u.UsesAmmo && w.AmmoEnabled) { ammoUsers++; if (u.Ammo <= 0) ammoEmpty++; else if (u.AmmoRatio < LowAmmoThreshold) ammoLow++; }
        }
        if (alive == 0) return default;
        f.Suppressed = supp / alive >= SuppressedThreshold;
        f.Pinned = pinned * 2 >= alive;
        f.Charging = charging * 2 >= alive;
        f.Holding = trench * 2 >= alive;
        f.HoldFire = armed > 0 && fhold * 2 >= armed; f.ReturnFire = armed > 0 && !f.HoldFire && (fhold + fret) * 2 >= armed;
        f.OutOfAmmo = ammoUsers > 0 && ammoEmpty * 2 >= ammoUsers;
        f.LowAmmo = ammoUsers > 0 && (ammoEmpty + ammoLow) * 2 >= ammoUsers;
        bool officer = false;
        foreach (var m in q.Company.Members) if (m.Alive && m.IsOfficer) { officer = true; break; }
        f.Leaderless = !officer && !q.NcoLed;
        return f;
    }

    // ---------------------------------------------------------------- 도트 아이콘 (7x7, '#' = 채움; 윤곽은 자동)
    static readonly string[][] Art =
    {
        Array.Empty<string>(),
        new[] { "..###..", "..###..", "#######", ".#####.", "..###..", "...#...", "#######" },   // Pinned: 아래 화살표 + 바닥
        new[] { "...#...", "...#...", "...#...", "...#...", "...#...", ".......", "...#..." },   // Suppressed: 느낌표
        new[] { "...#...", "..##...", ".######", "#######", ".######", "..##...", "...#..." },   // Retreating: 뒤(왼쪽) 화살표
        new[] { "...#...", "..###..", "..###..", "..###..", "..###..", ".#####.", ".#####." },   // OutOfAmmo: 탄환
        new[] { "...#...", "..###..", ".#####.", "#..#..#", "...#...", "...#...", "...#..." },   // Charging: 앞으로 창끝
        new[] { ".#####.", "##...##", "....##.", "...##..", "...#...", ".......", "...#..." },   // Leaderless: 물음표
        new[] { "#######", "#######", "#######", "#######", ".#####.", "..###..", "...#..." },   // Holding: 방패
        new[] { "...#...", "..#.#..", "..#.#..", "..###..", "..#.#..", ".#####.", ".#####." },   // LowAmmo: 속 빈 탄환
        new[] { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#" },   // HoldFire: X (사격 금지)
        new[] { "...#...", "..###..", ".#####.", "...#...", "...#...", "..###..", ".#####." },   // ReturnFire: 되받는 화살 (응사만)
    };
    static readonly Color[] Tint =
    {
        default, new(235, 80, 70, 255), new(255, 205, 60, 255), new(120, 175, 240, 255), new(205, 150, 90, 255),
        new(245, 125, 50, 255), new(205, 205, 205, 255), new(115, 205, 105, 255), new(250, 190, 70, 255),
        new(240, 60, 60, 255), new(255, 170, 40, 255),
    };
    const int Cell = 9;   // 7 + 윤곽 1 + 여유 1
    static Texture2D atlas; static bool atlasTried, atlasOk;

    /// <summary>테스트용: 상태의 7x7 모양.</summary>
    public static string[] Shape(Status s) => Art[(int)s];

    static void EnsureAtlas()
    {
        if (atlasTried) return;
        atlasTried = true;
        if (!Raylib.IsWindowReady()) return;
        var img = Raylib.GenImageColor(Cell * Art.Length, Cell, new Color(0, 0, 0, 0));
        var edge = new Color(12, 12, 12, 255);
        for (int s = 1; s < Art.Length; s++)
        {
            var a = Art[s];
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                {
                    if (a[y][x] != '#') continue;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        Raylib.ImageDrawPixel(ref img, s * Cell + 1 + x + dx, 1 + y + dy, edge);
                }
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    if (a[y][x] == '#') Raylib.ImageDrawPixel(ref img, s * Cell + 1 + x, 1 + y, Tint[s]);
        }
        atlas = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(atlas, TextureFilter.Point);
        atlasOk = true;
    }

    // ---------------------------------------------------------------- 상태 캐시 (분대 id → 상태, 6틱마다)
    static readonly Dictionary<int, (long Tick, Status St)> cache = new();
    static readonly HashSet<int> retreatCos = new();
    static long retreatTick = -100;
    static readonly HashSet<int> seen = new();
    static readonly List<Squad> draw = new();

    static void RefreshRetreat(World w)
    {
        if (w.Tick >= retreatTick && w.Tick - retreatTick < 6) return;
        retreatTick = w.Tick; retreatCos.Clear();
        foreach (var (f, ai) in w.Controllers)
            if (ai is TacticalAi ta && ta.StateOf(f) is { } s)
                foreach (var g in s.Groups) if (g.CompanyId != 0 && g.State == TacState.Fallback) retreatCos.Add(g.CompanyId);
    }

    /// <summary>지휘관 없음 표시가 의미 있는 때: 분대가 선택됐거나, 최근 3초 안에 사격했거나 교전 중 (표시 전용).</summary>
    public static bool LeaderlessMatters(World w, Squad q)
    {
        foreach (var m in q.Members)
        {
            if (!m.Alive) continue;
            if (m.Selected || m.Engaging || w.Tick - m.LastShotTick < 90 || m.SuppressionRatio > 0.05f) return true;
        }
        return false;
    }

    static Status StatusOf(World w, Squad q)
    {
        if (cache.TryGetValue(q.Id, out var c) && w.Tick >= c.Tick && w.Tick - c.Tick < 6) return c.St;
        Status st;
        if (Demo) st = (Status)(q.Id % 9);
        else
        {
            var f = Read(w, q, retreatCos.Contains(q.Company.Id));
            if (f.Leaderless && !LeaderlessMatters(w, q)) f.Leaderless = false;   // 지휘관 없음은 전투 중이거나 선택했을 때만 의미가 있다
            st = Pick(f);
        }
        if (cache.Count > 4096) cache.Clear();
        cache[q.Id] = (w.Tick, st);
        return st;
    }

    /// <summary>BeginMode2D 안에서 호출. units = 이번 프레임에 화면 안 · 보이는 유닛 (UnitRenderer 컬링 결과). selectedOnly = 축소 점 화면 (선택 분대만).</summary>
    internal static void Draw(World w, RtsCamera cam, int me, ReadOnlySpan<UnitRenderer.Item> units, bool selectedOnly)
    {
        if (!Enabled) return;
        EnsureAtlas();
        if (!atlasOk) return;
        RefreshRetreat(w);
        seen.Clear(); draw.Clear();
        bool all = w.Fog.SeesAll(me);
        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i].U;
            if (u.Squad is not { } q || !seen.Add(q.Id)) continue;
            if (!all && u.Owner != me && !w.Allied(me, u.Owner)) continue;
            if (selectedOnly && !u.Selected) continue;
            draw.Add(q);
        }
        float zoom = cam.Zoom, px = 1.5f / zoom, size = Cell * px;   // 도트 1칸 = 화면 1.5px (작게)
        // 중간 줌에서 흐리게 (줌 0.5 → 45%, 1.0 이상 → 100%); 선택한 분대는 항상 진하게
        byte fade = (byte)(255 * (0.45f + 0.55f * Math.Clamp((zoom - 0.5f) / 0.5f, 0f, 1f)));
        for (int i = 0; i < draw.Count; i++)
        {
            var q = draw[i];
            var st = StatusOf(w, q);
            if (st == Status.None) continue;
            bool sel = false; foreach (var m in q.Members) if (m.Alive && m.Selected) { sel = true; break; }
            if (!OverlayRules.ShowSquadIcon(KoreanDivision.UI.UserSettings.Overlay, st, sel)) continue;   // T38m: 간결 = 선택 · 위험 상태만
            float minY = float.MaxValue, sx = 0; int n = 0;
            foreach (var m in q.Members)
            {
                if (!m.Alive) continue;
                n++; sx += m.Pos.X; if (m.Pos.Y < minY) minY = m.Pos.Y;
            }
            if (n == 0) continue;
            float cx = sx / n * Config.TileSize, top = minY * Config.TileSize - 22f / zoom;   // 분대 맨 앞 병사 머리 위
            Raylib.DrawTexturePro(atlas, new Rectangle((int)st * Cell, 0, Cell, Cell), new Rectangle(cx - size / 2, top - size, size, size), Vector2.Zero, 0, sel || st == Status.Retreating || st == Status.Pinned ? Color.White : new Color((byte)255, (byte)255, (byte)255, fade));
        }
    }

    public static void Dispose() { if (atlasOk) Raylib.UnloadTexture(atlas); atlasOk = false; atlasTried = false; }
}
