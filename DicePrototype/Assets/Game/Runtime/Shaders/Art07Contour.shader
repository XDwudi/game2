Shader "DiceCraft/Art07/Contour"
{
    Properties { _BaseColor("Ink",Color)=(.086,.15,.16,1) _Width("Legacy model width",Float)=.018 _PixelWidth("Contour pixels",Range(1,4))=2.4 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 8 ReadMask 8 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float _Width;float _PixelWidth;
            CBUFFER_END
            float4 vert(float4 p:POSITION,float3 n:NORMAL):SV_POSITION
            {
                float4 clip=TransformObjectToHClip(p.xyz);
                float3 worldNormal=TransformObjectToWorldNormal(n);
                float3 viewNormal=mul((float3x3)UNITY_MATRIX_V,worldNormal);
                float2 projected=mul((float2x2)UNITY_MATRIX_P,viewNormal.xy);
                projected=projected/max(length(projected),.0001);
                clip.xy+=projected*(_PixelWidth*2/_ScreenParams.xy)*clip.w;
                return clip;
            }
            half4 frag():SV_Target{return _BaseColor;}
            ENDHLSL
        }
    }
}
