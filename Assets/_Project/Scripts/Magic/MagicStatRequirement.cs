using System;
using UnityEngine;

[Serializable]
public class MagicStatRequirement
{
    public MagicStatAttribute attribute;
    [Min(1)] public int requiredValue = 1;
}
