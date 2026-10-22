using System.Numerics;
using KoreanDivision.Core;
using KoreanDivision.UI;
using Raylib_cs;

namespace KoreanDivision.Render;

/// <summary>
/// 축소 표시용 부대 기호 (ROADMAP: "크게 축소하면 부대 기호 + 병종 아이콘"): 중대마다 NATO 식 틀 기호 하나를 중대 중심에 그린다.
/// 틀 = 세력 색, 안의 그림 = 병종 (보병 X · 기관총 · 대전차 · 포병 점 · 기병 사선 · 기갑 타원 · 보급 마차 · 장교 깃발), 아래 병력 막대 (현재/편성 당시), 번호.
/// 중대 단위로 집계 (6틱마다 캐시, 유닛당 글자 없음) 하므로 1만 기도 가볍다. 화면 좌표로 그린다 (BeginMode2D 밖).
/// 병종은 UnitDef 의 category · 무기 속성에서 데이터대로 판정한다 (Of).
/// </summary>
public static class UnitSymbols
{
    public enum Branch { Infantry, MachineGun, AntiTank, Artillery, Cavalry, Armor, Supply, Officer, Scout, Assault, Howitzer, Sniper }

    static readonly string[] BranchName = S.SymBranches.Split('|');

    /// <summary>유닛 정의 → 병종 (정의별 캐시).</summary>
    public static Branch Of(UnitDef d)
    {
        if (cache.TryGetValue(d, out var b)) return b;
        var w = d.Weapon != null && Defs.Current.Weapons.TryGetValue(d.Weapon, out var wd) ? wd : null;
        b = d.IsOfficer ? Branch.Officer
          : d.Category == "wagon" || d.IsSupplyWagon ? Branch.Supply
          : d.Category == "armored" ? Branch.Armor
          : d.Category == "cavalry" ? Branch.Cavalry
          : d.Category == "gun" && w != null && w.Indirect ? Branch.Howitzer   // 곡사포 = 곡사 무기 포 (자료 판정, 이름 아님)
          : d.Category == "gun" || (w != null && w.Indirect) ? Branch.Artillery
          : w != null && w.PreferOfficer && !w.Indirect ? Branch.Sniper   // 저격 = 장교 우선 표적 무기 (무기 속성)
          : w != null && w.IsAntiArmor ? Branch.AntiTank
          : w != null && w.Spray > 0 ? Branch.MachineGun
          : d.Scout ? Branch.Scout
          : d.Grenades > 0 && d.Category == "infantry" ? Branch.Assault
          : Branch.Infantry;
        cache[d] = b;
        return b;
    }
    static readonly Dictionary<UnitDef, Branch> cache = new();

    sealed class Agg
    {
        public Company C = null!;
        public Vector2 Pos; public int Alive, Seen; public float Ammo, Supp; public int AmmoN;
        public Branch Main;
    }
    static readonly Dictionary<int, Agg> aggs = new();
    static readonly int[] counts = new int[Enum.GetValues<Branch>().Length];
    static readonly List<Agg> live = new();
    static readonly HashSet<int> hiRegs = new();
    static long lastTick = -100; static int lastSeat = -1;

    static void Refresh(World world, int seat)
    {
        long tick = world.Tick;
        if (tick >= lastTick && tick - lastTick < 6 && seat == lastSeat) return;
        lastTick = tick; lastSeat = seat;
        live.Clear(); hiRegs.Clear();
        var fog = world.Fog;
        bool all = fog.SeesAll(seat);
        var grid = all ? ReadOnlySpan<byte>.Empty : fog.Grid(seat);
        bool allVis = all || grid.Length == 0;
        int mw = world.Map.Width;
        foreach (var c in world.Companies)
        {
            if (!aggs.TryGetValue(c.Id, out var a)) aggs[c.Id] = a = new Agg();
            a.C = c; a.Alive = 0; a.Seen = 0; a.Ammo = a.Supp = 0; a.AmmoN = 0;
            Array.Clear(counts);
            Vector2 sum = default;
            bool sel = false;
            foreach (var u in c.Members)
            {
                if (!u.Alive) continue;
                a.Alive++;
                if (u.Selected) sel = true;
                if (!allVis && u.Owner != seat && grid[(int)MathF.Floor(u.Pos.Y) * mw + (int)MathF.Floor(u.Pos.X)] != 2) continue;
                a.Seen++; sum += u.Pos; counts[(int)Of(u.Def)]++;
                a.Supp += u.SuppressionRatio;
                if (u.UsesAmmo && u.AmmoMax > 0) { a.Ammo += u.Ammo / (float)u.AmmoMax; a.AmmoN++; }
            }
            if (sel && c.Battalion != null) hiRegs.Add(c.Battalion.Id);
            if (a.Seen == 0) continue;
            a.Pos = sum / a.Seen;
            int best = 0; for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            a.Main = (Branch)best;
            live.Add(a);
        }
    }

    /// <summary>병종 → 병종 이미지 키 이름 (테마 키 icon.branch.&lt;이름&gt;@&lt;크기&gt;, assets/themes/kd1920/icons/branch-*.png). 지휘(장교) = officer (kd1920 은 이미지, 옛 테마는 이미지 없음 → NATO 깃발).</summary>
    public static string? IconName(Branch b) => b switch
    {
        Branch.Infantry => "infantry", Branch.MachineGun => "mg", Branch.AntiTank => "at", Branch.Artillery => "artillery",
        Branch.Cavalry => "cavalry", Branch.Armor => "armour", Branch.Supply => "supply", Branch.Scout => "scout", Branch.Assault => "assault", Branch.Howitzer => "howitzer", Branch.Sniper => "sniper", Branch.Officer => "officer",
        _ => null,
    };
    static readonly int[] IconSizes = { 12, 16, 20, 24, 32 };

    /// <summary>병종 이미지 (가장 가까운 크기, 없으면 null → NATO 기호).</summary>
    public static ThemeSprite? BranchIcon(Branch b, int px)
    {
        if (IconName(b) is not { } n) return null;
        int best = IconSizes[0];
        foreach (var s in IconSizes) if (s <= px) best = s;
        return Theme.Current?.Get($"icon.branch.{n}@{best}");
    }

    /// <summary>축소 표시에서 점 대신 기호로 대체되는 유닛인가 (중대 소속).</summary>
    public static bool Replaces(Unit u) => u.Company != null;

    public static void Draw(World world, RtsCamera cam, int seat, Vector2 mouse, bool mouseInView, Rectangle view)
    {
        Refresh(world, seat);
        float sz = 16f;
        Agg? hot = null;
        foreach (var a in live)
        {
            Vector2 s = cam.WorldToScreen(a.Pos);
            if (s.X < view.X - 20 || s.X > view.X + view.Width + 20 || s.Y < view.Y - 20 || s.Y > view.Y + view.Height + 20) continue;
            var c = a.C;
            bool selected = false;
            foreach (var u in c.Members) if (u.Selected) { selected = true; break; }
            bool regHi = !selected && c.Battalion != null && hiRegs.Contains(c.Battalion.Id);
            var icon = BadgeIcon(world, c.Owner, cam.Zoom);
            var r = icon != null ? new Rectangle(s.X - icon.Size.X / 2, s.Y - icon.Size.Y / 2, icon.Size.X, icon.Size.Y) : new Rectangle(s.X - sz * 0.75f, s.Y - sz * 0.5f, sz * 1.5f, sz);
            if (mouseInView && Ui.Contains(new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 12 + (icon != null ? 18 : 0)), mouse)) hot = a;
            if (icon != null) DrawBadge(a, icon, r, selected, regHi, seat); else DrawOne(a, r, selected, regHi, seat);
        }
        if (hot != null) Tooltip(hot, mouse, view);
    }

    static void DrawOne(Agg a, Rectangle r, bool selected, bool regHi, int seat)
    {
        var team = Palette.Player(a.C.Owner);
        var fill = Palette.Alpha(Palette.Shade(team, -70), 235);
        var dark = Palette.Alpha(Color.Black, 220);
        if (selected || regHi)
        {
            var hi = selected ? (a.C.Owner == seat ? Palette.Select : Palette.HpLow) : Palette.Accent;
            Raylib.DrawRectangleLinesEx(new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), 2, hi);
        }
        Raylib.DrawRectangleRec(r, fill);
        Raylib.DrawRectangleLinesEx(r, 2, team);
        Raylib.DrawRectangleLinesEx(new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), 1, dark);
        if (BranchIcon(a.Main, (int)MathF.Min(r.Width, r.Height) - 2) is { } bi)
            Raylib.DrawTexture(bi.Base[0], (int)MathF.Round(r.X + (r.Width - bi.Base[0].Width) / 2), (int)MathF.Round(r.Y + (r.Height - bi.Base[0].Height) / 2), Color.White);
        else DrawGlyph(a.Main, r);
        float cx = r.X + r.Width / 2, h = r.Height, w = r.Width;
        // 병력 막대 (현재 / 편성 당시) — 틀 아래
        float ratio = Math.Clamp(a.Alive / (float)Math.Max(1, Math.Max(a.C.InitialStrength, a.Alive)), 0, 1);
        var bar = new Rectangle(r.X, r.Y + h + 2, w, 3);
        Raylib.DrawRectangleRec(bar, dark);
        Raylib.DrawRectangleRec(new Rectangle(bar.X, bar.Y, bar.Width * ratio, bar.Height), Palette.HpColor(ratio));
        // 중대 번호 (틀 위)
        string num = a.C.Number.ToString();
        Vector2 m = Ui.Measure(num, 13);
        Ui.Text(num, cx - m.X / 2 + 1, r.Y - 14 + 1, 13, dark);
        Ui.Text(num, cx - m.X / 2, r.Y - 14, 13, Palette.Text);
    }

    /// <summary>NATO 식 병종 기호 (사각형 안의 선 · 도형).</summary>
    static void DrawGlyph(Branch main, Rectangle r)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, w = r.Width, h = r.Height;
        var ink = Color.White;
        switch (main)
        {
            case Branch.Infantry:
                Raylib.DrawLineEx(new(r.X, r.Y), new(r.X + w, r.Y + h), 1.5f, ink);
                Raylib.DrawLineEx(new(r.X + w, r.Y), new(r.X, r.Y + h), 1.5f, ink); break;
            case Branch.MachineGun:
                Raylib.DrawLineEx(new(r.X, r.Y), new(r.X + w, r.Y + h), 1.5f, ink);
                Raylib.DrawLineEx(new(r.X + w, r.Y), new(r.X, r.Y + h), 1.5f, ink);
                Raylib.DrawLineEx(new(cx, r.Y), new(cx, r.Y + h * 0.55f), 2f, ink); break;
            case Branch.AntiTank:
                Raylib.DrawTriangleLines(new(cx, r.Y + 2), new(r.X + 2, r.Y + h - 2), new(r.X + w - 2, r.Y + h - 2), ink); break;
            case Branch.Artillery:
                Raylib.DrawCircleV(new(cx, cy), 3.2f, ink); break;
            case Branch.Howitzer:
                Raylib.DrawCircleV(new(cx, cy), 3.2f, ink);
                Raylib.DrawLineEx(new(cx, r.Y), new(cx, r.Y + h * 0.3f), 1.5f, ink); break;
            case Branch.Sniper:
                Raylib.DrawLineEx(new(r.X, r.Y), new(r.X + w, r.Y + h), 1.5f, ink);
                Raylib.DrawLineEx(new(r.X + w, r.Y), new(r.X, r.Y + h), 1.5f, ink);
                Raylib.DrawCircleLinesV(new(cx, cy), 3f, ink); break;
            case Branch.Cavalry:
                Raylib.DrawLineEx(new(r.X, r.Y + h), new(r.X + w, r.Y), 1.5f, ink); break;
            case Branch.Armor:
                Raylib.DrawEllipseLines((int)cx, (int)cy, w * 0.32f, h * 0.28f, ink);
                Raylib.DrawEllipseLines((int)cx, (int)cy, w * 0.32f - 1, h * 0.28f - 1, ink); break;
            case Branch.Supply:
                Raylib.DrawLineEx(new(r.X + 3, cy), new(r.X + w - 3, cy), 2f, ink);
                Raylib.DrawCircleLinesV(new(cx - w * 0.2f, cy + h * 0.28f), 1.8f, ink);
                Raylib.DrawCircleLinesV(new(cx + w * 0.2f, cy + h * 0.28f), 1.8f, ink); break;
            case Branch.Officer:
                Raylib.DrawLineEx(new(r.X + 4, r.Y + 2), new(r.X + 4, r.Y + h - 2), 1.5f, ink);
                Raylib.DrawRectangleRec(new(r.X + 5, r.Y + 2, w * 0.45f, h * 0.4f), ink); break;
        }
    }

    /// <summary>세력 휘장 아이콘 (테마 키 icon.badge.&lt;세력&gt;@&lt;크기&gt;). 크기 = 줌에 맞춰 16/24/32. 아이콘이 없으면 (옛 테마) null → NATO 기호.</summary>
    static ThemeSprite? BadgeIcon(World world, int owner, float zoom)
    {
        string f = FactionArt.Variant(world, owner) switch { "japanese" => "japanese", "fengtian" => "fengtian", "russian-white" => "russian", "russian-red" => "soviet", _ => "korean" };
        int px = zoom < 0.18f ? 16 : zoom < 0.3f ? 24 : 32;
        return Theme.Current?.Get($"icon.badge.{f}@{px}");
    }

    /// <summary>휘장 아이콘 한 개 + 세력 색 테두리 + 작은 병종 기호 (우하단) + 병력 막대 + 번호.</summary>
    static void DrawBadge(Agg a, ThemeSprite icon, Rectangle r, bool selected, bool regHi, int seat)
    {
        var team = Palette.Player(a.C.Owner);
        var dark = Palette.Alpha(Color.Black, 220);
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = r.Width / 2;
        if (selected || regHi)
        {
            var hi = selected ? (a.C.Owner == seat ? Palette.Select : Palette.HpLow) : Palette.Accent;
            Raylib.DrawRing(new Vector2(cx, cy), rad + 2, rad + 4, 0, 360, 36, hi);
        }
        Raylib.DrawTexture(icon.Base[0], (int)MathF.Round(r.X), (int)MathF.Round(r.Y), Color.White);
        Raylib.DrawRing(new Vector2(cx, cy), rad - 0.5f, rad + 1.2f, 0, 360, 36, team);   // 같은 세력 · 같은 휘장끼리 구분
        // 병종 이미지: 세력 휘장 바로 아래 (휘장 + 병종 그림). 이미지가 없으면 (옛 테마 · 지휘) 우하단 작은 NATO 기호.
        float barY = r.Y + r.Height + 3;
        if (BranchIcon(a.Main, (int)(r.Width * 0.6f)) is { } bi)
        {
            int bw = bi.Base[0].Width, bh = bi.Base[0].Height;
            Raylib.DrawTexture(bi.Base[0], (int)MathF.Round(cx - bw / 2f), (int)MathF.Round(r.Y + r.Height + 1), Color.White);
            barY = r.Y + r.Height + 1 + bh + 2;
        }
        else
        {
            float gw = MathF.Max(10, r.Width * 0.5f), gh = gw * 0.66f;
            var g = new Rectangle(r.X + r.Width - gw * 0.85f, r.Y + r.Height - gh * 0.85f, gw, gh);
            Raylib.DrawRectangleRec(g, Palette.Alpha(Palette.Shade(team, -70), 240));
            Raylib.DrawRectangleLinesEx(g, 1, team);
            DrawGlyph(a.Main, g);
        }
        float ratio = Math.Clamp(a.Alive / (float)Math.Max(1, Math.Max(a.C.InitialStrength, a.Alive)), 0, 1);
        var bar = new Rectangle(r.X, barY, r.Width, 3);
        Raylib.DrawRectangleRec(bar, dark);
        Raylib.DrawRectangleRec(new Rectangle(bar.X, bar.Y, bar.Width * ratio, bar.Height), Palette.HpColor(ratio));
        string num = a.C.Number.ToString();
        Vector2 m = Ui.Measure(num, 13);
        Ui.Text(num, cx - m.X / 2 + 1, r.Y - 14 + 1, 13, dark);
        Ui.Text(num, cx - m.X / 2, r.Y - 14, 13, Palette.Text);
    }

    static void Tooltip(Agg a, Vector2 mouse, Rectangle view)
    {
        var c = a.C;
        string reg = c.Battalion is { } rg ? "  ·  " + string.Format(S.SymBattalion, rg.Number) : "";
        string l1 = string.Format(S.SymCompany, c.Number) + $"{reg}  ({BranchName[(int)a.Main]})";
        string l2 = $"{S.SymStrength} {a.Alive}/{Math.Max(c.InitialStrength, a.Alive)}";
        string l3 = a.AmmoN > 0 ? $"{S.SymAmmoAvg} {a.Ammo / a.AmmoN * 100:0}%  ·  {S.SymSuppAvg} {a.Supp / Math.Max(1, a.Seen) * 100:0}%"
                                : $"{S.SymSuppAvg} {a.Supp / Math.Max(1, a.Seen) * 100:0}%";
        float tw = MathF.Max(Ui.Measure(l1, 16).X, MathF.Max(Ui.Measure(l2, 16).X, Ui.Measure(l3, 16).X)) + 20;
        var r = new Rectangle(Math.Min(mouse.X + 16, view.X + view.Width - tw - 4), Math.Min(mouse.Y + 16, view.Y + view.Height - 74), tw, 70);
        Ui.Panel(r, Palette.Alpha(Palette.PanelDark, 240), Palette.HudTrim);
        Ui.Text(l1, r.X + 10, r.Y + 6, 16, Palette.Accent);
        Ui.Text(l2, r.X + 10, r.Y + 26, 16, Palette.Text);
        Ui.Text(l3, r.X + 10, r.Y + 46, 16, Palette.Text);
    }
}
