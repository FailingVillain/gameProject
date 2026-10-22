using System.Numerics;
using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 통신 알파 뒤 ②~④ (T10, ROADMAP_LONG '전투 시스템 추가' — 사용자 2026-10-21 승인 목록, 세부 규칙은 아직 사용자 미확정):
///  ② 대포병 (counterBattery): 곡사포 · 간접 사격 포가 쏘면, 그 포에서 detectRange 칸 안에 관측 유닛 (장교 · 정찰병, Def.Spotter) 을 둔 적대 세력이
///     한 발마다 detectChance 확률 (해시 난수) 로 포 위치를 탐지 → revealSec 초 동안 그 세력에게 그 포가 보이고 (안개 CanSee) 관측된 것으로 친다 (SeenBySpotter).
///     그래서 플레이어 · AI 의 곡사포가 그 포를 쏠 수 있고, 전술 AI 는 aiPriority 면 돌격 지원 다음 순위로 탐지된 포를 노린다.
///  ③ 조명탄 (flare): 장교가 FireFlareCommand 로 range 칸 안 지점에 조명탄 (장교마다 perOfficer 발). 낮이 아닐 때 radius 칸 원이 sec 초 동안 밝다:
///     그 원에서 seeRange 칸 안에 유닛이 있는 세력은 그 원을 본다 (안개 찍기), 원 안 표적은 명중 배수가 낮 값 (날씨 × 낮).
///  ④ 행군 피로 (marchFatigue): 걷기 이동 중 스태미나가 회복 (+staminaRegenMove) 하는 대신 walkDrain/초 줄어든다 (정지 회복 그대로).
///     지침 효과는 기존 스태미나 규칙 (staminaLow 미만 이동 ×0.85, 돌격 · 속보 보너스 비례 감소).
/// 모두 assets/data/fieldops.json 의 enabled 로 켠다 — 기본 끔 (기존 동작 · 골든 체크섬 불변). 수치는 초안 (사용자 승인 전).
/// 상태 = World.CbMarks · World.Flares · Unit.FlaresUsed (CMB2 끝 WriteFieldOps, 체크섬 HashFieldOps — 기본 상태면 생략).
/// </summary>
public sealed record FireFlareCommand(int Faction, int[] Units, Vector2 Target) : GameCommand(Faction);

/// <summary>대포병 탐지 표시: Faction 이 GunId 포의 위치를 Until 틱까지 안다.</summary>
public sealed class CbMark
{
    public int Faction;
    public int GunId;
    public long Until;
}

/// <summary>떠 있는 조명탄 (원형, 크기 고정). EndTick 이 되면 꺼진다.</summary>
public sealed class Flare
{
    public int Owner;
    public Vector2 Pos;
    public float Radius;
    public long StartTick, EndTick;
}

/// <summary>assets/data/fieldops.json (T10). 기본 = 모두 끔.</summary>
public sealed class FieldOpsDefs
{
    public CounterBatteryDef CounterBattery { get; set; } = new();
    public FlareDef Flare { get; set; } = new();
    public MarchFatigueDef MarchFatigue { get; set; } = new();

    static FieldOpsDefs? current;
    public static FieldOpsDefs Current => current ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "data", "fieldops.json"));
    /// <summary>[테스트] 정의를 갈아 끼운다 (null = 파일 다시 읽기).</summary>
    public static void Override(FieldOpsDefs? d) => current = d;

    public static FieldOpsDefs Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<FieldOpsDefs>(File.ReadAllText(path), DataJson.Opts) is { } r) return r;
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
        return new FieldOpsDefs();
    }
}

public sealed class CounterBatteryDef
{
    public bool Enabled { get; set; }
    /// <summary>포 둘레 이 칸 안에 관측 유닛 (장교 · 정찰병) 이 있는 적대 세력만 탐지 판정.</summary>
    public float DetectRange { get; set; } = 60f;
    /// <summary>한 발마다 탐지 확률.</summary>
    public float DetectChance { get; set; } = 0.35f;
    /// <summary>탐지된 포가 보이는 시간 (초, 다시 쏘면 갱신).</summary>
    public float RevealSec { get; set; } = 20f;
    /// <summary>전술 AI 곡사포가 탐지된 포를 돌격 지원 다음 순위로 노리는가.</summary>
    public bool AiPriority { get; set; } = true;
}

public sealed class FlareDef
{
    public bool Enabled { get; set; }
    /// <summary>쏘는 거리 (칸) · 밝힌 원 반경 (칸) · 지속 (초) · 장교 한 명이 쏠 수 있는 발수.</summary>
    public float Range { get; set; } = 25f;
    public float Radius { get; set; } = 10f;
    public float Sec { get; set; } = 40f;
    public int PerOfficer { get; set; } = 2;
    /// <summary>밝힌 원 중심에서 이 칸 안에 유닛이 있는 세력이 그 원을 본다.</summary>
    public float SeeRange { get; set; } = 40f;
}

public sealed class MarchFatigueDef
{
    public bool Enabled { get; set; }
    /// <summary>걷기 이동 중 스태미나 감소 (초당, 켜면 staminaRegenMove 회복 대신).</summary>
    public float WalkDrain { get; set; } = 1f;
}

public sealed partial class Unit
{
    /// <summary>쏜 조명탄 수 (장교, T10 ③).</summary>
    [SnapExt] internal int FlaresUsed;
    /// <summary>[표시] 남은 조명탄 (장교가 아니거나 조명탄 꺼짐 = 0).</summary>
    public int FlaresLeft => FieldOpsDefs.Current.Flare.Enabled && Def.IsOfficer ? Math.Max(0, FieldOpsDefs.Current.Flare.PerOfficer - FlaresUsed) : 0;
}

public sealed partial class World
{
    /// <summary>대포병 탐지 표시 (생긴 순서). 저장 · 체크섬.</summary>
    [SnapExt] public readonly List<CbMark> CbMarks = new();
    /// <summary>떠 있는 조명탄 (쏜 순서). 저장 · 체크섬.</summary>
    [SnapExt] public readonly List<Flare> Flares = new();

    static bool CbOn => FieldOpsDefs.Current.CounterBattery.Enabled;

    /// <summary>만료된 탐지 표시 · 다 탄 조명탄 지우기 (UpdateBlasts 앞, 매 틱).</summary>
    void UpdateFieldOps()
    {
        if (CbMarks.Count > 0) CbMarks.RemoveAll(m => m.Until < Tick || Find(m.GunId) is null);
        if (Flares.Count > 0) Flares.RemoveAll(f => f.EndTick <= Tick);
    }

    // ---------------------------------------------------------------- ② 대포병

    /// <summary>포가 한 발 쏨 (Fire · FireAtGround): 켜져 있으면 적대 세력마다 탐지 판정.</summary>
    void CbOnFire(Unit gun)
    {
        var cb = FieldOpsDefs.Current.CounterBattery;
        if (!cb.Enabled) return;
        float r2 = cb.DetectRange * cb.DetectRange;
        for (int f = 1; f < Factions.Count; f++)
        {
            if (!IsHostile(f, gun.Owner)) continue;
            bool near = false;
            for (int i = 0; i < spotters.Count; i++)
            {
                var s = spotters[i];
                if (s.Alive && (s.Owner == f || Allied(f, s.Owner)) && Vector2.DistanceSquared(s.Pos, gun.Pos) <= r2) { near = true; break; }
            }
            if (!near || Roll(gun.Id, 0x4CB0 + f) >= cb.DetectChance) continue;
            long until = Tick + SecTicks(cb.RevealSec);
            CbMark? have = null;
            foreach (var m in CbMarks) if (m.Faction == f && m.GunId == gun.Id) { have = m; break; }
            if (have != null) have.Until = Math.Max(have.Until, until);
            else CbMarks.Add(new CbMark { Faction = f, GunId = gun.Id, Until = until });
            Stats.CbDetections++;
        }
    }

    /// <summary>faction (또는 동맹) 이 대포병 탐지로 이 유닛 위치를 아는가. 표시가 없으면 즉시 false.</summary>
    public bool CbMarked(int faction, Unit u)
    {
        if (CbMarks.Count == 0) return false;
        for (int i = 0; i < CbMarks.Count; i++)
        {
            var m = CbMarks[i];
            if (m.GunId == u.Id && m.Until >= Tick && (m.Faction == faction || Allied(faction, m.Faction))) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- ③ 조명탄

    /// <summary>조명탄 (플레이어): 장교만 · 남은 발수 · 사거리 안. 한 명령에 한 발 (목록에서 쏠 수 있는 첫 장교).</summary>
    public void FireFlare(int faction, IEnumerable<Unit> units, Vector2 target) =>
        IssueUnits(faction, units.Where(u => u.Def.IsOfficer), ids => new FireFlareCommand(faction, ids, target));

    void ExecFireFlare(List<Unit> list, Vector2 target)
    {
        var fd = FieldOpsDefs.Current.Flare;
        if (!fd.Enabled || Weather.Time == TimeOfDay.Day || !float.IsFinite(target.X) || !float.IsFinite(target.Y)) return;
        foreach (var u in list)
        {
            if (!u.Alive || u.Surrendered || !u.Def.IsOfficer || u.FlaresUsed >= fd.PerOfficer) continue;
            if (Vector2.Distance(u.Pos, target) > fd.Range + 0.01f) continue;
            u.FlaresUsed++;
            Flares.Add(new Flare { Owner = u.Owner, Pos = target, Radius = fd.Radius, StartTick = Tick, EndTick = Tick + SecTicks(fd.Sec) });
            Stats.FlaresFired++;
            Fog.Update();   // 밝힌 곳이 바로 보이게
            return;
        }
    }

    /// <summary>지점이 조명탄으로 밝은가 (낮이 아닐 때만 의미).</summary>
    public bool FlareLit(Vector2 p)
    {
        if (Flares.Count == 0 || Weather.Time == TimeOfDay.Day) return false;
        for (int i = 0; i < Flares.Count; i++)
        {
            var f = Flares[i];
            if (Vector2.DistanceSquared(f.Pos, p) <= f.Radius * f.Radius) return true;
        }
        return false;
    }

    /// <summary>명중률의 날씨 · 시간대 배수: 조명탄 원 안 표적이면 낮 값.</summary>
    float WeatherHitMulAt(Vector2 target) =>
        Flares.Count > 0 && FlareLit(target) ? Defs.Weather.HitMul(Weather.Kind, TimeOfDay.Day) : WeatherHitMul;

    /// <summary>세력 f 가 조명탄 fl 의 밝힌 원을 보는가 (원 중심 seeRange 칸 안에 f 의 살아 있는 유닛). 안개 갱신이 부른다.</summary>
    internal bool FlareSeenBy(int f, Flare fl)
    {
        float r = FieldOpsDefs.Current.Flare.SeeRange + fl.Radius, r2 = r * r;
        foreach (var u in Units)
            if (u.Alive && u.Owner == f && Vector2.DistanceSquared(u.Pos, fl.Pos) <= r2) return true;
        return false;
    }

    // ---------------------------------------------------------------- 저장 (CMB2 끝, WriteBreach 뒤) · 체크섬

    void WriteFieldOps(SnapWriter w)
    {
        foreach (var u in Units) w.I(u.FlaresUsed);
        w.I(CbMarks.Count);
        foreach (var m in CbMarks) { w.I(m.Faction); w.I(m.GunId); w.L(m.Until); }
        w.I(Flares.Count);
        foreach (var f in Flares) { w.I(f.Owner); w.F(f.Pos.X); w.F(f.Pos.Y); w.F(f.Radius); w.L(f.StartTick); w.L(f.EndTick); }
    }

    void ReadFieldOps(SnapReader r)
    {
        foreach (var u in Units)
        {
            u.FlaresUsed = r.I();
            if (u.FlaresUsed < 0 || u.FlaresUsed > 1000) throw new SnapshotException(string.Format(SnapMsg.Invalid, "flares used"));
        }
        CbMarks.Clear();
        int n = r.Count(16);
        for (int i = 0; i < n; i++)
        {
            var m = new CbMark { Faction = r.I(), GunId = r.I(), Until = r.L() };
            if (m.Faction <= 0 || m.Faction >= Factions.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "counter-battery mark"));
            CbMarks.Add(m);
        }
        Flares.Clear();
        int k = r.Count(32);
        for (int i = 0; i < k; i++)
        {
            var f = new Flare { Owner = r.I(), Pos = new Vector2(r.F(), r.F()), Radius = r.F(), StartTick = r.L(), EndTick = r.L() };
            if (!float.IsFinite(f.Pos.X) || !float.IsFinite(f.Pos.Y) || !(f.Radius > 0) || f.EndTick < f.StartTick) throw new SnapshotException(string.Format(SnapMsg.Invalid, "flare"));
            Flares.Add(f);
        }
    }

    /// <summary>기본 상태 (조명탄 · 탐지 없음) 는 체크섬에 넣지 않는다 — 이 기능을 쓰지 않는 게임의 골든 체크섬 불변.</summary>
    void HashFieldOps(ref Hasher h)
    {
        bool any = CbMarks.Count > 0 || Flares.Count > 0;
        if (!any) foreach (var u in Units) if (u.FlaresUsed != 0) { any = true; break; }
        if (!any) return;
        foreach (var u in Units) h.Add(u.FlaresUsed);
        h.Add(CbMarks.Count);
        foreach (var m in CbMarks) { h.Add(m.Faction); h.Add(m.GunId); h.Add(m.Until); }
        h.Add(Flares.Count);
        foreach (var f in Flares) { h.Add(f.Owner); h.Add(f.Pos.X); h.Add(f.Pos.Y); h.Add(f.Radius); h.Add(f.StartTick); h.Add(f.EndTick); }
    }
}
