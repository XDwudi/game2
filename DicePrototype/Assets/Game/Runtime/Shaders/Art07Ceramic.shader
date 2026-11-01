Shader "DiceCraft/Art07/Ceramic"
{
    Properties
    {
        _BaseColor("Ceramic",Color)=(.957,.91,.796,1)
        _EdgeColor("Ink edge",Color)=(.086,.15,.16,1)
        _Glass("Glass shell",Range(0,1))=0
        _GlassOpacity("Glass shell opacity",Range(.15,.85))=.48
    }
    SubShader
    {
        // A single front shell: ordinary ceramic writes alpha1, glass exposes the felt
        // already behind it. Pips are separate opaque geometry, not tinted in this pass.
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Ceramic"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            // URP reserves high stencil bits; bit8 belongs to StencilUsage.UserMask.
            // The later contour pass rejects this exact front silhouette, so a glass
            // body does not reveal an opaque inverted-hull interior behind the shell.
            Stencil { Ref 8 ReadMask 8 WriteMask 8 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;};
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;half4 _EdgeColor;half _Glass;half _GlassOpacity;
            CBUFFER_END
            V vert(A a){V v;v.world=TransformObjectToWorld(a.vertex.xyz);v.pos=TransformWorldToHClip(v.world);v.normal=TransformObjectToWorldNormal(a.normal);return v;}
            half4 frag(V v):SV_Target
            {
                half3 n=normalize(v.normal);Light key=GetMainLight();half l=saturate(dot(n,key.direction));
                half shade=l>.65?1:l>.22?.82:.61;
                half3 color=lerp(_EdgeColor.rgb,_BaseColor.rgb,shade);
                half facing=saturate(dot(n,normalize(GetWorldSpaceViewDir(v.world))));
                half rim=pow(1-facing,4);
                half3 glassColor=lerp(_BaseColor.rgb,half3(.52,.79,.79),.18)*lerp(.8,1,l);
                color=lerp(color,glassColor,_Glass);
                color=lerp(color,_EdgeColor.rgb,rim*lerp(.25,.5,_Glass));
                half alpha=lerp(1,saturate(_GlassOpacity+rim*.22),_Glass);
                return half4(color,alpha);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
