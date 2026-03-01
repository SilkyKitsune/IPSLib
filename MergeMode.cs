namespace IPSLib;

public enum MergeMode
{
    /// <summary> Adds the patch without performing any merging </summary>
    None,

    /// <summary> Will not add if an overlapping patch is found </summary>
    Ignore,

    /// <summary> Replaces the first overlapping patch found </summary>
    Replace,

    /// <summary> Combines with the first overlapping patch found, prioritizing previous patch data </summary>
    CombineUnder,

    /// <summary> Combines with the first overlapping patch found, prioritizing new patch data </summary>
    CombineOver
}