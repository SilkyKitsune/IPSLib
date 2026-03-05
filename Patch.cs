using System;
using ProjectFox.CoreEngine.Collections;
using ProjectFox.CoreEngine.Data;

namespace IPSLib;

#if DEBUG
/// <summary> Experimental and incomplete, not intended for use </summary>
[Obsolete] public abstract class PatchOld
{
    public PatchOld(int address) => this.address = address;

    public readonly int address;

    public abstract int Size { get; }

    public abstract bool Apply(ref byte[] data);

    public abstract byte[] GetBytes();

    public abstract PatchOld Merge(out PatchOld additionalPatch, PatchOld patch, bool keepNew);//return []?

    public virtual string ToStringFull() => string.Empty;
}

/// <summary> Experimental and incomplete, not intended for use </summary>
[Obsolete] public sealed class StandardPatch : PatchOld
{
    /*public static Patch[] Merge(StandardPatch standardPatch, RLEPatch rlePatch, bool keepNew)
    {
    if (standardPatch == null || standardPatch.data == null || rlePatch == null || standardPatch.address != rlePatch.address) return null;

        if (standardPatch.data.Length == rlePatch.size) return new Patch[1] { rlePatch };

        return standardPatch.data.Length < rlePatch.size ?
            new Patch[2] { standardPatch, new RLEPatch(rlePatch.address, rlePatch.size - standardPatch.data.Length, rlePatch.data) } :
            new Patch[2] { new StandardPatch(standardPatch.address + rlePatch.size, standardPatch.data[rlePatch.size..]), rlePatch };
    }*/

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

    public override byte[] GetBytes() => data;

    public override PatchOld Merge(out PatchOld additionalPatch, PatchOld patch, bool keepNew)
    {
        throw new NotImplementedException();
        //additionalPatch = null;
        //if (patch is StandardPatch standardPatch) return Merge(standardPatch, keepNew);
        //if (patch is RLEPatch rlePatch) return Merge(this, rlePatch, keepNew);
        //return null;
    }

    /*public StandardPatch Merge(out StandardPatch additionalPatch, StandardPatch standardPatch, bool keepNew)
    {
        /*additionalPatch = null;
        
        if (standardPatch == null || standardPatch.data == null) return data == null ? null : this;

        if (data == null) return standardPatch;

        int n = standardPatch.address - address;
        bool neg = n < 0,
            eNeg = standardPatch.address + standardPatch.data.Length < address + data.Length;

        //int n_ = neg ? standardPatch.address - n : address + n,
        //s = neg == eNeg ? (neg ? standardPatch.) : ();

        Rectangle overlap = new Rectangle(address, 0, data.Length, 0).IntersectionBounds(
            new Rectangle(standardPatch.address, 0, standardPatch.data.Length, 0));*


        /*if (address == standardPatch.address)
        {

        }

        int endAddress0 = address + data.Length, endAddress1 = standardPatch.address + standardPatch.data.Length;

        if (endAddress0 == standardPatch.address)
        {
            //?
        }

        if (endAddress1 == address)
        {
            //?
        }

        if (standardPatch.address > address && standardPatch.address < endAddress0)
        {

        }

        if (address > standardPatch.address && address < endAddress1)
        {

        }*

        /*if (address != standardPatch.address)//temp condition
        {
            additionalPatch = standardPatch;
            return this;
        }

        byte[] bytes;
        if (data.Length >= standardPatch.data.Length)
        {
            bytes = new byte[data.Length];
            data.CopyTo(bytes, data.Length);
            standardPatch.data.CopyTo(bytes, standardPatch.data.Length);
        }
        else
        {
            bytes = new byte[standardPatch.data.Length];
            standardPatch.data.CopyTo(bytes, standardPatch.data.Length);
            data.CopyTo(bytes, data.Length);
        }
        return new StandardPatch(address, bytes);*
    }*/

    public override string ToString() =>
        $"{typeof(StandardPatch)}\n" +
        $"  Address: {Data.ToHexString(address, false, true)}\n" +
        $"  Size: {data.Length} ({Data.ToHexString(data.Length, false, true)})";

    public override string ToStringFull() => ToString() + $"\n  Data: {Data.JoinHex(false, false, ", ", data)}";
}

/// <summary> Experimental and incomplete, not intended for use </summary>
[Obsolete] public sealed class RepeatedBytePatch : PatchOld
{
    public RepeatedBytePatch(int address, int size, byte data) : base(address)
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

    public override byte[] GetBytes()
    {
        byte[] data = new byte[size];
        for (int i = 0; i < data.Length; i++) data[i] = this.data;
        return data;
    }

    public override PatchOld Merge(out PatchOld additionalPatch, PatchOld patch, bool keepNew)
    {
        throw new NotImplementedException();
        //additionalPatch = null;
        //if (patch is StandardPatch standardPatch) return StandardPatch.Merge(standardPatch, this, secondPriority);
        //if (patch is RLEPatch rlePatch) return Merge(out additionalPatch, rlePatch, keepNew);
        //return null;
    }

    /*public RLEPatch Merge(out RLEPatch additionalPatch, RLEPatch rlePatch, bool keepNew)
    {
        //additionalPatch = null;

        /*if (rlePatch == null || address != rlePatch.address) return null;

        if (size == rlePatch.size) return rlePatch;

        if (size < rlePatch.size)
        {
            additionalPatch = new(address + size, rlePatch.size - size, rlePatch.data);
            return this;
        }

        additionalPatch = new(rlePatch.address + rlePatch.size, size - rlePatch.size, data);
        return rlePatch;*
    }*/

    public override string ToString() => ToStringFull();

    public override string ToStringFull() =>
        $"{typeof(RepeatedBytePatch)}\n" +
        $"  Address: {Data.ToHexString(address, false, true)}\n" +
        $"  Size: {size} ({Data.ToHexString(size, false, true)})\n" +
        $"  Data: {Data.ToHexString(data)}";
}
#endif

public class Patch : ICopy<Patch>
{
    public Patch(int address, byte[] bytes, int applyCount = 1)
    {
        if (address < 0) throw new ArgumentOutOfRangeException(nameof(address));

        this.bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        this.address = address;
        this.applyCount = applyCount > 1 ? applyCount : 1;
    }

    public readonly int address, applyCount;
    public readonly byte[] bytes;

    public bool Apply(ref byte[] data, bool allowAppend)
    {
        if (data == null) throw new NullReferenceException(nameof(data));

        if (allowAppend)
        {
            int applyLength = address + (bytes.Length * applyCount);
            if (address >= data.Length || allowAppend && applyLength > data.Length)
            {
                byte[] newData = new byte[applyLength];
                data.CopyTo(newData, 0);
                data = newData;
            }
        }
        else if (address >= data.Length) return false;

        for (int i = 0, d = address; i < applyCount; i++)
            for (int s = 0; s < bytes.Length; s++, d++)
            {
                if (d >= data.Length) break;
                data[d] = bytes[s];
            }
        
        return true;
    }

    public void DeepCopy(out Patch copy)
    {
        byte[] bytesCopy = new byte[bytes.Length];
        bytes.CopyTo(bytesCopy, 0);
        copy = new(address, bytesCopy, applyCount);
    }

    public byte[] GetBytes()
    {
        byte[] bytes = new byte[this.bytes.Length * applyCount];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = this.bytes[i % this.bytes.Length];
        return bytes;
    }

    public void ShallowCopy(out Patch copy) => copy = new(address, bytes, applyCount);

    public override string ToString() =>
        $"Address: {Data.ToHexString(address, false, true)}\n" +
        $"Size: {bytes.Length} ({Data.ToHexString(bytes.Length, false, true)})\n" +
        $"Apply Count: {applyCount} ({Data.ToHexString(applyCount, false, true)})";

    public string ToStringFull() => ToString() + $"\nData: {Data.JoinHex(false, false, " ", bytes)}";
}