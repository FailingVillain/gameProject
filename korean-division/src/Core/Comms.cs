using System.Text.Json;

namespace KoreanDivision.Core;

/// <summary>
/// 통신 · 지휘 구조 알파 (사용자 2026-10-21/22 확정, docs/COMMS_ALPHA_PLAN.md) — 조각 ①: 명령 전달 지연 기반.
/// 중대 · 유닛 명령은 지금 그대로 즉시. 대대장 (조각 ③: 연대장) 앞 명령은 DispatchOrderCommand 로 보내고,
/// 유선 (wireMin~Max 초) · 기마 전령 (courierMin~Max 초, 조각 ②에서 실제 말 이동) 으로 도착한 틱에 그 제대의 현재 구성원에게 실행된다.
/// 받는 제대가 사라지면 분실 (Lost) — 전달 · 분실 항목은 keepSec 동안 목록에 남아 UI 가 '전달 중' · 분실 알림을 그린다.
/// 상태 = World.Dispatches · nextDispatchId · Difficulty (ORG_ 끝 WriteComms, 체크섬 HashComms). 리플레이 = 발송 명령만 기록.
/// </summary>
public enum CommsVia : byte { Wire = 0, Courier = 1 }

public enum DispatchState : byte { Transit = 0, Delivered = 1, Lost = 2 }

/// <summary>분실 원인 (알림 문구용).</summary>
public enum DispatchLoss : byte { None = 0, TargetGone = 1, CourierCaptured = 2, WireCut = 3 }

/// <summary>대대장 앞 명령 (전달 지연). Order = 대대에 내릴 명령 (Units 는 도착 때 대대의 현재 구성원으로 바뀐다). Order 안에 다시 Dispatch 는 넣을 수 없다.</summary>
public sealed record DispatchOrderCommand(int Faction, int Battalion, CommsVia Via, GameCommand Order) : GameCommand(Faction);

/// <summary>전달 중 · 끝난 명령 한 건.</summary>
public sealed class Dispatch
{
    public int Id { get; internal set; }
    public int Faction { get; internal set; }
    public int Battalion { get; internal set; }
    public CommsVia Via { get; internal set; }
    public long SentTick { get; internal set; }
    public long ArriveTick { get; internal set; }
    public DispatchState State { get; internal set; }
    public DispatchLoss Loss { get; internal set; }
    /// <summary>전달 · 분실된 틱 (Transit 이면 -1).</summary>
    public long EndTick { get; internal set; } = -1;
    public GameCommand Order { get; internal set; } = null!;
    /// <summary>[표시] 남은 초 (Transit 일 때).</summary>
    public float SecondsLeft(long tick) => Math.Max(0, ArriveTick - tick) * Config.SimDt;
}

public sealed partial class World
{
    /// <summary>난이도 키 (difficulty.json levels 키, 소문자). 월드 준비 단계 (틱 0 전) 에 정한다. 저장 · 체크섬 · 리플레이 머리말.</summary>
    [SnapExt] string difficulty = DifficultyDefs.Current.Default;
    public string Difficulty { get => difficulty; set => difficulty = DifficultyDefs.Current.Normalize(value); }
    public DifficultyLevel DiffLevel => DifficultyDefs.Current.Get(difficulty);

    /// <summary>전달 중 · 최근 끝난 명령 (Id 순). 저장 · 체크섬.</summary>
    [SnapExt] readonly List<Dispatch> dispatches = new();
    [SnapExt] int nextDispatchId = 1;
    public IReadOnlyList<Dispatch> Dispatches => dispatches;

    /// <summary>[공개 API] 대대장에게 명령을 보낸다 (전달 지연). order 의 Units 는 무엇이든 좋다 (도착 때 대대 구성원으로 바뀜).</summary>
    public void DispatchOrder(int faction, Battalion b, CommsVia via, GameCommand order) =>
        Issue(new DispatchOrderCommand(faction, b.Id, via, order));

    void ExecDispatchOrder(int f, DispatchOrderCommand c)
    {
        if (c.Order == null || c.Order is DispatchOrderCommand || c.Order.Faction != f || !Enum.IsDefined(c.Via)
            || FindBattalion(c.Battalion) is not { } b || b.Owner != f) { Notify(f, CommandResult.NotAllowed); return; }
        var lv = DiffLevel.Comms;
        int id = nextDispatchId++;
        float lo = c.Via == CommsVia.Wire ? lv.WireMinSec : lv.CourierMinSec, hi = c.Via == CommsVia.Wire ? lv.WireMaxSec : lv.CourierMaxSec;
        if (hi < lo) hi = lo;
        float sec = Math.Max(0f, lo + (hi - lo) * Roll(id, 0x434F4D53));
        long ticks = Math.Max(1, (long)MathF.Round(sec / Config.SimDt));
        var d = new Dispatch { Id = id, Faction = f, Battalion = b.Id, Via = c.Via, SentTick = Tick, ArriveTick = Tick + ticks, Order = c.Order };
        dispatches.Add(d);
        // T10b: 유선 끊김 (발송 시점) — 규칙 켜짐 + 난이도 wireCut 일 때만
        var rules = DifficultyDefs.Current.CommsRules;
        if (c.Via == CommsVia.Wire && rules.Enabled && lv.WireCut && BattalionUnitIds(b) is { Length: > 0 } ids
            && EnemyNear(f, Centroid(ids), rules.CutRadius) && Roll(id, 0x57435554) < rules.CutChance)
            Lose(d, DispatchLoss.WireCut);
    }

    /// <summary>T10b: 기마 전령 사로잡힘 판정 (규칙 켜짐 + 난이도 courierCapture). 출발점 = 보내는 세력의 첫 살아 있는 사령부 건물 (없으면 판정 안 함).</summary>
    bool CourierCaptured(Dispatch d, int[] ids)
    {
        var rules = DifficultyDefs.Current.CommsRules;
        if (d.Via != CommsVia.Courier || !rules.Enabled || !DiffLevel.Comms.CourierCapture || Tick >= d.ArriveTick) return false;
        long every = Math.Max(1, (long)MathF.Round(rules.CheckSec / Config.SimDt));
        if ((Tick - d.SentTick) % every != 0 || Tick == d.SentTick) return false;
        Building? hq = null;
        foreach (var bd in Buildings) if (bd.Alive && bd.Owner == d.Faction && bd.Def.Headquarters) { hq = bd; break; }
        if (hq == null) return false;
        float t = (float)(Tick - d.SentTick) / Math.Max(1, d.ArriveTick - d.SentTick);
        var at = System.Numerics.Vector2.Lerp(hq.Pos, Centroid(ids), t);
        return EnemyNear(d.Faction, at, rules.CaptureRadius) && Roll(d.Id, 0x43415054) < rules.CaptureChance;
    }

    /// <summary>at 둘레 radius 칸 안에 f 의 적대 유닛이 있는가 (규칙이 켜졌을 때만 · 전령은 checkSec 마다라 임시 목록 할당은 드묾 — World 필드를 늘리지 않음).</summary>
    bool EnemyNear(int f, System.Numerics.Vector2 at, float radius)
    {
        foreach (var u in UnitsInRadius(at, radius)) if (IsHostile(f, u.Owner)) return true;
        return false;
    }

    System.Numerics.Vector2 Centroid(int[] ids)
    {
        System.Numerics.Vector2 s = default; int n = 0;
        foreach (int i in ids) if (Find(i) is { } u) { s += u.Pos; n++; }
        return n == 0 ? s : s / n;
    }

    /// <summary>매 틱 명령 처리 직후: 도착한 명령 실행 · 받는 제대가 사라진 명령 분실 · 오래된 끝난 항목 정리.</summary>
    void StepComms()
    {
        if (dispatches.Count == 0) return;
        long keep = Math.Max(0, (long)MathF.Round(DiffLevel.Comms.KeepSec / Config.SimDt));
        for (int i = 0; i < dispatches.Count; i++)
        {
            var d = dispatches[i];
            if (d.State != DispatchState.Transit) continue;
            var b = FindBattalion(d.Battalion);
            int[] ids = b == null || b.Owner != d.Faction ? Array.Empty<int>() : BattalionUnitIds(b);
            if (ids.Length == 0) { Lose(d, DispatchLoss.TargetGone); continue; }
            if (CourierCaptured(d, ids)) { Lose(d, DispatchLoss.CourierCaptured); continue; }
            if (Tick < d.ArriveTick) continue;
            d.State = DispatchState.Delivered; d.EndTick = Tick;
            Execute(Retarget(d.Order, ids));
        }
        dispatches.RemoveAll(d => d.State != DispatchState.Transit && Tick - d.EndTick >= keep);
    }

    void Lose(Dispatch d, DispatchLoss why) { d.State = DispatchState.Lost; d.Loss = why; d.EndTick = Tick; }

    int[] BattalionUnitIds(Battalion b)
    {
        var l = new List<int>();
        foreach (var c in b.Companies) foreach (var u in c.Members) if (u.Alive && u.Owner == b.Owner) l.Add(u.Id);
        l.Sort();
        return l.ToArray();
    }

    /// <summary>도착한 명령의 대상 = 대대의 현재 구성원.</summary>
    static GameCommand Retarget(GameCommand c, int[] ids) => c switch
    {
        MoveCommand m => m with { Units = ids },
        AttackMoveCommand m => m with { Units = ids },
        QuickStepCommand m => m with { Units = ids },
        RetreatCommand m => m with { Units = ids },
        AttackCommand m => m with { Units = ids },
        StopCommand m => m with { Units = ids },
        HoldCommand m => m with { Units = ids },
        ChargeCommand m => m with { Units = ids },
        OccupyPositionsCommand m => m with { Units = ids },
        _ => c,
    };

    // ---------------------------------------------------------------- 저장 · 체크섬 (ORG_ 끝, WriteRally 뒤 — comms)

    static string OrderJson(GameCommand c) => JsonSerializer.Serialize(c, CommandJson.Options);

    void WriteComms(SnapWriter w)
    {
        w.Str(difficulty);
        w.I(nextDispatchId);
        w.I(dispatches.Count);
        foreach (var d in dispatches)
        {
            w.I(d.Id); w.I(d.Faction); w.I(d.Battalion); w.Byte((byte)d.Via); w.L(d.SentTick); w.L(d.ArriveTick);
            w.Byte((byte)d.State); w.Byte((byte)d.Loss); w.L(d.EndTick); w.Str(OrderJson(d.Order));
        }
    }

    void ReadComms(SnapReader r)
    {
        string? key = r.Str();
        if (!DifficultyDefs.Current.IsKnown(key)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "difficulty"));
        difficulty = key!.ToLowerInvariant();
        nextDispatchId = r.I();
        dispatches.Clear();
        int n = r.Count(30);
        for (int i = 0; i < n; i++)
        {
            var d = new Dispatch { Id = r.I(), Faction = r.I(), Battalion = r.I() };
            byte via = r.Byte();
            if (!Enum.IsDefined(typeof(CommsVia), via)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "dispatch via"));
            d.Via = (CommsVia)via; d.SentTick = r.L(); d.ArriveTick = r.L();
            byte st = r.Byte(), loss = r.Byte();
            if (!Enum.IsDefined(typeof(DispatchState), st) || !Enum.IsDefined(typeof(DispatchLoss), loss)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "dispatch state"));
            d.State = (DispatchState)st; d.Loss = (DispatchLoss)loss; d.EndTick = r.L();
            string? js = r.Str();
            GameCommand? order;
            try { order = js == null ? null : JsonSerializer.Deserialize<GameCommand>(js, CommandJson.Options); }
            catch (JsonException) { order = null; }
            d.Order = order ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "dispatch order"));
            dispatches.Add(d);
        }
    }

    /// <summary>기본 상태 (전달 없음 · 기본 난이도) 는 체크섬에 넣지 않는다 — 통신을 쓰지 않는 게임의 골든 체크섬 불변.</summary>
    void HashComms(ref Hasher h)
    {
        if (dispatches.Count == 0 && nextDispatchId == 1 && difficulty == DifficultyDefs.Current.Default) return;
        h.Add(difficulty); h.Add(nextDispatchId); h.Add(dispatches.Count);
        foreach (var d in dispatches)
        {
            h.Add(d.Id); h.Add(d.Faction); h.Add(d.Battalion); h.Add((int)d.Via); h.Add(d.SentTick); h.Add(d.ArriveTick);
            h.Add((int)d.State); h.Add((int)d.Loss); h.Add(d.EndTick); h.Add(OrderJson(d.Order));
        }
    }
}
