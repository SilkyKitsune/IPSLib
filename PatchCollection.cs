using System;
using ProjectFox.CoreEngine.Collections;
using M = ProjectFox.CoreEngine.Math.Math;

namespace IPSLib;

#if DEBUG
/// <summary> Experimental and incomplete, not intended for use </summary>
[Obsolete] public abstract class PatchCollectionOld
{
    private readonly AutoSizedArray<PatchOld> patches = new(0x4);

    public int PatchCount => patches.Length;

    public PatchOld this[int index] => patches[index];

    public virtual bool Add(PatchOld patch, MergeMode mergeMode)
    {
        if (patch == null) return false;

        int index = patches.IndexOf(patch);
        if (index >= 0) switch (mergeMode)
            {
                case MergeMode.Ignore:
                    return false;
                case MergeMode.Replace:
                    patches[index] = patch;
                    return true;
                case MergeMode.CombineUnder:
                case MergeMode.CombineOver:
                    /*Patch oldPatch = patches[index];

                    if (oldPatch.Size >= patch.Size) goto case MergeMode.Replace;

                    Patch[] newPatches = oldPatch.Merge(patch);
                    if (newPatches == null) patch.Merge(oldPatch);

                    if (newPatches == null) return false;

                    patches.ReplaceRange(index, 1, newPatches);
                    return true;*/
                    throw new NotImplementedException();
            }
        
        patches.Add(patch);
        return true;
    }

    public virtual bool Apply(ref byte[] data)
    {
        if (data == null || data.Length == 0 || patches.Length == 0) return false;

        bool b = true;
        foreach (PatchOld patch in patches.ToArray()) b = patch.Apply(ref data) && b;
        return b;
    }

    /*public bool Conflicts(Patch patch)//return Patch[]? virtual?
    {
        if (patch == null) return false;

        int endAddress = patch.address + patch.Size;
        if (patch.address == endAddress) return false;

        foreach (Patch patch_ in patches.ToArray())
        {
            if (Math.BetweenAgainstMin(patch.address, patch_.address, patch_.address + patch_.Size) ||
                Math.BetweenAgainstMin(patch_.address, patch.address, endAddress)) return true;
        }
        return false;
    }*/

    public PatchOld[] GetPatches() => patches.ToArray();

    public bool Remove(int address)
    {
        bool b = false;
        foreach (PatchOld patch in patches.ToArray())
            if (patch.address == address)
            {
                patches.Remove(patch);
                b = true;
            }
        return b;
    }

    public bool Remove(PatchOld patch) => patches.Remove(patch);

    public bool RemoveAt(int index) => patches.RemoveAt(index);

    //SortAndTruncate(MergeMode)

    public override string ToString() => patches.Join('\n');

    public string ToString(int index) => index < 0 || index >= patches.Length ? string.Empty : patches[index].ToString();

    public virtual string ToStringFull()
    {
        string s = "";
        foreach (PatchOld patch in patches.ToArray()) s += patch.ToStringFull();
        return s;
    }

    public string ToStringFull(int index) => index < 0 || index >= patches.Length ? string.Empty : patches[index].ToStringFull();

    public abstract void WritePatch(string path, bool overwrite = true);
}
#endif

public abstract class PatchCollection : ICopy<PatchCollection>
{
    private readonly AutoSizedArray<Patch> patches = new(0x4);

    public int PatchCount => patches.Length;

    public Patch this[int index] => patches[index];

    public virtual bool Add(Patch patch, MergeMode mergeMode)
    {
        if (patch == null) return false;

        if (patches.Length > 0 || mergeMode != MergeMode.None)
        {
            int i = 0;
            foreach (Patch oldPatch in patches.ToArray())
            {
                if (patch == oldPatch) return false;

                Patch first = patch, second = oldPatch;
                bool oldFirst = patch.address > oldPatch.address;
                if (oldFirst)
                {
                    first = oldPatch;
                    second = patch;
                }

                if (second.address <= first.address + (first.bytes.Length * first.applyCount))
                {
                    switch (mergeMode)
                    {
                        case MergeMode.Ignore:
                            return false;

                        case MergeMode.Replace:
                            patches[i] = patch;
                            return true;

                        case MergeMode.CombineUnder:
                            {
                                byte[] firstBytes = first.GetBytes(), secondBytes = second.GetBytes(),
                                    newBytes = new byte[M.Max(first.address + firstBytes.Length, second.address + secondBytes.Length) - first.address];

                                if (oldFirst)
                                {
                                    secondBytes.CopyTo(newBytes, second.address - first.address);
                                    firstBytes.CopyTo(newBytes, 0);
                                }
                                else
                                {
                                    firstBytes.CopyTo(newBytes, 0);
                                    secondBytes.CopyTo(newBytes, second.address - first.address);
                                }

                                patches[i] = new(first.address, newBytes);
                                //patches.ReplaceRange(i, 1, new Patch(first.address, newBytes).Truncate());
                                return true;
                            }
                            
                        case MergeMode.CombineOver:
                            {
                                byte[] firstBytes = first.GetBytes(), secondBytes = second.GetBytes(),
                                    newBytes = new byte[M.Max(first.address + firstBytes.Length, second.address + secondBytes.Length) - first.address];

                                if (oldFirst)
                                {
                                    firstBytes.CopyTo(newBytes, 0);
                                    secondBytes.CopyTo(newBytes, second.address - first.address);
                                }
                                else
                                {
                                    secondBytes.CopyTo(newBytes, second.address - first.address);
                                    firstBytes.CopyTo(newBytes, 0);
                                }

                                patches[i] = new(first.address, newBytes);
                                //patches.ReplaceRange(i, 1, new Patch(first.address, newBytes).Truncate());
                                return true;
                            }
                    }
                }
                i++;
            }
        }

        patches.Add(patch);
        return true;
    }

    public void Add(MergeMode mergeMode, params Patch[] patches)
    {
        if (patches == null || patches.Length == 0) return;
        foreach (Patch patch in patches) Add(patch, mergeMode);
    }

    public void Add(PatchCollection patchCollection, MergeMode mergeMode)
    {
        if (patchCollection == null || patchCollection.patches.Length == 0) return;
        foreach (Patch patch in patchCollection.patches.ToArray()) Add(patch, mergeMode);
    }

    public virtual bool Apply(ref byte[] data, bool allowAppend)
    {
        if (data == null || data.Length == 0 || patches.Length == 0) return false;

        bool b = true;
        foreach (Patch patch in patches.ToArray()) b = patch.Apply(ref data, allowAppend) && b;
        return b;
    }

    public abstract void DeepCopy(out PatchCollection copy);

    public Patch[] GetPatches() => patches.ToArray();

    public bool Remove(int address)
    {
        bool b = false;
        foreach (Patch patch in patches.ToArray())
            if (patch.address == address)
            {
                patches.Remove(patch);
                b = true;
            }
        return b;
    }

    public bool Remove(Patch patch) => patches.Remove(patch);

    public bool RemoveAt(int index) => patches.RemoveAt(index);

    public abstract void ShallowCopy(out PatchCollection copy);

    //SortAndTruncate(MergeMode)

    public override string ToString() => patches.Join("\n-\n");

    public string ToString(int index) => index < 0 || index >= patches.Length ? string.Empty : patches[index].ToString();

    public virtual string ToStringFull()
    {
        string s = "";
        foreach (Patch patch in patches.ToArray()) s += patch.ToStringFull() + "\n-\n";
        return s;
    }

    public string ToStringFull(int index) => index < 0 || index >= patches.Length ? string.Empty : patches[index].ToStringFull();

    public abstract void WritePatch(string path, bool overwrite = true);
}