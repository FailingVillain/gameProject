using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace KoreanDivision.Core;

/// <summary>
/// 리플렉션 기반 범용 직렬화 — AI 컨트롤러의 내부 상태 · 대기 중인 명령(레코드) 같은 '데이터 덩어리'용.
/// 새 AI 필드를 추가해도 따로 저장 코드를 쓸 필요가 없다 (모든 인스턴스 필드를 저장).
/// 지원: 기본형 · enum · string · Vector2 · Nullable · 배열 · List · Dictionary · HashSet · 구조체(튜플 포함) · 클래스(참조 공유 보존) ·
/// Entity (Id 참조) · UnitDef/BuildingDef/WeaponDef/ArmorDef (키). 딕셔너리 · 집합은 키 순으로 저장해 같은 상태 = 같은 바이트.
/// 저장하지 않는 필드: 매 틱 다시 채우는 작업용 목록 (List&lt;Unit&gt; · List&lt;Building&gt;) · AiDefs 캐시 · 델리게이트 · [NonSerialized] 필드.
/// </summary>
internal sealed class ObjCodec
{
    readonly SnapCtx ctx;
    readonly Dictionary<object, int> seenW = new(ReferenceEqualityComparer.Instance);
    readonly List<object> seenR = new();

    public ObjCodec(SnapCtx ctx) => this.ctx = ctx;

    static readonly Dictionary<Type, FieldInfo[]> fieldCache = new();
    static readonly Dictionary<string, Type> typeCache = new();

    static bool Skipped(FieldInfo f)
    {
        var t = f.FieldType;
        if (f.IsNotSerialized) return true;   // [NonSerialized] 작업 버퍼 (전술 AI 의 층별 목록 등)
        if (typeof(Delegate).IsAssignableFrom(t)) return true;
        if (t == typeof(AiDefs)) return true;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            var a = t.GetGenericArguments()[0];
            if (a == typeof(Unit) || a == typeof(Building)) return true;
        }
        return false;
    }

    static FieldInfo[] FieldsOf(Type t)
    {
        lock (fieldCache)
        {
            if (fieldCache.TryGetValue(t, out var f)) return f;
            var chain = new List<Type>();
            for (var c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType) chain.Add(c);
            chain.Reverse();
            var list = new List<FieldInfo>();
            foreach (var c in chain)
                list.AddRange(c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(x => !Skipped(x)).OrderBy(x => x.MetadataToken));
            return fieldCache[t] = list.ToArray();
        }
    }

    static Type ResolveType(string name)
    {
        lock (typeCache)
        {
            if (typeCache.TryGetValue(name, out var t)) return t;
            t = typeof(World).Assembly.GetType(name) ?? throw new SnapshotException(string.Format(SnapMsg.Invalid, "unknown type " + name));
            return typeCache[name] = t;
        }
    }

    static bool IsGeneric(Type t, Type def) => t.IsGenericType && t.GetGenericTypeDefinition() == def;

    // ---------------------------------------------------------------- 쓰기

    public void WriteFields(SnapWriter w, object obj)
    {
        foreach (var f in FieldsOf(obj.GetType())) Write(w, f.GetValue(obj), f.FieldType);
    }

    public void Write(SnapWriter w, object? v, Type t)
    {
        if (t == typeof(string)) { w.Str((string?)v); return; }
        if (t.IsEnum) { w.L(Convert.ToInt64(v)); return; }
        if (t.IsPrimitive)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Boolean: w.Bool((bool)v!); return;
                case TypeCode.Byte: w.Byte((byte)v!); return;
                case TypeCode.SByte: w.I((sbyte)v!); return;
                case TypeCode.Int16: w.I((short)v!); return;
                case TypeCode.UInt16: w.I((ushort)v!); return;
                case TypeCode.Int32: w.I((int)v!); return;
                case TypeCode.UInt32: w.L((uint)v!); return;
                case TypeCode.Int64: w.L((long)v!); return;
                case TypeCode.UInt64: w.U64((ulong)v!); return;
                case TypeCode.Single: w.F((float)v!); return;
                case TypeCode.Double: w.D((double)v!); return;
                case TypeCode.Char: w.I((char)v!); return;
            }
            throw new SnapshotException(string.Format(SnapMsg.UnsupportedType, t));
        }
        if (t == typeof(Vector2)) { w.Vec((Vector2)v!); return; }
        if (Nullable.GetUnderlyingType(t) is Type inner)
        {
            w.Bool(v != null);
            if (v != null) Write(w, v, inner);
            return;
        }
        if (typeof(Entity).IsAssignableFrom(t)) { ctx.WriteEntity(w, (Entity?)v); return; }
        if (t == typeof(UnitDef)) { w.Str(((UnitDef?)v)?.Key); return; }
        if (t == typeof(BuildingDef)) { w.Str(((BuildingDef?)v)?.Key); return; }
        if (t == typeof(WeaponDef)) { w.Str(((WeaponDef?)v)?.Key); return; }
        if (t == typeof(ArmorDef)) { w.Str(((ArmorDef?)v)?.Key); return; }
        if (t.IsArray)
        {
            var arr = (Array?)v;
            if (arr == null) { w.I(-1); return; }
            w.I(arr.Length);
            var et = t.GetElementType()!;
            foreach (var e in arr) Write(w, e, et);
            return;
        }
        if (IsGeneric(t, typeof(List<>)))
        {
            var l = (IList?)v;
            if (l == null) { w.I(-1); return; }
            w.I(l.Count);
            var et = t.GetGenericArguments()[0];
            foreach (var e in l) Write(w, e, et);
            return;
        }
        if (IsGeneric(t, typeof(Dictionary<,>)))
        {
            var d = (IDictionary?)v;
            if (d == null) { w.I(-1); return; }
            var ga = t.GetGenericArguments();
            var entries = new List<(object Key, object? Val)>(d.Count);
            foreach (DictionaryEntry de in d) entries.Add((de.Key, de.Value));
            Sort(entries, e => e.Key);
            w.I(entries.Count);
            foreach (var (k, val) in entries) { Write(w, k, ga[0]); Write(w, val, ga[1]); }
            return;
        }
        if (IsGeneric(t, typeof(HashSet<>)))
        {
            if (v == null) { w.I(-1); return; }
            var items = new List<object?>();
            foreach (var e in (IEnumerable)v) items.Add(e);
            Sort(items, e => e!);
            w.I(items.Count);
            var et = t.GetGenericArguments()[0];
            foreach (var e in items) Write(w, e, et);
            return;
        }
        if (t.IsValueType)
        {
            foreach (var f in FieldsOf(t)) Write(w, f.GetValue(v), f.FieldType);
            return;
        }
        if (t.IsClass)
        {
            if (v == null) { w.Byte(0); return; }
            if (seenW.TryGetValue(v, out int idx)) { w.Byte(2); w.I(idx); return; }
            seenW[v] = seenW.Count;
            w.Byte(1);
            var rt = v.GetType();
            if (!t.IsSealed) w.Str(rt.FullName);
            foreach (var f in FieldsOf(rt)) Write(w, f.GetValue(v), f.FieldType);
            return;
        }
        throw new SnapshotException(string.Format(SnapMsg.UnsupportedType, t));
    }

    /// <summary>키 정렬: 숫자 · 문자열 · enum 은 값 순, 정의 객체는 키 문자열 순. 그 외는 열거 순서 유지.</summary>
    static void Sort<T>(List<T> list, Func<T, object?> key)
    {
        if (list.Count < 2) return;
        object? sample = key(list[0]);
        Comparison<T>? cmp = sample switch
        {
            int => (a, b) => ((int)key(a)!).CompareTo((int)key(b)!),
            long => (a, b) => ((long)key(a)!).CompareTo((long)key(b)!),
            string => (a, b) => string.CompareOrdinal((string)key(a)!, (string)key(b)!),
            Enum => (a, b) => Convert.ToInt64(key(a)).CompareTo(Convert.ToInt64(key(b))),
            UnitDef => (a, b) => string.CompareOrdinal(((UnitDef)key(a)!).Key, ((UnitDef)key(b)!).Key),
            BuildingDef => (a, b) => string.CompareOrdinal(((BuildingDef)key(a)!).Key, ((BuildingDef)key(b)!).Key),
            _ => null,
        };
        if (cmp != null) list.Sort(cmp);
    }

    // ---------------------------------------------------------------- 읽기

    /// <summary>이미 만들어진 객체(생성자가 돌아간 컨트롤러 등)의 필드를 채운다.</summary>
    public void ReadFieldsInto(SnapReader r, object obj)
    {
        seenR.Add(obj);
        foreach (var f in FieldsOf(obj.GetType())) f.SetValue(obj, Read(r, f.FieldType));
    }

    public object? Read(SnapReader r, Type t)
    {
        if (t == typeof(string)) return r.Str();
        if (t.IsEnum) return Enum.ToObject(t, r.L());
        if (t.IsPrimitive)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Boolean: return r.Bool();
                case TypeCode.Byte: return r.Byte();
                case TypeCode.SByte: return (sbyte)r.I();
                case TypeCode.Int16: return (short)r.I();
                case TypeCode.UInt16: return (ushort)r.I();
                case TypeCode.Int32: return r.I();
                case TypeCode.UInt32: return (uint)r.L();
                case TypeCode.Int64: return r.L();
                case TypeCode.UInt64: return r.U64();
                case TypeCode.Single: return r.F();
                case TypeCode.Double: return r.D();
                case TypeCode.Char: return (char)r.I();
            }
            throw new SnapshotException(string.Format(SnapMsg.UnsupportedType, t));
        }
        if (t == typeof(Vector2)) return r.Vec();
        if (Nullable.GetUnderlyingType(t) is Type inner) return r.Bool() ? Read(r, inner) : null;
        if (typeof(Entity).IsAssignableFrom(t))
        {
            var e = ctx.ReadEntity(r);
            if (e != null && !t.IsInstanceOfType(e)) throw new SnapshotException(string.Format(SnapMsg.Invalid, "entity kind"));
            return e;
        }
        if (t == typeof(UnitDef)) return Lookup(r.Str(), ctx.Defs.Units, SnapMsg.DefUnit);
        if (t == typeof(BuildingDef)) return Lookup(r.Str(), ctx.Defs.Buildings, SnapMsg.DefBuilding);
        if (t == typeof(WeaponDef)) return Lookup(r.Str(), ctx.Defs.Weapons, SnapMsg.DefWeapon);
        if (t == typeof(ArmorDef)) return Lookup(r.Str(), ctx.Defs.Armors, SnapMsg.DefArmor);
        if (t.IsArray)
        {
            int n = r.I();
            if (n < 0) return null;
            if (n > r.Remaining) throw new SnapshotException(string.Format(SnapMsg.Invalid, "array length"));   // 원소마다 최소 1바이트 — 깨진 길이로 거대한 배열을 만들지 않게
            var et = t.GetElementType()!;
            var arr = Array.CreateInstance(et, n);
            for (int i = 0; i < n; i++) arr.SetValue(Read(r, et), i);
            return arr;
        }
        if (IsGeneric(t, typeof(List<>)))
        {
            int n = r.I();
            if (n < 0) return null;
            var l = (IList)Activator.CreateInstance(t)!;
            var et = t.GetGenericArguments()[0];
            for (int i = 0; i < n; i++) l.Add(Read(r, et));
            return l;
        }
        if (IsGeneric(t, typeof(Dictionary<,>)))
        {
            int n = r.I();
            if (n < 0) return null;
            var d = (IDictionary)Activator.CreateInstance(t)!;
            var ga = t.GetGenericArguments();
            for (int i = 0; i < n; i++) { var k = Read(r, ga[0]); d[k!] = Read(r, ga[1]); }
            return d;
        }
        if (IsGeneric(t, typeof(HashSet<>)))
        {
            int n = r.I();
            if (n < 0) return null;
            var h = Activator.CreateInstance(t)!;
            var add = t.GetMethod("Add")!;
            var et = t.GetGenericArguments()[0];
            for (int i = 0; i < n; i++) add.Invoke(h, new[] { Read(r, et) });
            return h;
        }
        if (t.IsValueType)
        {
            object boxed = Activator.CreateInstance(t)!;
            foreach (var f in FieldsOf(t)) f.SetValue(boxed, Read(r, f.FieldType));
            return boxed;
        }
        if (t.IsClass)
        {
            byte tag = r.Byte();
            if (tag == 0) return null;
            if (tag == 2)
            {
                int idx = r.I();
                if ((uint)idx >= (uint)seenR.Count) throw new SnapshotException(string.Format(SnapMsg.Invalid, "object ref"));
                return seenR[idx];
            }
            if (tag != 1) throw new SnapshotException(string.Format(SnapMsg.Invalid, "object tag"));
            var rt = t.IsSealed ? t : ResolveType(r.Str() ?? "");
            if (!t.IsAssignableFrom(rt) || rt.IsAbstract) throw new SnapshotException(string.Format(SnapMsg.Invalid, "object type"));   // 선언된 형식의 하위 형식만
            object o = rt.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null
                ? Activator.CreateInstance(rt, nonPublic: true)!
                : RuntimeHelpers.GetUninitializedObject(rt);
            seenR.Add(o);
            foreach (var f in FieldsOf(rt)) f.SetValue(o, Read(r, f.FieldType));
            return o;
        }
        throw new SnapshotException(string.Format(SnapMsg.UnsupportedType, t));
    }

    static T Lookup<T>(string? key, Dictionary<string, T> dict, string what) where T : class
    {
        if (string.IsNullOrEmpty(key)) return null!;
        return dict.TryGetValue(key, out var v) ? v : throw new SnapshotException(string.Format(SnapMsg.MissingDef, what, key));
    }
}
