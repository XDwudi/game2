using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class DiceTable
    {
        private MaterialPropertyBlock bodyShadowProperties071;
        private int shadowCoverageId071;
        private void ApplyTemporaryShadow071(Visual visual, DieInstance die)
        {
            if (bodyShadowProperties071 == null)
            {
                bodyShadowProperties071 = new MaterialPropertyBlock();
                shadowCoverageId071 = Shader.PropertyToID("_ShadowCoverage071");
            }
            visual.body.GetPropertyBlock(bodyShadowProperties071);
            bodyShadowProperties071.SetFloat(shadowCoverageId071, die.lifetime == "stage_temporary" ? .5f : 1f);
            visual.body.SetPropertyBlock(bodyShadowProperties071);
        }
    }
}
