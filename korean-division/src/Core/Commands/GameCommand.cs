using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KoreanDivision.Core;

/// <summary>
/// 게임 명령 (데이터 객체). 사람 · AI 의 모든 조작은 이 객체로 World 대기열에 들어가 다음 틱 시작에 실행된다.
/// 엔티티는 객체 참조가 아니라 Id 로 가리키므로 그대로 저장(리플레이) · 전송(멀티플레이)할 수 있다.
/// 새 명령: 레코드 추가 → 아래 JsonDerivedType 등록 → World.Execute 에 분기 추가.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$t")]
[JsonDerivedType(typeof(MoveCommand), "move")]
[JsonDerivedType(typeof(AttackMoveCommand), "amove")]
[JsonDerivedType(typeof(QuickStepCommand), "quickstep")]
[JsonDerivedType(typeof(AttackCommand), "attack")]
[JsonDerivedType(typeof(StopCommand), "stop")]
[JsonDerivedType(typeof(HoldCommand), "hold")]
[JsonDerivedType(typeof(GroundFireCommand), "groundfire")]
[JsonDerivedType(typeof(DebugSpawnCommand), "debugspawn")]
[JsonDerivedType(typeof(RevealMapCommand), "reveal")]
[JsonDerivedType(typeof(SetCombatSettingCommand), "combatset")]
[JsonDerivedType(typeof(ChargeCommand), "charge")]
[JsonDerivedType(typeof(ThrowGrenadeCommand), "grenade")]
[JsonDerivedType(typeof(ThrowSmokeCommand), "smokegrenade")]   // N65 (Breach.cs, 리플레이 v20)
[JsonDerivedType(typeof(CutWireCommand), "cutwire")]   // N59 (Breach.cs, 리플레이 v20)
[JsonDerivedType(typeof(ChargeOnlyCommand), "chargeonly")]
[JsonDerivedType(typeof(MountCommand), "mount")]
[JsonDerivedType(typeof(FormCompanyCommand), "company")]
[JsonDerivedType(typeof(DisbandCompanyCommand), "disbandco")]
[JsonDerivedType(typeof(SplitCompanyCommand), "splitco")]
[JsonDerivedType(typeof(FormBattalionCommand), "battalion")]
[JsonDerivedType(typeof(SetBattalionNumberCommand), "batnumber")]
[JsonDerivedType(typeof(MoveCompanyCommand), "moveco")]
[JsonDerivedType(typeof(SetCompanyFormCommand), "coform")]
[JsonDerivedType(typeof(SetMarchFireCommand), "marchfire")]
[JsonDerivedType(typeof(SetBattalionFormCommand), "batform")]
[JsonDerivedType(typeof(MissionEndCommand), "missionend")]
[JsonDerivedType(typeof(SetScoutAutoCommand), "scoutauto")]
[JsonDerivedType(typeof(SetCompanyTacticCommand), "cotactic")]
[JsonDerivedType(typeof(SetSquadFormCommand), "sqform")]
[JsonDerivedType(typeof(SetUnitRoleCommand), "role")]
[JsonDerivedType(typeof(CloseRanksCommand), "closeranks")]
[JsonDerivedType(typeof(RetreatCommand), "retreat")]
[JsonDerivedType(typeof(SetRallyCommand), "rally")]
[JsonDerivedType(typeof(GroundFireExCommand), "groundfireex")]
[JsonDerivedType(typeof(OccupyPositionsCommand), "occupy")]
[JsonDerivedType(typeof(SetPostureCommand), "posture")]
[JsonDerivedType(typeof(SetKeepFormationCommand), "keepform")]
[JsonDerivedType(typeof(SetFireModeCommand), "firemode")]
[JsonDerivedType(typeof(SetFinalLineCommand), "finalline")]
[JsonDerivedType(typeof(TrenchAssaultCommand), "trenchassault")]
[JsonDerivedType(typeof(DispatchOrderCommand), "dispatch")]
public abstract record GameCommand(int Faction)
{
    /// <summary>N37 (2026-10-19): 플레이어가 직접 내린 명령의 선택 인원 (0 = AI · 옛 기록 → 지연 배수 1). 명령 반응 · 지휘 계통 지연이 balance.json command.playerSel* 곡선으로 줄어든다.
    /// 명령에 실려 가므로 리플레이가 그대로 재현한다 (리플레이 형식 12).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Sel { get; init; }
}

/// <summary>Form = 진형 지정 (null = 자유). 진형은 명령에 실려 가므로 리플레이가 그대로 재현한다.</summary>
public sealed record MoveCommand(int Faction, int[] Units, Vector2 Target, FormSpec? Form = null) : GameCommand(Faction);
public sealed record AttackMoveCommand(int Faction, int[] Units, Vector2 Target, FormSpec? Form = null) : GameCommand(Faction);
/// <summary>속보 (ROADMAP §4 스태미나 · 속보): 이동 명령인데 스태미나를 써서 걷기보다 빠르게 (보병 · 장교만).</summary>
public sealed record QuickStepCommand(int Faction, int[] Units, Vector2 Target, FormSpec? Form = null, bool Cross = false) : GameCommand(Faction);   // Cross: 전술 AI 도하 강행 (pronemove 엎드림 규칙 면제, Unit.ForceCross)
/// <summary>후퇴 (Pursuit.cs): 속보 가능한 병사는 속보, 그 밖은 보통 이동, 전원 Retreating (도착 · 새 명령까지 — 보병 돌격 표적이 되지 않음).</summary>
public sealed record RetreatCommand(int Faction, int[] Units, Vector2 Target, FormSpec? Form = null, bool UseRally = true) : GameCommand(Faction);   // UseRally: 집결 지점이 있는 중대는 그 지점으로 (Rally.cs, N79a)
public sealed record AttackCommand(int Faction, int[] Units, int TargetId) : GameCommand(Faction);
public sealed record StopCommand(int Faction, int[] Units) : GameCommand(Faction);
public sealed record HoldCommand(int Faction, int[] Units) : GameCommand(Faction);
/// <summary>진지 점유 (engage, 2026-10-19 — 전술 AI 방어 · 후퇴 · 예비): Target 둘레 Radius 안의 참호 자리 → 엄폐 칸 (명중 배수 ≤ CoverMaxHit) 에 병사를 배정해 들어가 사수. Dir = 적 쪽.</summary>
public sealed record OccupyPositionsCommand(int Faction, int[] Units, Vector2 Target, Vector2 Dir, float Radius, float CoverMaxHit) : GameCommand(Faction);
/// <summary>플레이어 지점 포격 (GroundFire.cs): 곡사포 · 방열 직사포가 관측자 없이 땅의 한 지점을 계속 쏜다. AI 는 쓰지 않는다.</summary>
public sealed record GroundFireCommand(int Faction, int[] Units, Vector2 Target) : GameCommand(Faction);
/// <summary>착검 돌격 (ROADMAP §2-4b): TargetId 가 있으면 그 적에게, 없으면 지점으로 돌격 이동 (근접 무기가 있는 유닛만, 돌격 중 사격 없음 · 속도 +30%).</summary>
public sealed record ChargeCommand(int Faction, int[] Units, Vector2 Target, int TargetId) : GameCommand(Faction);
/// <summary>수류탄 투척 (교전 4차): 수류탄이 있고 사거리(grenade.range) 안인 유닛이 지점으로 던진다.</summary>
public sealed record ThrowGrenadeCommand(int Faction, int[] Units, Vector2 Target) : GameCommand(Faction);
/// <summary>기병 '돌격만' 토글 (교전 4차): On 이면 사격하지 않고 돌격만 한다.</summary>
/// <summary>기병 하마/승마 (N61, Dismount.cs): Mount = true 면 타고 자동 하마 잠금, false 면 내려서 쏘고 자동 복귀.</summary>
public sealed record MountCommand(int Faction, int[] Units, bool Mount) : GameCommand(Faction);
public sealed record ChargeOnlyCommand(int Faction, int[] Units, bool On) : GameCommand(Faction);
/// <summary>[디버그] 유닛 생성 (인구 무시). 리플레이 재현을 위해 이것도 명령으로 기록한다.</summary>
public sealed record DebugSpawnCommand(int Faction, string Unit, Vector2 Center, int Count) : GameCommand(Faction);

/// <summary>[치트] 자기 세력이 지도 전체를 본다 (show all maps). On=false 면 해제.</summary>
public sealed record RevealMapCommand(int Faction, bool On) : GameCommand(Faction);

// ---- 외교

/// <summary>실행 기록 한 줄: 몇 번째 틱에 실행됐는가.</summary>
public readonly record struct CommandRecord(long Tick, GameCommand Command);

/// <summary>명령 직렬화 설정 (Vector2 = [x, y]).</summary>
public static class CommandJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new Vector2Converter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    sealed class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Vector2 는 [x, y] 형식");
            reader.Read();
            float x = reader.GetSingle();
            reader.Read();
            float y = reader.GetSingle();
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Vector2 는 [x, y] 형식");
            return new Vector2(x, y);
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }
}
