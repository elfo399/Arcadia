using System;
using UnityEngine;

[Serializable]
public class MagicMaterialRequirement
{
    public ItemData item;
    [Min(1)] public int amount = 1;
}
