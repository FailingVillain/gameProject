namespace KoreanDivision.Core;

/// <summary>
/// 세력별 유닛 목록 (AI 가 전체 유닛을 훑지 않도록). 저장 대상 아님 — 파생 캐시.
/// 각 목록은 World.Units 의 순서(생성 순서)를 그대로 따른다:
///  · 생성: 해당 세력 목록 끝에 추가 (World.Units 도 끝에 추가)
///  · 죽음: 해당 세력을 표시해 두었다가 다음 질의 때 죽은 것만 걸러냄 (순서 유지)
///  · 소유권 이전 · 불러오기: 관련 세력을 World.Units 훑기로 다시 만듦 (드문 일)
/// 질의 결과 = World.Units 를 Owner 로 거른 것과 동일 (살아 있는 것 + 아직 정리 전인 죽은 것은 호출자가 Alive 로 거른다).
/// </summary>
public sealed partial class World
{
    sealed class FactionIndex
    {
        public readonly List<Unit> All = new(), Armored = new();
        public bool DeadPending, Rebuild = true;
    }

    FactionIndex[] fIndex = new FactionIndex[0];
    bool indexAllRebuild = true;

    FactionIndex IdxOf(int faction)
    {
        if (faction >= fIndex.Length)
        {
            int old = fIndex.Length;
            Array.Resize(ref fIndex, Math.Max(faction + 1, Math.Max(Factions.Count, 4)));
            for (int i = old; i < fIndex.Length; i++) fIndex[i] = new FactionIndex();
        }
        return fIndex[faction];
    }

    void IndexSpawn(Unit u)
    {
        var ix = IdxOf(u.Owner);
        if (ix.Rebuild) return;   // 어차피 다시 만든다
        ix.All.Add(u);
        if (u.Def.IsArmored) ix.Armored.Add(u);
    }

    /// <summary>유닛이 정리(RemoveDead)되었다 — 해당 세력 목록에서 죽은 것을 나중에 걸러낸다.</summary>
    void IndexDead(int owner) => IdxOf(owner).DeadPending = true;

    /// <summary>소유권 이전 · 불러오기 등: 해당 세력(들) 목록을 다음 질의에서 다시 만든다. -1 = 전부.</summary>
    void IndexInvalidate(int a, int b = -1)
    {
        if (a < 0) { indexAllRebuild = true; return; }
        IdxOf(a).Rebuild = true;
        if (b >= 0) IdxOf(b).Rebuild = true;
    }

    void IndexRebuildAll()
    {
        indexAllRebuild = false;
        for (int i = 0; i < Factions.Count; i++) IdxOf(i).Rebuild = true;
        foreach (var ix in fIndex) { ix.All.Clear(); ix.Armored.Clear(); ix.DeadPending = false; ix.Rebuild = false; }
        foreach (var u in Units)
        {
            var ix = IdxOf(u.Owner);
            ix.All.Add(u);
            if (u.Def.IsArmored) ix.Armored.Add(u);
        }
    }

    FactionIndex Fresh(int faction)
    {
        if (indexAllRebuild) IndexRebuildAll();
        var ix = IdxOf(faction);
        if (ix.Rebuild)
        {
            ix.All.Clear(); ix.Armored.Clear();
            foreach (var u in Units)
                if (u.Owner == faction) { ix.All.Add(u); if (u.Def.IsArmored) ix.Armored.Add(u); }
            ix.Rebuild = false; ix.DeadPending = false;
        }
        else if (ix.DeadPending)
        {
            ix.All.RemoveAll(static u => !u.Alive);
            ix.Armored.RemoveAll(static u => !u.Alive);
            ix.DeadPending = false;
        }
        return ix;
    }

    /// <summary>세력의 유닛 (World.Units 순서, 읽기 전용으로 쓸 것 · 순회 중 명령 발행은 안전 — 명령은 다음 틱에 실행).</summary>
    public List<Unit> UnitsOf(int faction) => Fresh(faction).All;

    /// <summary>세력의 기갑 유닛 (World.Units 순서).</summary>
    public List<Unit> ArmoredOf(int faction) => Fresh(faction).Armored;
}
