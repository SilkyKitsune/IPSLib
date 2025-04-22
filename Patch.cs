using System;
using ProjectFox.CoreEngine.Data;

namespace IPSLib;

public abstract class Patch
{
    public Patch(int address) => this.address = address;

    public readonly int address;

    public abstract int Size { get; }

    public abstract bool Apply(ref byte[] data);

    public virtual string ToStringFull() => string.Empty;
}

public sealed class StandardPatch : Patch
{
    public StandardPatch(int address, byte[] data) : base(address) => this.data = data;

    public readonly byte[] data;

    public override int Size => data.Length;

    public override bool Apply(ref byte[] data)
    {
        if (data == null || this.data == null) return false;

        int newLength = address + this.data.Length;
        if (address >= data.Length || newLength > data.Length)
        {
            byte[] newData = new byte[newLength];
            Array.Copy(data, newData, data.Length);
            data = newData;
        }

        Array.Copy(this.data, 0, data, address, this.data.Length);
        return true;
    }

    public override string ToString() =>
        $"{typeof(StandardPatch)}\n" +
        $"  Address: {Data.ToHexString(address, false, true)}\n" +
        $"  Size: {data.Length}";

    public override string ToStringFull() => ToString() + $"\n  Data: {Data.JoinHex(false, false, ", ", data)}";
}

public sealed class RLEPatch : Patch
{
    public RLEPatch(int address, int size, byte data) : base(address)
    {
        this.size = size;
        this.data = data;
    }

    public readonly int size;

    public readonly byte data;

    public override int Size => size;

    public override bool Apply(ref byte[] data)
    {
        if (data == null) return false;

        int newLength = address + size;
        if (address >= data.Length || newLength > data.Length)
        {
            byte[] newData = new byte[newLength];
            Array.Copy(data, newData, data.Length);
            data = newData;
        }

        for (int i = 0; i < size; i++) data[address + i] = this.data;
        return true;
    }

    public override string ToString() => ToStringFull();

    public override string ToStringFull() =>
        $"{typeof(RLEPatch)}\n" +
        $"  Address: {Data.ToHexString(address, false, true)}\n" +
        $"  Size: {size}\n" +
        $"  Data: {Data.ToHexString(data)}";
}