using System;
using System.IO;
using ProjectFox.CoreEngine.Collections;
using ProjectFox.CoreEngine.Data;
using M = ProjectFox.CoreEngine.Math.Math;

namespace IPSLib;

/// <summary> First version of the IPS class, maintained for parity </summary>
[Obsolete] public sealed class IPSOld
{
    public static bool TryRead(out IPSOld ips, string path)
    {
        ips = null;

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 8) return false;
        //this had || and the EOF had && ???
        if (data[0] != 0x50 || data[1] != 0x41 || data[2] != 0x54 || data[3] != 0x43 || data[4] != 0x48) ///PATCH
            return false;
        
        ips = new();
        for (int i = 5; i < data.Length &&
            (data[i] != 0x45 || data[i + 1] != 0x4F || data[i + 2] != 0x46);) ///EOF
        {
            int address = Data.ToInt32(new byte[] { 0, data[i++], data[i++], data[i++] }, false),
                size = (ushort)Data.ToInt16(new byte[] { data[i++], data[i++] }, false);

            if (size == 0) ips.Add(true, address, new byte[3] { data[i++], data[i++], data[i++] });//change contructor too
            else ips.Add(false, address, data[i..(i += size)]);
            //Console.WriteLine($"e {i}<{data.Length}={i < data.Length} && data[{i}]!={0x45}={data[i]} && data[{i+1}]!={0x4F}={data[i + 1]} && data[{i+2}]!={0x46}={data[i + 2]}");
        }
        return true;
    }
    
    private readonly LookupTable<int, byte[]> table = new(0x4);

    public int PatchCount => table.Length;

    public bool Add(bool rle, int address, byte[] data)
    {
        if (address > 0xFFFFFF || data == null || data.Length == 0 || data.Length > ushort.MaxValue) return false;
        if (rle)
        {
            if (data.Length != 3) return false;
            table.Add(address == 0 ? int.MinValue : -address, data);
        }
        else table.Add(address, data);
        return true;
    }

    public bool Add(IPSOld ips, MergeMode mergeMode)
    {
        if (ips == null || ips.table.Length == 0) return false;

        int[] addresses = ips.table.GetCodes();
        byte[][] values = ips.table.GetValues();

        for (int i = 0; i < addresses.Length; i++)
        {
            int address = addresses[i];
            byte[] value = values[i];

            if (table.ContainsCode(address))
            {
                switch (mergeMode)
                {
                    case MergeMode.Replace:
                        table[address] = value;
                        break;
                    case MergeMode.CombineUnder:
                    case MergeMode.CombineOver:
                        byte[] oldValue = table[address];
                        if (value.Length >= oldValue.Length) goto case MergeMode.Replace;
                        value.CopyTo(oldValue, 0);
                        break;
                }
            }
            else table.Add(address, value);
        }
        return true;
    }

    public bool Add(Patch patch, MergeMode mergeMode)
    {
        if (patch == null || patch.bytes.Length == 0 ||
            patch.address > 0xFF_FF_FF || patch.bytes.Length > ushort.MaxValue || patch.applyCount > ushort.MaxValue ||
            (patch.bytes.Length > 1 && patch.applyCount > 1)) return false;

        if (table.Length > 0 || mergeMode != MergeMode.None)
        {
            int[] addresses = table.GetCodes();
            byte[][] values = table.GetValues();
            for (int i = 0; i < addresses.Length; i++)
            {
                int address = addresses[i], applyCount = 1;
                byte[] value = values[i];

                if (address < 0)
                {
                    address = address == int.MinValue ? 0 : -address;
                    applyCount = (ushort)Data.ToInt16(new byte[2] { value[0], value[1] }, false);

                    byte b = value[2];
                    value = new byte[applyCount];
                    for (int j = 0; j < applyCount; j++) value[j] = b;
                }

                int firstAddress = patch.address, secondAddress = address, firstApplyCount = patch.applyCount;
                byte[] firstBytes = patch.GetBytes(), secondBytes = value;
                bool oldFirst = patch.address > address;
                if (oldFirst)
                {
                    secondAddress = patch.address;
                    secondBytes = firstBytes;

                    firstAddress = address;
                    firstApplyCount = applyCount;
                    firstBytes = value;
                }

                if (secondAddress <= firstAddress + (firstBytes.Length * firstApplyCount))
                {
                    switch (mergeMode)
                    {
                        case MergeMode.Ignore:
                            return false;

                        case MergeMode.Replace:
                            table.RemoveAt(i);
                            break;

                        case MergeMode.CombineUnder:
                            {
                                byte[] newBytes = new byte[M.Max(firstAddress + firstBytes.Length, secondAddress + secondBytes.Length) - firstAddress];

                                if (oldFirst)
                                {
                                    secondBytes.CopyTo(newBytes, secondAddress - firstAddress);
                                    firstBytes.CopyTo(newBytes, 0);
                                }
                                else
                                {
                                    firstBytes.CopyTo(newBytes, 0);
                                    secondBytes.CopyTo(newBytes, secondAddress - firstAddress);
                                }

                                patch = new(firstAddress, newBytes);
                                goto case MergeMode.Replace;
                            }

                        case MergeMode.CombineOver:
                            {
                                byte[] newBytes = new byte[M.Max(firstAddress + firstBytes.Length, secondAddress + secondBytes.Length) - firstAddress];

                                if (oldFirst)
                                {
                                    firstBytes.CopyTo(newBytes, 0);
                                    secondBytes.CopyTo(newBytes, secondAddress - firstAddress);
                                }
                                else
                                {
                                    secondBytes.CopyTo(newBytes, secondAddress - firstAddress);
                                    firstBytes.CopyTo(newBytes, 0);
                                }

                                patch = new(firstAddress, newBytes);
                                goto case MergeMode.Replace;
                            }
                    }
                }
            }
        }

        if (patch.applyCount == 1) Add(false, patch.address, patch.bytes);
        else
        {
            byte[] applyCountBytes = Data.GetBytes((short)(ushort)patch.applyCount, false);
            Add(true, patch.address, new byte[3] { applyCountBytes[0], applyCountBytes[1], patch.bytes[0] });
        }
        return true;
    }

    public bool Apply(byte[] data)
    {
        if (data == null || data.Length == 0 || table.Length == 0) return false;

        int[] addresses = table.GetCodes();
        byte[][] values = table.GetValues();

        for (int i = 0; i < addresses.Length; i++)
        {
            int address = addresses[i];
            byte[] newData = values[i];
            if (address > -1)
            {
                if (address >= data.Length) return false;
                Array.Copy(newData, 0, data, address, newData.Length);
            }
            else
            {
                address = address == int.MinValue ? 0 : -address;
                if (address >= data.Length) return false;
                byte b = newData[2];
                for (int j = 0, l = (ushort)Data.ToInt16(new byte[] { newData[0], newData[1] }, false); j < l; j++) data[address + j] = b;
            }
        }
        return true;
    }

    public Patch[] GetPatches()
    {
        if (table.Length == 0) return null;

        int[] addresses = table.GetCodes();
        byte[][] values = table.GetValues();
        Patch[] patches = new Patch[addresses.Length];

        for (int i = 0; i < addresses.Length; i++)
        {
            int address = addresses[i];
            byte[] value = values[i];

            patches[i] = address < 0 ?
                new(-address, new byte[1] { value[2] }, (ushort)Data.ToInt16(new byte[2] { value[0], value[1] }, false)) :
                new(address, value);
        }
        return patches;
    }

    public bool Remove(int address) => table.Remove(address) || (address == 0 && table.Remove(int.MinValue)) || table.Remove(-address);

    public bool RemoveAt(int index) => table.RemoveAt(index);

    public override string ToString()
    {
        string s = "PATCH\n-----\n";
        for (int i = 0, l = table.Length; i < l; i++) s += ToString(i);
        return s;
    }

    public string ToStringFull()
    {
        string s = "PATCH\n-----\n";
        for (int i = 0, l = table.Length; i < l; i++) s += ToStringFull(i);
        return s;
    }

    public string ToString(int index)
    {
        if (index < 0 || index >= table.Length) return string.Empty;

        int address = table.GetCodes()[index];
        byte[] data = table.GetValues()[index];

        return address > -1 ?
            $"Address: {Data.ToHexString(address, false, true)}\n" +
            $"  Size: {data.Length}\n"
            :
            $"Address: {Data.ToHexString(address == int.MinValue ? 0 : -address, false, true)}\n" +
            $"  RLE_Size: {(ushort)Data.ToInt16(new byte[] { data[0], data[1] }, false)}\n" +
            $"  Data: {Data.ToHexString(data[2])}\n";
    }

    public string ToStringFull(int index)
    {
        if (index < 0 || index >= table.Length) return string.Empty;

        int address = table.GetCodes()[index];
        byte[] data = table.GetValues()[index];

        return address > -1 ?
            $"Address: {Data.ToHexString(address, false, true)}\n" +
            $"  Size: {data.Length}\n" +
            $"  Data: {Data.JoinHex(false, false, ", ", data)}\n"
            :
            $"Address: {Data.ToHexString(address == int.MinValue ? 0 : -address, false, true)}\n" +
            $"  RLE_Size: {(ushort)Data.ToInt16(new byte[] { data[0], data[1] }, false)}\n" +
            $"  Data: {Data.ToHexString(data[2])}\n";
    }

    public void WritePatch(string path, bool overwrite = true)
    {
        path = Path.ChangeExtension(path, "ips");

        if (string.IsNullOrEmpty(path) || (!overwrite && File.Exists(path))) throw new Exception();

        AutoSizedArray<byte> data = new(new byte[]
        {
            0x50, 0x41, 0x54, 0x43, 0x48, ///PATCH
        }, 0x100);

        int[] addresses = table.GetCodes();
        byte[][] values = table.GetValues();

        for (int i = 0; i < addresses.Length; i++)
        {
            int address = addresses[i];
            if (address > -1)
            {
                data.Add(Data.GetBytes(address, false)[1..4]);
                data.Add(Data.GetBytes((short)(ushort)values[i].Length, false));//is this double cast necessary?
            }
            else
            {
                data.Add(address == int.MinValue ? new byte[3] : Data.GetBytes(-address, false)[1..4]);
                data.Add(0x00, 0x00);
            }
            data.Add(values[i]);
        }
        data.Add(0x45, 0x4F, 0x46); ///EOF
        File.WriteAllBytes(path, data.ToArray());
    }
}

#if DEBUG
/// <summary> Experimental and incomplete, not intended for use </summary>
[Obsolete] public sealed class IPSFirstRevision : PatchCollectionOld
{
    public static bool TryRead(out IPSFirstRevision ips, string path)
    {
        ips = null;

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 8) return false;

        if (data[0] != 0x50 || data[1] != 0x41 || data[2] != 0x54 || data[3] != 0x43 || data[4] != 0x48) ///PATCH
            return false;

        ips = new();
        for (int i = 5; i < data.Length &&
            (data[i] != 0x45 || data[i + 1] != 0x4F || data[i + 2] != 0x46);) ///EOF
        {
            int address = Data.ToInt32(new byte[4] { 0, data[i++], data[i++], data[i++] }, false),
                size = Data.ToInt32(new byte[4] { 0, 0, data[i++], data[i++] }, false);

            if (size == 0) ips.Add(new RepeatedBytePatch(address, Data.ToInt32(new byte[4] { 0, 0, data[i++], data[i++] }, false), data[i++]), MergeMode.Ignore);
            else ips.Add(new StandardPatch(address, data[i..(i += size)]), MergeMode.Ignore);
        }
        return true;
    }

    public override bool Add(PatchOld patch, MergeMode mergeMode)
    {
        if (patch == null || patch.address > 0xFF_FF_FF || patch.Size > ushort.MaxValue || (patch is not StandardPatch && patch is not RepeatedBytePatch)) return false;
        //check combine mode to make sure new length doesn't exceed FFFF?
        return base.Add(patch, mergeMode);
    }

    public override string ToString() => "IPS\n---\n" + base.ToString();

    public override string ToStringFull() => "IPS\n---\n" + base.ToStringFull();

    public override void WritePatch(string path, bool overwrite = true)
    {
        path = Path.ChangeExtension(path, "ips");

        if (string.IsNullOrEmpty(path) || (!overwrite && File.Exists(path))) throw new Exception();

        AutoSizedArray<byte> data = new(new byte[5]
        {
            0x50, 0x41, 0x54, 0x43, 0x48, ///PATCH
        }, 0x100);

        foreach (PatchOld patch in GetPatches())
        {
            data.Add(Data.GetBytes(patch.address, false)[1..4]);
            if (patch is StandardPatch standardPatch)
            {
                data.Add(Data.GetBytes((short)(ushort)standardPatch.data.Length, false));//is this double cast necessary?
                data.Add(standardPatch.data);
            }
            else if (patch is RepeatedBytePatch rlePatch)
            {
                data.Add(0x00, 0x00);
                data.Add(Data.GetBytes((short)(ushort)rlePatch.size, false));//is this double cast necessary?
                data.Add(rlePatch.data);
            }
        }
        data.Add(0x45, 0x4F, 0x46); ///EOF
        File.WriteAllBytes(path, data.ToArray());
    }
}
#endif

public sealed class IPS : PatchCollection
{
    public static bool TryRead(out IPS ips, string path)
    {
        ips = null;

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 8) return false;

        if (data[0] != 0x50 || data[1] != 0x41 || data[2] != 0x54 || data[3] != 0x43 || data[4] != 0x48) ///PATCH
            return false;

        ips = new();
        for (int i = 5; i < data.Length;)
        {
            int address = Data.ToInt32(new byte[4] { 0, data[i++], data[i++], data[i++] }, false);
            if (address == 0x00454F46) break;

            int size = Data.ToInt32(new byte[4] { 0, 0, data[i++], data[i++] }, false);
            if (size == 0)
            {
                int applyCount = Data.ToInt32(new byte[4] { 0, 0, data[i++], data[i++] }, false);
                ips.Add(new(address, new byte[1] { data[i++] }, applyCount), MergeMode.None);
            }
            else ips.Add(new(address, data[i..(i += size)]), MergeMode.None);
        }
        return true;
    }

    public override bool Add(Patch patch, MergeMode mergeMode)
    {
        if (patch == null ||
            patch.address > 0xFF_FF_FF || patch.bytes.Length > ushort.MaxValue || patch.applyCount > ushort.MaxValue ||
            (patch.bytes.Length > 1 && patch.applyCount > 1)) return false;
        return base.Add(patch, mergeMode);
    }

    public override void DeepCopy(out PatchCollection copy)
    {
        copy = new IPS();
        foreach (Patch patch in GetPatches())
        {
            patch.DeepCopy(out Patch patchCopy);
            copy.Add(patchCopy, MergeMode.None);
        }
    }

    public override void ShallowCopy(out PatchCollection copy)
    {
        copy = new IPS();
        foreach (Patch patch in GetPatches()) copy.Add(patch, MergeMode.None);
    }

    public override string ToString() => "IPS\n---\n" + base.ToString();

    public override string ToStringFull() => "IPS\n---\n" + base.ToStringFull();

    public override void WritePatch(string path, bool overwrite = true)
    {
        path = Path.ChangeExtension(path, "ips");

        if (string.IsNullOrEmpty(path) || (!overwrite && File.Exists(path))) throw new Exception();

        AutoSizedArray<byte> data = new(new byte[5]
        {
            0x50, 0x41, 0x54, 0x43, 0x48 ///PATCH
        }, 0x100);

        foreach (Patch patch in GetPatches())
        {
            if (patch.bytes.Length == 0) continue;

            byte[] addressBytes = Data.GetBytes(patch.address, false);
            data.Add(addressBytes[1], addressBytes[2], addressBytes[3]);

            if (patch.applyCount == 1)
            {
                data.Add(Data.GetBytes((short)(ushort)patch.bytes.Length, false));
                data.Add(patch.bytes);
            }
            else
            {
                data.Add(0, 0);
                data.Add(Data.GetBytes((short)(ushort)patch.applyCount, false));
                data.Add(patch.bytes[0]);
            }
        }

        data.Add(0x45, 0x4F, 0x46); ///EOF
        File.WriteAllBytes(path, data.ToArray());
    }
}