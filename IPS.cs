﻿using System;
using System.IO;
using ProjectFox.CoreEngine.Collections;
using ProjectFox.CoreEngine.Data;

namespace IPSLib;

public sealed class IPS
{
    public static bool TryRead(out IPS ips, string path)
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

    public bool Add(IPS ips, MergeMode mergeMode)
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
                    case MergeMode.Combine:
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

    public bool Add(Patch patch)
    {
        if (patch is StandardPatch sp) return Add(false, sp.address, sp.data);
        if (patch is RLEPatch rp)
        {
            byte[] data = Data.GetBytes((short)(ushort)rp.size, false);
            return Add(true, rp.address, new byte[3] { data[0], data[1], rp.data });
        }
        return false;
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