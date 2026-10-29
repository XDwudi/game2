Shader "DiceCraft/VertexFace"
{
    Properties { _BaseColor("Tint", Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            V vert(A i){ V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.color=i.color; return o; }
            half4 frag(V i):SV_Target { return half4(i.color.rgb * (i.color.r==i.color.g && i.color.g==i.color.b ? _BaseColor.rgb : half3(1,1,1)),1); }
            ENDHLSL
        }
    }
}
