Shader "DiceCraft/Art07/FaceTransition"
{
    Properties
    {
        _BaseColor("Actual face color",Color)=(1,1,1,1)
        _Progress("Committed transition progress",Range(0,1))=0
        _OldLayer("Previous permanent face",Float)=0
        _Wipe("Left to right color wipe",Float)=0
        _ShrinkPips("Shrink previous pips",Float)=0
        _ExclusivePips("Never superimpose changed pip patterns",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+2" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION;float4 color:COLOR;float faceX:TEXCOORD0;float pipRegion:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float _Progress,_OldLayer,_Wipe,_ShrinkPips,_ExclusivePips;
            CBUFFER_END
            V vert(A i)
            {
                V o;o.faceX=i.positionOS.x;
                // MakeFace keeps every pip fill AND outline inside .293, while
                // gold and texture geometry starts outside .31. Classify before
                // contraction so permanent ornament never disappears with pips.
                o.pipRegion=max(abs(i.positionOS.x),abs(i.positionOS.y))<.31?1:0;
                // Only central pips contract. Gold edges and texture marks retain their physical border.
                if(_OldLayer>.5&&_ShrinkPips>.5&&max(abs(i.positionOS.x),abs(i.positionOS.y))<.31)i.positionOS.xy*=1-.1*_Progress;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;return o;
            }
            half4 frag(V i):SV_Target
            {
                float nextAlpha=_Wipe>.5?saturate((_Progress-(i.faceX+.5))/.035+.5):_Progress;
                if(_Progress<=0)nextAlpha=0;if(_Progress>=1)nextAlpha=1;
                float alpha=_OldLayer>.5?1-nextAlpha:nextAlpha;
                // Changed point counts are sequential, not two transparent dice
                // patterns at once. The old 3 fades out before the new 4 starts;
                // no instant can be read as their union (the nonexistent 5).
                // Color-only wipes and outer gold/texture marks retain their
                // existing transition, including simultaneous dye/copy effects.
                if(_ExclusivePips>.5&&i.pipRegion>.5)
                    alpha=_OldLayer>.5?saturate(1-_Progress/.5):saturate((_Progress-.5)/.5);
                half3 rgb=i.color.rgb*(i.color.r==i.color.g&&i.color.g==i.color.b?_BaseColor.rgb:half3(1,1,1));
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
}
