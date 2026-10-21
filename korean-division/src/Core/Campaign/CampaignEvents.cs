using System.Text.Json;

namespace KoreanDivision.Core;

public sealed class CampaignChoiceDef
{
    public string Text { get; set; } = "";
    public string Effect { get; set; } = "";
}

public sealed class CampaignEventDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Text { get; set; } = "";
    public List<CampaignChoiceDef> Choices { get; set; } = new();
}

/// <summary>
/// 본국/지휘부 이벤트 로드 및 선택지 적용 (docs/CAMPAIGN_MVP_SPEC.md §5).
/// E1(시위대 전진 배치), E2(공적 가로채기), E3(출혈 강요), E4(황손의 훈수), E5(군단장의 만류).
/// </summary>
public static class CampaignEvents
{
    private static List<CampaignEventDef>? _events;
    public static IReadOnlyList<CampaignEventDef> Current => _events ??= Load();

    public static string DefaultPath
    {
        get
        {
            string baseDir = AppContext.BaseDirectory;
            string p = Path.Combine(baseDir, "assets", "data", "campaign_events.json");
            if (File.Exists(p)) return p;
            for (var dir = new DirectoryInfo(baseDir); dir != null; dir = dir.Parent)
            {
                string cand = Path.Combine(dir.FullName, "assets", "data", "campaign_events.json");
                if (File.Exists(cand)) return cand;
            }
            return p;
        }
    }

    public static List<CampaignEventDef> Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return new List<CampaignEventDef>();
        try
        {
            string text = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<List<CampaignEventDef>>(text, opts) ?? new List<CampaignEventDef>();
        }
        catch
        {
            return new List<CampaignEventDef>();
        }
    }

    public static IEnumerable<string> AllText()
    {
        foreach (var ev in Current)
        {
            yield return ev.Title;
            yield return ev.Sender;
            yield return ev.Text;
            foreach (var c in ev.Choices)
            {
                yield return c.Text;
                yield return c.Effect;
            }
        }
    }

    /// <summary>
    /// 벽시계 난수를 쓰지 않고 Seed 와 key, 완료 미션 수 기반으로 0~99 결정적 난수를 반환한다.
    /// </summary>
    public static int RollDeterministic(CampaignState s, string key)
    {
        uint h = 2166136261u ^ s.Seed;
        h = (h ^ (uint)s.Completed.Count) * 16777619u;
        foreach (char c in key)
            h = (h ^ c) * 16777619u;
        h ^= h >> 16;
        h *= 0x85ebca6bu;
        h ^= h >> 13;
        h *= 0xc2b2ae35u;
        h ^= h >> 16;
        return (int)(h % 100);
    }

    /// <summary>
    /// 아직 처리하지 않은(Flags["ev_" + id] == 0) 발생 가능 이벤트 목록.
    /// </summary>
    public static IReadOnlyList<CampaignEventDef> AvailableEvents(CampaignState s) =>
        Current.Where(e => s.Flags.GetValueOrDefault("ev_" + e.Id, 0) == 0).ToList();

    /// <summary>
    /// 이벤트 선택지(0..2)를 적용하고 설명 문자열을 반환한다.
    /// </summary>
    public static string ApplyChoice(CampaignState s, string eventId, int choiceIndex)
    {
        string result = "";
        switch (eventId)
        {
            case "e1_guard_front":
                if (choiceIndex == 0) // (a) 따름: 시위대 의무 투입, 심기 +10, 보급 +200
                {
                    AddFlag(s, "heir_mood", 10);
                    s.Supply += 200f;
                    var guard = s.Roster.FirstOrDefault(c => c.Id == "guard");
                    if (guard == null)
                    {
                        s.Roster.Add(new CampaignState.RosterCompany
                        {
                            Id = "guard",
                            BattalionNumber = 0,
                            BattalionName = "직할",
                            Kind = "guard",
                            Name = "황실 시위대",
                            Strength = 60,
                            MaxStrength = 60,
                            Available = true,
                            Mandatory = true
                        });
                    }
                    else
                    {
                        guard.Available = true;
                        guard.Mandatory = true;
                    }
                    result = "시위대를 전진 배치하기로 했습니다. 황손의 심기가 흡족해하며 보급품 200을 하사했습니다 (심기 +10, 보급 +200, 시위대 의무 투입).";
                }
                else if (choiceIndex == 1) // (b) 설득: 50% 성공 0, 50% 실패 -10
                {
                    int roll = RollDeterministic(s, eventId + "_b");
                    if (roll < 50)
                    {
                        result = "참모장의 조언대로 설득에 성공했습니다. 황손은 마지못해 수긍했습니다 (심기 변동 없음).";
                    }
                    else
                    {
                        AddFlag(s, "heir_mood", -10);
                        result = "설득에 실패했습니다. 황손이 심기를 굳혔습니다 (심기 -10).";
                    }
                }
                else // (c) 무시: 심기 -15
                {
                    AddFlag(s, "heir_mood", -15);
                    result = "황손의 하명을 무시했습니다 (심기 -15).";
                }
                break;

            case "e2_credit_steal":
                if (choiceIndex == 0) // (a) 수용: 신임 +5
                {
                    AddFlag(s, "cmd_favor", 5);
                    result = "총사령관의 공적 독식을 수용했습니다 (신임 +5).";
                }
                else if (choiceIndex == 1) // (b) 항의: 신임 -10, 다음 정기 보급 -10%
                {
                    AddFlag(s, "cmd_favor", -10);
                    s.Flags["next_supply_penalty_pct"] = Math.Min(30, s.Flags.GetValueOrDefault("next_supply_penalty_pct", 0) + 10);
                    result = "총사령관에게 강력히 항의했습니다 (신임 -10, 다음 정기 보급 -10%).";
                }
                else // (c) 남 참장 통보: nam_ally = 1
                {
                    s.Flags["nam_ally"] = 1;
                    result = "11사단장 남 참장에게 사실을 통보했습니다 (남 참장 동맹 플래그 확보).";
                }
                break;

            case "e3_bleed_order":
                if (choiceIndex == 0) // (a) 수락: 보급 +300
                {
                    s.Supply += 300f;
                    result = "출혈 강요 작전을 수락했습니다. 긴급 보급품 300을 수령했습니다 (보급 +300).";
                }
                else if (choiceIndex == 1) // (b) 사정 상신: 신임 -10, 다음 정기 보급 -10%
                {
                    AddFlag(s, "cmd_favor", -10);
                    s.Flags["next_supply_penalty_pct"] = Math.Min(30, s.Flags.GetValueOrDefault("next_supply_penalty_pct", 0) + 10);
                    result = "사정을 상신했습니다 (신임 -10, 다음 정기 보급 -10%).";
                }
                else // (c) 참모장 건의: 50% (a), 50% (b)
                {
                    int roll = RollDeterministic(s, eventId + "_c");
                    if (roll < 50)
                    {
                        s.Supply += 300f;
                        result = "참모장의 절충안이 통하여 보급품 300을 확보했습니다 (보급 +300).";
                    }
                    else
                    {
                        AddFlag(s, "cmd_favor", -10);
                        s.Flags["next_supply_penalty_pct"] = Math.Min(30, s.Flags.GetValueOrDefault("next_supply_penalty_pct", 0) + 10);
                        result = "절충안이 거부되어 신임이 깎이고 보급 패널티를 받았습니다 (신임 -10, 다음 정기 보급 -10%).";
                    }
                }
                break;

            case "e4_heir_lecture":
                if (choiceIndex == 0) // (a) 사과: 심기 +10
                {
                    AddFlag(s, "heir_mood", 10);
                    result = "황손에게 사과하여 체면을 세워주었습니다 (심기 +10).";
                }
                else if (choiceIndex == 1) // (b) 타협: 심기 +5, 보급 -50
                {
                    AddFlag(s, "heir_mood", 5);
                    s.Supply = Math.Max(0f, s.Supply - 50f);
                    result = "시위대 소수 동행으로 타협했습니다 (심기 +5, 보급 -50).";
                }
                else // (c) 무시: 심기 -10
                {
                    AddFlag(s, "heir_mood", -10);
                    result = "황손의 훈수를 무시했습니다 (심기 -10).";
                }
                break;

            case "e5_corps_restraint":
                if (choiceIndex == 0) // (a) 수용: 보급 +200, 심기 -5
                {
                    s.Supply += 200f;
                    AddFlag(s, "heir_mood", -5);
                    result = "군단장의 방어 권고를 수용했습니다 (보급 +200, 황손 심기 -5).";
                }
                else // (b) 공세 유지: 심기 +5
                {
                    AddFlag(s, "heir_mood", 5);
                    result = "원래 계획대로 공세를 유지합니다 (황손 심기 +5).";
                }
                break;

            default:
                result = $"이벤트 [{eventId}] 선택 [{choiceIndex}] 완료";
                break;
        }

        s.Flags["ev_" + eventId] = choiceIndex + 1;
        return result;
    }

    static void AddFlag(CampaignState s, string key, int delta, int min = 0, int max = 100)
    {
        s.Flags[key] = Math.Clamp(s.Flags.GetValueOrDefault(key, 0) + delta, min, max);
    }
}
