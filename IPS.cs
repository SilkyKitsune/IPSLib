using System;
using System.IO;
using ProjectFox.CoreEngine.Collections;

namespace IPSLib;

public sealed class IPS
{
    private static byte[] GetBytesBig(int i) => new byte[3] { (byte)(i >> 16 & byte.MaxValue), (byte)(i >> 8 & byte.MaxValue), (byte)(i & byte.MaxValue), };

    private static byte[] GetBytesBig(ushort s) => new byte[2] { (byte)(s >> 8 & byte.MaxValue), (byte)(s & byte.MaxValue), };

    private static int ToInt16Big(byte byte0, byte byte1) => (byte0 << 8) | byte1;

    private static int ToInt24Big(byte byte0, byte byte1, byte byte2) => (byte0 << 16) | (byte1 << 8) | byte2;

    public IPS() { }

    public IPS(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new Exception();

        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 8) throw new Exception();

        if (data[0] != 0x50 || data[1] != 0x41 || data[2] != 0x54 || data[3] != 0x43 || data[4] != 0x48) ///PATCH
            throw new Exception();

        for (int i = 5; i < data.Length &&
            data[i] != 0x45 && data[i + 1] != 0x4F && data[i + 2] != 0x46;) ///EOF
        {
            int address = ToInt24Big(data[i], data[++i], data[++i]), size = ToInt16Big(data[++i], data[++i]);
            bool rle = size == 0;
            Add(rle, address, rle ?
                new byte[3] { data[++i], data[++i], data[i] } :
                data[++i..(i += size)]);
        }
    }

    private readonly HashLookupTable<int, byte[]> tables = new(0x4);

    public bool Add(bool rle, int address, byte[] data)
    {
        if (address > 0xFFFFFF || data == null || data.Length == 0 || data.Length > ushort.MaxValue) return false;
        if (rle)
        {
            if (data.Length != 3) return false;
            tables.Add(address == 0 ? int.MinValue : -address, data);
        }
        else tables.Add(address, data);
        return true;
    }

    public bool Add(IPS ips)
    {
        if (ips == null || ips.tables.Length == 0) return false;

        int[] addresses = tables.GetCodes();
        byte[][] values = tables.GetValues();

        for (int i = 0; i < addresses.Length; i++)
            if (!tables.ContainsCode(addresses[i]))
                tables.Add(addresses[i], values[i]);
        return true;
    }

    public bool Apply(byte[] data)
    {
        if (data == null || data.Length == 0 || tables.Length == 0) return false;

        int[] addresses = tables.GetCodes();
        byte[][] values = tables.GetValues();

        for (int i = 0; i < addresses.Length; i++)
        {
            int address = addresses[i];
            if (address > -1)
            {
                if (address >= data.Length) return false;
                Array.Copy(values[i], 0, data, address, values[i].Length);
            }
            else
            {
                address = address == int.MinValue ? 0 : -address;
                if (address >= data.Length) return false;
                byte b = data[2];
                for (int l = ToInt16Big(data[0], data[1]); address < l; address++) data[address] = b;
            }
        }
        return true;
    }

    public bool Remove(int address) => tables.Remove(address) || (address == 0 && tables.Remove(int.MinValue)) || tables.Remove(-address);

    public bool RemoveAt(int index) => tables.RemoveAt(index);

    public override string ToString()
    {
        string s = "PATCH\n-----\n";
        for (int i = 0, l = tables.Length; i < l; i++) s += ToString(i);
        return s;
    }

    public string ToString(int index)
    {
        if (index < 0 || index >= tables.Length) return string.Empty;

        int address = tables.GetCodes()[index];
        byte[] data = tables.GetValues()[index];

        return address > -1 ?
            $"Address: {Strings.ToHexString(address, false, true)}\n" +
            $"  Size: {data.Length}\n" +
            $"  Data: {Strings.JoinHex(false, false, ", ", data)}\n"
            :
            $"Address: {Strings.ToHexString(address == int.MinValue ? 0 : -address, false, true)}\n" +
            $"  RLE_Size: {ToInt16Big(data[0], data[1])}\n" +
            $"  Data: {Strings.ToHexString(data[2])}\n";
    }

    public void WritePatch(string path, bool overwrite = true)
    {
        path = Path.ChangeExtension(path, "ips");

        if (string.IsNullOrEmpty(path) || (!overwrite && File.Exists(path))) throw new Exception();

        AutoSizedArray<byte> data = new(new byte[]
        {
            0x50, 0x41, 0x54, 0x43, 0x48, ///PATCH
        }, 0x100);

        int[] addresses = tables.GetCodes();
        byte[][] values = tables.GetValues();

        for (int i = 0; i < addresses.Length; i++)
        {
            int address = addresses[i];
            if (address > -1)
            {
                data.Add(GetBytesBig(address));
                data.Add(GetBytesBig((ushort)values[i].Length));
                data.Add(values[i]);
            }
            else
            {
                data.Add(GetBytesBig(address == int.MinValue ? 0 : -address));
                data.Add(0x00, 0x00);
                data.Add(values[i]);
            }
        }
        data.Add(0x45, 0x4F, 0x46); ///EOF
        File.WriteAllBytes(path, data.ToArray());
    }
}