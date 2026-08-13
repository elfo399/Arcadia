using System.Collections.Generic;

public enum MagicFailureReason
{
    None,
    InvalidRecipe,
    LockedRecipe,
    AlreadyLearned,
    MissingStats,
    MissingCoins,
    MissingMaterials,
    NotLearned
}

public sealed class MagicRequirementStatus
{
    public ItemData Item;
    public int Required;
    public int Owned;
    public bool Satisfied;
}

public sealed class MagicStatRequirementStatus
{
    public MagicStatAttribute Attribute;
    public int Required;
    public int Owned;
    public bool Satisfied;
}

public sealed class MagicLearnCheck
{
    public bool IsValid;
    public MagicFailureReason FailureReason;
    public int CoinCost;
    public readonly List<MagicRequirementStatus> Materials = new List<MagicRequirementStatus>();
    public readonly List<MagicStatRequirementStatus> Stats = new List<MagicStatRequirementStatus>();
}
