using KoreanDivision.Core;

namespace KoreanDivision;

/// <summary>통신 · 난이도 (docs/COMMS_ALPHA_PLAN.md) — 게임 쪽 연결. 조각 ①: 난이도 키를 월드에 넣는다 (틱 0 전).</summary>
public sealed partial class Game
{
    /// <summary>--difficulty 가 있으면 그 값, 캠페인 미션이면 캠페인 저장의 난이도, 아니면 기본 (difficulty.json default).</summary>
    static void ApplyDifficultyOption(World w, GameOptions opt, bool campaign = false)
    {
        string? key = opt.Difficulty;
        if (key == null && campaign && CampaignState.Load() is { Difficulty: { Length: > 0 } cd }) key = cd;
        if (key != null) w.Difficulty = key;
    }
}
