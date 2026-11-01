using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft
{
    // A matching filename is required for a MonoBehaviour serialized in a prefab.
    [AddComponentMenu("")]
    public sealed class UiElementPool : MonoBehaviour
    {
        internal readonly Dictionary<string,RectTransform> elements=new Dictionary<string,RectTransform>(StringComparer.Ordinal);
    }
}
