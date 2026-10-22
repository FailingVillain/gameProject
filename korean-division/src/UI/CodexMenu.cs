using System.Numerics;
using Raylib_cs;
using KoreanDivision.Core;
using KoreanDivision.Render;

namespace KoreanDivision.UI;

/// <summary>
/// 병종 도감 화면: 게임에 등록된 모든 유닛의 성능, 무장, 제원, 배경 설정을 탐색한다.
/// 세력 필터 및 병과 필터를 지원하며 유닛 3D/스프라이트 회전 미리보기를 제공한다.
/// </summary>
public sealed class CodexMenu
{
    public enum FactionFilter
    {
        All,
        Korean,
        Japanese,
        Fengtian,
        Russian,
        Bandit
    }

    public enum CategoryFilter
    {
        All,
        Infantry,
        Heavy,
        Cavalry,
        Armor
    }

    public bool BackRequested { get; set; }

    FactionFilter facFilter = FactionFilter.All;
    CategoryFilter catFilter = CategoryFilter.All;

    string? selectedKey;
    int scrollIndex = 0;
    float previewAngle = MathF.PI / 2f;
    bool autoRotate = true;

    Vector2 mouse;
    bool click;

    public void Draw(int screenW, int screenH)
    {
        if (Defs.Current == null) return;

        mouse = Input.GetMousePosition();
        click = Input.IsMouseButtonPressed(MouseButton.Left);

        if (Input.IsKeyPressed(KeyboardKey.Escape))
        {
            BackRequested = true;
            return;
        }

        float pw = MathF.Min(1040, screenW - 40);
        float ph = MathF.Min(640, screenH - 50);
        float px = (screenW - pw) / 2;
        float py = MathF.Max(12, (screenH - ph) / 2 - 10);
        var panel = new Rectangle(px, py, pw, ph);

        // 메인 패널
        Ui.Panel(panel, Palette.Alpha(Palette.PanelDark, 240), Palette.HudTrim);

        // 상단 헤더 타이틀
        Ui.Text(S.CodexTitle, px + 24, py + 16, 26, Palette.Accent);

        // 상단 필터 바
        DrawFilterBar(px + 24, py + 52, pw - 48);

        // 필터링된 유닛 목록 취득
        var filtered = GetFilteredUnits();
        if (filtered.Count == 0)
        {
            selectedKey = null;
        }
        else if (selectedKey == null || !filtered.Any(u => u.Key == selectedKey))
        {
            selectedKey = filtered[0].Key;
        }

        // 키보드 위/아래 이동
        HandleKeyboardNav(filtered);

        // 본문 분할 영역
        float contentY = py + 116;
        float bottomBarH = 46;
        float contentH = py + ph - contentY - bottomBarH - 12;

        float listW = 290;
        var listRect = new Rectangle(px + 20, contentY, listW, contentH);
        float detailX = listRect.X + listW + 16;
        float detailW = px + pw - detailX - 20;
        var detailRect = new Rectangle(detailX, contentY, detailW, contentH);

        // 좌측 목록
        DrawUnitList(listRect, filtered);

        // 우측 상세 패널
        UnitDef? selectedDef = selectedKey != null && Defs.Current.Units.TryGetValue(selectedKey, out var def) ? def : null;
        DrawDetailPanel(detailRect, selectedDef);

        // 하단 [뒤로] 버튼
        var backBtnRect = new Rectangle(px + pw - 130 - 20, py + ph - 44, 130, 34);
        if (Button(backBtnRect, S.CodexBack, size: 20))
        {
            BackRequested = true;
        }
    }

    // ---------------------------------------------------------------- 필터 바

    void DrawFilterBar(float x, float y, float w)
    {
        // 1행: 세력 필터
        float chipH = 26;
        float curX = x;

        var facs = new[]
        {
            (FactionFilter.All, S.CodexFacAll),
            (FactionFilter.Korean, S.CodexFacKorean),
            (FactionFilter.Japanese, S.CodexFacJapanese),
            (FactionFilter.Fengtian, S.CodexFacFengtian),
            (FactionFilter.Russian, S.CodexFacRussian),
            (FactionFilter.Bandit, S.CodexFacBandit)
        };

        foreach (var (fac, label) in facs)
        {
            float chipW = Ui.Measure(label, 16).X + 22;
            var r = new Rectangle(curX, y, chipW, chipH);
            if (Chip(r, label, facFilter == fac))
            {
                facFilter = fac;
                scrollIndex = 0;
            }
            curX += chipW + 8;
        }

        // 2행: 병과 필터
        curX = x;
        float y2 = y + chipH + 6;

        var cats = new[]
        {
            (CategoryFilter.All, S.CodexCatAll),
            (CategoryFilter.Infantry, S.CodexCatInf),
            (CategoryFilter.Heavy, S.CodexCatHeavy),
            (CategoryFilter.Cavalry, S.CodexCatCav),
            (CategoryFilter.Armor, S.CodexCatArmor)
        };

        foreach (var (cat, label) in cats)
        {
            float chipW = Ui.Measure(label, 16).X + 22;
            var r = new Rectangle(curX, y2, chipW, chipH);
            if (Chip(r, label, catFilter == cat))
            {
                catFilter = cat;
                scrollIndex = 0;
            }
            curX += chipW + 8;
        }
    }

    // ---------------------------------------------------------------- 키보드 탐색

    void HandleKeyboardNav(List<UnitDef> filtered)
    {
        if (filtered.Count == 0) return;
        int curIdx = filtered.FindIndex(u => u.Key == selectedKey);
        if (curIdx < 0) curIdx = 0;

        if (Input.IsKeyPressed(KeyboardKey.Up) && curIdx > 0)
        {
            selectedKey = filtered[curIdx - 1].Key;
            EnsureVisible(curIdx - 1);
        }
        else if (Input.IsKeyPressed(KeyboardKey.Down) && curIdx < filtered.Count - 1)
        {
            selectedKey = filtered[curIdx + 1].Key;
            EnsureVisible(curIdx + 1);
        }
    }

    void EnsureVisible(int targetIdx)
    {
        const float itemH = 40;
        int visibleCount = 10;
        if (targetIdx < scrollIndex) scrollIndex = targetIdx;
        else if (targetIdx >= scrollIndex + visibleCount) scrollIndex = targetIdx - visibleCount + 1;
    }

    // ---------------------------------------------------------------- 유닛 목록

    void DrawUnitList(Rectangle r, List<UnitDef> list)
    {
        Ui.Panel(r, Palette.Alpha(Palette.Void, 180), Palette.PanelBorder);

        float headerH = 24;
        Raylib.DrawRectangleRec(new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, headerH), Palette.Alpha(Palette.PanelDark, 180));
        Raylib.DrawLineV(new Vector2(r.X, r.Y + headerH), new Vector2(r.X + r.Width, r.Y + headerH), Palette.PanelBorder);
        Ui.Text(string.Format(S.CodexUnitCount, list.Count), r.X + 10, r.Y + 4, 16, Palette.TextDim);

        var bodyRect = new Rectangle(r.X, r.Y + headerH, r.Width, r.Height - headerH);

        if (list.Count == 0)
        {
            Ui.TextCentered(S.CodexNoUnits, bodyRect, 16, Palette.TextDim);
            return;
        }

        const float itemH = 38;
        int visibleCount = (int)(bodyRect.Height / itemH);
        int maxScroll = Math.Max(0, list.Count - visibleCount);

        // 마우스 휠
        float wheel = Input.GetMouseWheelMove();
        if (wheel != 0 && Ui.Contains(r, mouse))
        {
            scrollIndex = Math.Clamp(scrollIndex - (int)wheel * 2, 0, maxScroll);
        }
        else
        {
            scrollIndex = Math.Clamp(scrollIndex, 0, maxScroll);
        }

        // 목록 항목 출력
        int end = Math.Min(list.Count, scrollIndex + visibleCount);
        for (int i = scrollIndex; i < end; i++)
        {
            var u = list[i];
            bool isSelected = u.Key == selectedKey;
            var itemRect = new Rectangle(bodyRect.X + 4, bodyRect.Y + (i - scrollIndex) * itemH + 2, bodyRect.Width - 8, itemH - 4);
            bool hover = Ui.Contains(itemRect, mouse);

            if (isSelected)
            {
                Raylib.DrawRectangleRounded(itemRect, 0.15f, 4, Palette.Alpha(Palette.BtnActive, 180));
                Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.15f, 4, 1.5f, Palette.Accent);
            }
            else if (hover)
            {
                Raylib.DrawRectangleRounded(itemRect, 0.15f, 4, Palette.Alpha(Palette.BtnHover, 140));
            }

            // 미니 바디 아이콘
            var iconCenter = new Vector2(itemRect.X + 20, itemRect.Y + itemRect.Height / 2f);
            Color teamCol = GetUnitFactionColor(u);
            UnitRenderer.DrawBody(iconCenter, 13f, teamCol, MathF.PI / 2f, u.Key);

            // 유닛 이름
            Color nameCol = isSelected ? Palette.Accent : Palette.Text;
            Ui.Text(u.Name, itemRect.X + 42, itemRect.Y + 5, 16, nameCol);

            // 병과 배지
            string badge = GetBadgeLabel(u);
            float badgeW = Ui.Measure(badge, 16).X;
            Ui.Text(badge, itemRect.X + itemRect.Width - badgeW - 8, itemRect.Y + 5, 16, Palette.TextDim);

            if (hover && click)
            {
                selectedKey = u.Key;
            }
        }

        // 스크롤바 인디케이터
        if (maxScroll > 0)
        {
            float barTotalH = bodyRect.Height - 8;
            float barThumbH = MathF.Max(20, barTotalH * visibleCount / list.Count);
            float barThumbY = bodyRect.Y + 4 + (barTotalH - barThumbH) * (scrollIndex / (float)maxScroll);
            var barRect = new Rectangle(bodyRect.X + bodyRect.Width - 6, barThumbY, 4, barThumbH);
            Raylib.DrawRectangleRounded(barRect, 0.5f, 2, Palette.PanelBorder);
        }
    }

    // ---------------------------------------------------------------- 상세 정보 패널

    void DrawDetailPanel(Rectangle r, UnitDef? u)
    {
        Ui.Panel(r, Palette.Alpha(Palette.Void, 180), Palette.PanelBorder);

        if (u == null)
        {
            Ui.TextCentered(S.CodexNoUnits, r, 16, Palette.TextDim);
            return;
        }

        // 1. 상단 미리보기 & 요약
        float topH = 120;
        var previewBox = new Rectangle(r.X + 16, r.Y + 14, 116, topH);
        DrawPreviewBox(previewBox, u);

        float infoX = previewBox.X + previewBox.Width + 18;
        Ui.Text(u.Name, infoX, r.Y + 14, 26, Palette.Accent);
        Ui.Text(string.Format(S.CodexKeyLabel, u.Key), infoX, r.Y + 44, 16, Palette.TextDim);

        string badge = GetBadgeLabel(u);
        Ui.Text(string.Format(S.CodexCatLabel, badge), infoX, r.Y + 68, 16, Palette.Text);

        string facLabel = GetFactionRestrictionLabel(u);
        Ui.Text(string.Format(S.CodexFacLabel, facLabel), infoX, r.Y + 92, 16, Palette.Text);

        // 2. 중단 능력치 2열 표
        float statsY = r.Y + topH + 28;
        float colGap = 16;
        float colW = (r.Width - 32 - colGap) / 2f;
        float col1X = r.X + 16;
        float col2X = col1X + colW + colGap;
        float statsH = 175;

        DrawBasicStats(new Rectangle(col1X, statsY, colW, statsH), u);
        DrawWeaponStats(new Rectangle(col2X, statsY, colW, statsH), u);

        // 3. 하단 상세 설명
        float descY = statsY + statsH + 12;
        float descH = r.Y + r.Height - descY - 14;
        var descBox = new Rectangle(r.X + 16, descY, r.Width - 32, descH);

        Ui.Panel(descBox, Palette.Alpha(Palette.PanelDark, 190), Palette.PanelBorder);
        Ui.Text(S.CodexSecDesc, descBox.X + 12, descBox.Y + 8, 16, Palette.Accent);

        var textBox = new Rectangle(descBox.X + 12, descBox.Y + 30, descBox.Width - 24, descBox.Height - 34);
        DrawWrappedText(u.Description, textBox, 16, Palette.Text);
    }

    // ---------------------------------------------------------------- 미리보기 박스 & 회전

    void DrawPreviewBox(Rectangle box, UnitDef u)
    {
        // 박스 배경 및 격자선
        Raylib.DrawRectangleRec(box, Palette.Alpha(Palette.PanelDark, 230));
        for (float gx = box.X + 14; gx < box.X + box.Width; gx += 16)
            Raylib.DrawLineV(new Vector2(gx, box.Y), new Vector2(gx, box.Y + box.Height), Palette.Alpha(Palette.PanelBorder, 50));
        for (float gy = box.Y + 14; gy < box.Y + box.Height; gy += 16)
            Raylib.DrawLineV(new Vector2(box.X, gy), new Vector2(box.X + box.Width, gy), Palette.Alpha(Palette.PanelBorder, 50));
        Raylib.DrawRectangleLinesEx(box, 1, Palette.PanelBorder);

        // 회전 업데이트
        if (autoRotate)
        {
            previewAngle += Raylib.GetFrameTime() * 0.9f;
            if (previewAngle > MathF.PI * 2f) previewAngle -= MathF.PI * 2f;
        }

        // 유닛 렌더링
        var center = new Vector2(box.X + box.Width / 2f, box.Y + box.Height / 2f - 10);
        Color teamCol = GetUnitFactionColor(u);
        UnitRenderer.DrawBody(center, 34f, teamCol, previewAngle, u.Key);

        // 박스 아래 조작 버튼 (◀, 토글, ▶)
        float btnY = box.Y + box.Height - 24;
        float bW = 28;
        var btnL = new Rectangle(box.X + 6, btnY, bW, 20);
        var btnPause = new Rectangle(box.X + 6 + bW + 4, btnY, box.Width - 12 - bW * 2 - 8, 20);
        var btnR = new Rectangle(box.X + box.Width - 6 - bW, btnY, bW, 20);

        if (Button(btnL, S.CodexRotateLeft, size: 16))
        {
            previewAngle -= 0.5f;
            autoRotate = false;
        }
        if (Button(btnPause, autoRotate ? S.CodexRotatePaused : S.CodexRotateAuto, size: 16))
        {
            autoRotate = !autoRotate;
        }
        if (Button(btnR, S.CodexRotateRight, size: 16))
        {
            previewAngle += 0.5f;
            autoRotate = false;
        }
    }

    // ---------------------------------------------------------------- 기본 스펙 표

    void DrawBasicStats(Rectangle r, UnitDef u)
    {
        Ui.Panel(r, Palette.Alpha(Palette.PanelDark, 160), Palette.PanelBorder);
        Ui.Text(S.CodexSecBasic, r.X + 12, r.Y + 8, 16, Palette.Accent);

        float lineY = r.Y + 32;
        float rowH = 20;
        float lblW = 85;

        void Row(string label, string val)
        {
            Ui.Text(label, r.X + 12, lineY, 16, Palette.TextDim);
            Ui.Text(val, r.X + 12 + lblW, lineY, 16, Palette.Text);
            lineY += rowH;
        }

        Row(S.CodexStatHp, $"{u.Hp}");
        Row(S.CodexStatSpeed, string.Format(S.CodexUnitTilesPerSec, u.Speed));

        string sightStr = u.SightNear > 0 ? string.Format(S.CodexSightNearFar, u.SightNear, u.SightFar) : string.Format(S.CodexSightSingle, u.Sight);
        if (u.Concealment > 0) sightStr += $" / {S.CodexStatConceal} {u.Concealment}%";
        Row(S.CodexStatSight, sightStr);

        string suppStr = u.SuppressResist <= 0 ? S.CodexSuppressOfficer
            : MathF.Abs(u.SuppressResist - 0.5f) < 0.05f ? S.CodexSuppressElite
            : MathF.Abs(u.SuppressResist - 0.75f) < 0.05f ? S.CodexSuppressRegular
            : MathF.Abs(u.SuppressResist - 1.0f) < 0.05f ? S.CodexSuppressRecruit
            : string.Format(S.CodexSuppressSpecial, u.SuppressResist);
        Row(S.CodexStatSuppressResist, suppStr);

        string armorStr = u.Armor != null && Defs.Current.Armors.TryGetValue(u.Armor, out var a) ? a.Name : S.CodexNone;
        Row(S.CodexStatArmor, armorStr);

        string shellStr = u.ShellDamageMul < 0.99f ? string.Format(S.CodexShellReduced, (int)MathF.Round(u.ShellDamageMul * 100)) : S.CodexNone;
        Row(S.CodexStatShellResist, shellStr);

        string deployStr = u.SetupTime > 0 ? string.Format(S.CodexDeployTime, u.SetupTime, u.PackTime) : S.CodexNone;
        Row(S.CodexStatDeploy, deployStr);
    }

    // ---------------------------------------------------------------- 무장 스펙 표

    void DrawWeaponStats(Rectangle r, UnitDef u)
    {
        Ui.Panel(r, Palette.Alpha(Palette.PanelDark, 160), Palette.PanelBorder);
        Ui.Text(S.CodexSecWeapon, r.X + 12, r.Y + 8, 16, Palette.Accent);

        float lineY = r.Y + 32;
        float rowH = 20;
        float lblW = 85;

        void Row(string label, string val)
        {
            Ui.Text(label, r.X + 12, lineY, 16, Palette.TextDim);
            Ui.Text(val, r.X + 12 + lblW, lineY, 16, Palette.Text);
            lineY += rowH;
        }

        var w = Defs.Current.Weapon(u.Weapon);
        if (w != null)
        {
            Row(S.CodexStatWeapon, w.Name);

            string rangeStr = w.MinRange > 0
                ? string.Format(S.CodexRangeMinMax, w.MinRange, w.Range)
                : (w.Indirect ? string.Format(S.CodexRangeIndirect, w.Range) : string.Format(S.CodexRangeDirect, w.Range));
            Row(S.CodexStatRange, rangeStr);

            Row(S.CodexStatDamageRate, string.Format(S.CodexDamageRateFormat, w.Damage, w.CooldownMean));

            string accStr = w.AccNear > 0
                ? $"{(int)MathF.Round(w.AccNear * 100)}%~{(int)MathF.Round(w.AccFar * 100)}%"
                : $"{(int)MathF.Round(w.Accuracy * 100)}%";
            Row(S.CodexStatAccuracy, accStr);

            Row(S.CodexStatAmmo, w.Ammo > 0 ? string.Format(S.CodexUnitRounds, w.Ammo) : S.CodexInfinite);

            string splashStr = w.Splash > 0
                ? string.Format(S.CodexUnitTiles, w.Splash)
                : (w.Spray > 0 ? string.Format(S.CodexSpray, w.Spray) : S.CodexSingleTarget);
            Row(S.CodexStatSplash, splashStr);

            string suppStr = w.Suppress > 0 ? $"{w.Suppress:0}" : S.CodexNone;
            Row(S.CodexStatSuppress, suppStr);
        }
        else
        {
            Row(S.CodexStatWeapon, S.CodexUnarmed);
            Row(S.CodexStatRange, S.CodexNone);
            Row(S.CodexStatDamageRate, S.CodexNone);
            Row(S.CodexStatAccuracy, S.CodexNone);
            Row(S.CodexStatAmmo, S.CodexNone);
            Row(S.CodexStatSplash, S.CodexNone);
            Row(S.CodexStatSuppress, S.CodexNone);
        }

        // 보조/근접/수류탄을 하단에 간략히 표기
        float extraY = r.Y + r.Height - 24;
        var sb = new System.Text.StringBuilder();
        if (u.Sidearm != null)
        {
            string saName = Defs.Current.Weapon(u.Sidearm)?.Name ?? u.Sidearm;
            sb.Append($"{S.CodexStatSidearm}: {saName} ");
        }
        if (u.Melee != null)
        {
            string melName = Defs.Current.Weapon(u.Melee)?.Name ?? u.Melee;
            sb.Append($"{S.CodexStatMelee}: {melName} ");
        }
        if (u.Grenades > 0)
        {
            sb.Append($"{S.CodexStatGrenade}: {string.Format(S.CodexUnitGrenadeCount, u.Grenades)}");
        }
        if (sb.Length > 0)
        {
            Ui.Text(sb.ToString(), r.X + 12, extraY, 16, Palette.TextDim);
        }
    }

    // ---------------------------------------------------------------- 자동 줄바꿈 텍스트 렌더러

    void DrawWrappedText(string text, Rectangle r, float size, Color color, float lineSpacing = 4f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        float lineHeight = size + lineSpacing;
        float curY = r.Y;
        float maxW = r.Width;

        string[] paragraphs = text.Split('\n');
        foreach (var para in paragraphs)
        {
            if (curY + size > r.Y + r.Height) break;
            if (string.IsNullOrWhiteSpace(para))
            {
                curY += lineHeight * 0.5f;
                continue;
            }

            string[] words = para.Split(' ');
            var lineSb = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                string testLine = lineSb.Length == 0 ? word : lineSb + " " + word;
                if (Ui.Measure(testLine, size).X <= maxW)
                {
                    lineSb.Clear();
                    lineSb.Append(testLine);
                }
                else
                {
                    if (lineSb.Length > 0)
                    {
                        Ui.Text(lineSb.ToString(), r.X, curY, size, color);
                        curY += lineHeight;
                        if (curY + size > r.Y + r.Height) break;
                        lineSb.Clear();
                    }

                    if (Ui.Measure(word, size).X > maxW)
                    {
                        foreach (char ch in word)
                        {
                            string testChar = lineSb.ToString() + ch;
                            if (Ui.Measure(testChar, size).X <= maxW)
                            {
                                lineSb.Append(ch);
                            }
                            else
                            {
                                Ui.Text(lineSb.ToString(), r.X, curY, size, color);
                                curY += lineHeight;
                                if (curY + size > r.Y + r.Height) break;
                                lineSb.Clear();
                                lineSb.Append(ch);
                            }
                        }
                    }
                    else
                    {
                        lineSb.Append(word);
                    }
                }
            }

            if (lineSb.Length > 0 && curY + size <= r.Y + r.Height)
            {
                Ui.Text(lineSb.ToString(), r.X, curY, size, color);
                curY += lineHeight;
            }
        }
    }

    // ---------------------------------------------------------------- 분류 및 필터링 헬퍼

    // 필터 결과 캐시 (도감은 매 프레임 그려지므로 필터 · 정의 집합이 바뀔 때만 다시 거른다)
    List<UnitDef>? cachedList; FactionFilter cachedFac; CategoryFilter cachedCat; object? cachedDefs;

    List<UnitDef> GetFilteredUnits()
    {
        var defs = Defs.Current;
        if (defs == null) return new();
        if (cachedList == null || !ReferenceEquals(cachedDefs, defs) || cachedFac != facFilter || cachedCat != catFilter)
        {
            cachedList = Filter(defs.Units.Values, facFilter, catFilter);
            cachedDefs = defs; cachedFac = facFilter; cachedCat = catFilter;
        }
        return cachedList;
    }

    /// <summary>필터 적용 (테스트용으로 공개 · 정적).</summary>
    public static List<UnitDef> Filter(IEnumerable<UnitDef> units, FactionFilter fac, CategoryFilter cat) =>
        units.Where(u => MatchesFaction(u, fac) && MatchesCategory(u, cat)).ToList();

    static bool MatchesFaction(UnitDef u, FactionFilter filter)
    {
        if (filter == FactionFilter.All) return true;
        string key = u.Key.ToLowerInvariant();
        bool isBandit = key.StartsWith("bandit_") || HasFaction(u, "bandit");
        bool isJapanese = key.Contains("japanese") || key.Contains("type3") || HasFaction(u, "japanese");
        bool isFengtian = key.Contains("fengtian") || HasFaction(u, "fengtian");
        bool isRussian = key.Contains("russian") || key.Contains("soviet") || key.Contains("cossack") || HasFaction(u, "russian") || HasFaction(u, "soviet");

        switch (filter)
        {
            case FactionFilter.Bandit:
                return isBandit;
            case FactionFilter.Japanese:
                return isJapanese;
            case FactionFilter.Fengtian:
                return isFengtian;
            case FactionFilter.Russian:
                return isRussian;
            case FactionFilter.Korean:
                if (u.KoreaOnly) return true;
                if (HasFaction(u, "korean")) return true;
                if (!isBandit && !isJapanese && !isFengtian && !isRussian) return true;
                return false;
            default:
                return true;
        }
    }

    static bool HasFaction(UnitDef u, string fac) =>
        u.Factions != null && u.Factions.Any(f => string.Equals(f, fac, StringComparison.OrdinalIgnoreCase));

    static bool MatchesCategory(UnitDef u, CategoryFilter filter)
    {
        if (filter == CategoryFilter.All) return true;
        switch (filter)
        {
            case CategoryFilter.Armor:
                return u.IsArmored || u.IsSupplyWagon || u.Category == "wagon";
            case CategoryFilter.Cavalry:
                return u.Mounted;
            case CategoryFilter.Heavy:
                return !u.Mounted && !u.IsArmored && (u.Heavy || u.Category == "gun");
            case CategoryFilter.Infantry:
                return !u.Mounted && !u.IsArmored && !u.Heavy && u.Category != "gun" && !u.IsSupplyWagon && u.Category != "wagon";
            default:
                return true;
        }
    }

    string GetBadgeLabel(UnitDef u)
    {
        if (u.IsArmored) return S.CodexBadgeArmor;
        if (u.IsSupplyWagon || u.Category == "wagon") return S.CodexBadgeWagon;
        if (u.Mounted) return S.CodexBadgeCav;
        if (u.Category == "gun") return S.CodexBadgeGun;
        if (u.Heavy) return S.CodexBadgeHeavy;
        if (u.IsOfficer) return S.CodexBadgeOfficer;
        if (u.Scout) return S.CodexBadgeScout;
        return S.CodexBadgeInf;
    }

    string GetFactionRestrictionLabel(UnitDef u)
    {
        if (u.Common) return S.CodexFacCommon;
        if (u.KoreaOnly) return S.CodexFacKoreanOnly;

        if (u.Factions != null && u.Factions.Length > 0)
        {
            if (u.Factions.Length == 1 && string.Equals(u.Factions[0], "bandit", StringComparison.OrdinalIgnoreCase))
                return S.CodexFacBanditOnly;
            if (u.Factions.Length == 1 && string.Equals(u.Factions[0], "japanese", StringComparison.OrdinalIgnoreCase))
                return S.CodexFacJapaneseOnly;
            if (u.Factions.Length == 1 && string.Equals(u.Factions[0], "fengtian", StringComparison.OrdinalIgnoreCase))
                return S.CodexFacFengtianOnly;

            var names = u.Factions.Select(f => f.ToLowerInvariant() switch
            {
                "korean" => S.CodexFacKorean,
                "japanese" => S.CodexFacJapanese,
                "fengtian" => S.CodexFacFengtian,
                "russian" or "soviet" => S.CodexFacRussian,
                "bandit" => S.CodexFacBandit,
                _ => f
            }).Distinct();
            return string.Join(", ", names);
        }

        string k = u.Key.ToLowerInvariant();
        if (k.StartsWith("bandit_")) return S.CodexFacBanditOnly;
        if (k.Contains("japanese") || k.Contains("type3")) return S.CodexFacJapaneseOnly;
        if (k.Contains("fengtian")) return S.CodexFacFengtianOnly;
        if (k.Contains("russian") || k.Contains("soviet")) return S.CodexFacRussianOnly;

        return S.CodexFacCommon;
    }

    Color GetUnitFactionColor(UnitDef u)
    {
        string k = u.Key.ToLowerInvariant();
        if (k.StartsWith("bandit_") || HasFaction(u, "bandit"))
            return new Color(200, 130, 60, 255); // 마적
        if (k.Contains("japanese") || k.Contains("type3") || HasFaction(u, "japanese"))
            return Palette.Player(2); // 일본군 (적색)
        if (k.Contains("fengtian") || HasFaction(u, "fengtian"))
            return Palette.Player(4); // 봉천군벌 (황색)
        if (k.Contains("russian") || k.Contains("soviet") || HasFaction(u, "russian") || HasFaction(u, "soviet"))
            return Palette.Player(3); // 러시아 (녹색)
        return Palette.Player(1); // 대한제국 (청색)
    }

    // ---------------------------------------------------------------- UI 위젯 (버튼 · 칩)

    bool Button(Rectangle r, string label, bool enabled = true, bool active = false, int size = 16)
    {
        bool hover = enabled && Ui.Contains(r, mouse);
        Color fill = !enabled ? Palette.PanelDark : active ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.2f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 4, 1, hover ? Palette.Accent : Palette.PanelBorder);
        Ui.TextCentered(label, r, size, enabled ? Palette.Text : Palette.TextDim);
        return enabled && hover && click;
    }

    bool Chip(Rectangle r, string label, bool active)
    {
        bool hover = Ui.Contains(r, mouse);
        Color fill = active ? Palette.BtnActive : hover ? Palette.BtnHover : Palette.Btn;
        Raylib.DrawRectangleRounded(r, 0.25f, 4, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.25f, 4, active ? 1.5f : 1f, active ? Palette.Accent : Palette.PanelBorder);
        Ui.TextCentered(label, r, 16, active ? Palette.Accent : Palette.Text);
        return hover && click;
    }
}
