Shader "DiceCraft/Art073/FaceStyle"
{
    // v0.73 per-face finish (glaze, grain, gold rim) drawn over the ceramic or glass body.
    // Vertex colour carries tint and opacity; uv.x is the highlight weight (0 matte .. 1 metal).
    // Lit by the same key light and the same diffuse curve as DiceCraft/Art07/Ceramic, so a finish
    // never looks pasted on. Pips are separate opaque geometry in front of this layer.
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float4 color:COLOR; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; float weight:TEXCOORD2; };
            V vert(A i)
            {
                V o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
                // Every face quad looks down its own +Z: one flat normal for the whole finish.
                o.normal=TransformObjectToWorldNormal(float3(0,0,1)); o.color=i.color; o.weight=i.uv.x; return o;
            }
            half4 frag(V i):SV_Target
            {
                half3 n=normalize(i.normal); Light key=GetMainLight(); half l=saturate(dot(n,key.direction));
                half3 view=normalize(GetWorldSpaceViewDir(i.world));
                half3 color=i.color.rgb*(.59+.41*l);
                half shine=pow(saturate(dot(n,normalize(key.direction+view))),28)*i.weight;
                color+=shine*half3(1,.95,.8)*key.color;
                return half4(color,i.color.a);
            }
            ENDHLSL
        }
    }
}
