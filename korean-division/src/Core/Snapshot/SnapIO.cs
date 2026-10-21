using System.Numerics;
using System.Text;

namespace KoreanDivision.Core;

/// <summary>저장 파일을 읽다가 형식이 맞지 않을 때 (깨짐 · 잘림 · 알 수 없는 값).</summary>
public sealed class SnapshotException : Exception
{
    public SnapshotException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>스냅샷 본문 쓰기: 7비트 가변 정수 + 부동소수는 비트 그대로 (결정적).</summary>
public sealed class SnapWriter
{
    public readonly MemoryStream Ms;
    readonly BinaryWriter w;

    public SnapWriter(int capacity = 1 << 16)
    {
        Ms = new MemoryStream(capacity);
        w = new BinaryWriter(Ms, Encoding.UTF8, leaveOpen: true);
    }

    public long Length => Ms.Length;
    public void Tag(string t) { w.Write((byte)t[0]); w.Write((byte)t[1]); w.Write((byte)t[2]); w.Write((byte)t[3]); }
    public void Bool(bool v) => w.Write(v ? (byte)1 : (byte)0);
    public void Byte(byte v) => w.Write(v);
    public void I(int v) => w.Write7BitEncodedInt(v);
    public void L(long v) => w.Write7BitEncodedInt64(v);
    public void F(float v) => w.Write(BitConverter.SingleToUInt32Bits(v));
    public void D(double v) => w.Write(BitConverter.DoubleToUInt64Bits(v));
    public void U64(ulong v) => w.Write(v);
    public void Vec(Vector2 v) { F(v.X); F(v.Y); }
    public void Str(string? s)
    {
        if (s == null) { I(-1); return; }
        var b = Encoding.UTF8.GetBytes(s);
        I(b.Length);
        w.Write(b);
    }
    public void Bytes(ReadOnlySpan<byte> b) { I(b.Length); w.Write(b); }
    public void Raw(ReadOnlySpan<byte> b) => w.Write(b);
    public void Flush() => w.Flush();

    public byte[] ToArray() { w.Flush(); return Ms.ToArray(); }
    public ReadOnlyMemory<byte> Buffer { get { w.Flush(); return new ReadOnlyMemory<byte>(Ms.GetBuffer(), 0, (int)Ms.Length); } }
}

public sealed class SnapReader
{
    readonly byte[] data;
    int pos;
    readonly int end;

    public SnapReader(byte[] data, int offset = 0, int count = -1)
    {
        this.data = data;
        pos = offset;
        end = count < 0 ? data.Length : offset + count;
    }

    public int Position => pos;
    public bool AtEnd => pos >= end;
    public int Remaining => end - pos;

    byte Next()
    {
        if (pos >= end) throw new SnapshotException(SnapMsg.Truncated);
        return data[pos++];
    }

    public void Tag(string t)
    {
        for (int i = 0; i < 4; i++)
            if (Next() != (byte)t[i]) throw new SnapshotException(string.Format(SnapMsg.Invalid, "section " + t));
    }

    public bool Bool() => Next() != 0;
    public byte Byte() => Next();

    public int I()
    {
        uint r = 0;
        int shift = 0;
        while (true)
        {
            byte b = Next();
            r |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 35) throw new SnapshotException(string.Format(SnapMsg.Invalid, "varint"));
        }
        return (int)r;
    }

    public long L()
    {
        ulong r = 0;
        int shift = 0;
        while (true)
        {
            byte b = Next();
            r |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 70) throw new SnapshotException(string.Format(SnapMsg.Invalid, "varint"));
        }
        return (long)r;
    }

    public float F()
    {
        if (pos + 4 > end) throw new SnapshotException(SnapMsg.Truncated);
        float v = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(data, pos));
        pos += 4;
        return v;
    }

    public double D()
    {
        if (pos + 8 > end) throw new SnapshotException(SnapMsg.Truncated);
        double v = BitConverter.UInt64BitsToDouble(BitConverter.ToUInt64(data, pos));
        pos += 8;
        return v;
    }

    public ulong U64()
    {
        if (pos + 8 > end) throw new SnapshotException(SnapMsg.Truncated);
        ulong v = BitConverter.ToUInt64(data, pos);
        pos += 8;
        return v;
    }

    public Vector2 Vec() => new(F(), F());

    /// <summary>개수 읽기 (악의적 · 깨진 값으로 거대한 할당을 하지 않게 남은 바이트로 상한).</summary>
    public int Count(int bytesPerItem = 1)
    {
        int n = I();
        if (n < 0 || (long)n * Math.Max(1, bytesPerItem) > end - pos + 16) throw new SnapshotException(string.Format(SnapMsg.Invalid, "count"));
        return n;
    }

    public string? Str()
    {
        int n = I();
        if (n < 0) return null;
        if (pos + n > end) throw new SnapshotException(SnapMsg.Truncated);
        string s = Encoding.UTF8.GetString(data, pos, n);
        pos += n;
        return s;
    }

    public byte[] Bytes()
    {
        int n = Count();
        var b = new byte[n];
        Buffer.BlockCopy(data, pos, b, 0, n);
        pos += n;
        return b;
    }

    public void Raw(Span<byte> dst)
    {
        if (pos + dst.Length > end) throw new SnapshotException(SnapMsg.Truncated);
        data.AsSpan(pos, dst.Length).CopyTo(dst);
        pos += dst.Length;
    }
}
