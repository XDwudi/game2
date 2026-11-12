Shader "DiceCraft/Art07/Ceramic"
{
    Properties
    {
        _BaseColor("Ceramic",Color)=(.957,.91,.796,1)
        _EdgeColor("Ink edge",Color)=(.086,.15,.16,1)
        _Smoothness("Ceramic smoothness",Range(0,1))=.62
        _Specular("Ceramic highlight",Range(0,1))=.3
        _Glass("Glass shell",Range(0,1))=0
        _GlassOpacity("Glass shell opacity",Range(.15,.85))=.48
        _ShadowCoverage071("Shadow coverage",Range(0,1))=1
        _Glass073("v0.73 clear glass",Range(0,1))=0
    }
    SubShader
    {
        // A single front shell: ordinary ceramic writes alpha1, glass exposes the felt
        // already behind it. Pips are separate opaque geometry, not tinted in this pass.
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            // v0.73: the far inner walls of a clear glass die, drawn before its front shell.
            // Ceramic and legacy glass discard every fragment here.
            Name "GlassInterior073"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;};
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;half4 _EdgeColor;half _Glass;half _GlassOpacity;half _Smoothness;half _Specular;half _ShadowCoverage071;half _Glass073;
            CBUFFER_END
            V vert(A a){V v;v.world=TransformObjectToWorld(a.vertex.xyz);v.pos=TransformWorldToHClip(v.world);v.normal=TransformObjectToWorldNormal(a.normal);return v;}
            half4 frag(V v):SV_Target
            {
                clip(_Glass*_Glass073-.5);
                // Inner walls face away from the viewer: shade them by how squarely they do.
                half3 n=normalize(v.normal);half3 view=normalize(GetWorldSpaceViewDir(v.world));
                half away=saturate(-dot(n,view));
                half3 color=_BaseColor.rgb*lerp(.42,.7,away);
                return half4(color,lerp(.5,.28,away));
            }
            ENDHLSL
        }
        Pass
        {
            Name "Ceramic"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            // Colour blends as before; the separate alpha term keeps coverage correct when the
            // die is drawn into a transparent texture for the interface (v0.73).
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
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
            half4 _BaseColor;half4 _EdgeColor;half _Glass;half _GlassOpacity;half _Smoothness;half _Specular;half _ShadowCoverage071;half _Glass073;
            CBUFFER_END
            V vert(A a){V v;v.world=TransformObjectToWorld(a.vertex.xyz);v.pos=TransformWorldToHClip(v.world);v.normal=TransformObjectToWorldNormal(a.normal);return v;}
            half4 frag(V v):SV_Target
            {
                half3 n=normalize(v.normal);Light key=GetMainLight();half l=saturate(dot(n,key.direction));
                if(_Glass*_Glass073>.5)
                {
                    // v0.73 clear glass: saturated body colour, glossy key highlight, bright rim.
                    half3 eye=normalize(GetWorldSpaceViewDir(v.world));
                    half gloss=pow(saturate(dot(n,normalize(key.direction+eye))),90);
                    half fresnel=pow(1-saturate(dot(n,eye)),2.2);
                    half3 glass=_BaseColor.rgb*lerp(.74,1.06,l)+gloss*.9*key.color;
                    glass=lerp(glass,half3(.88,.97,1),fresnel*.72);
                    return half4(glass,saturate(_GlassOpacity+fresnel*.38+gloss*.4));
                }
                // Broad ceramic shading and a soft highlight share the scene key light.
                // Rule tint belongs solely to the separate opaque face meshes.
                half3 view=normalize(GetWorldSpaceViewDir(v.world));
                half3 halfDirection=normalize(key.direction+view);
                half diffuse=.59+.41*l;
                half3 color=_BaseColor.rgb*diffuse;
                color=lerp(color,half3(.15,.3,.31),.045*(1-l));
                half highlight=pow(saturate(dot(n,halfDirection)),lerp(14,80,_Smoothness))*_Specular;
                color+=highlight*half3(1,.96,.85)*key.color;
                half facing=saturate(dot(n,view));
                half rim=pow(1-facing,4);
                half3 glassColor=lerp(_BaseColor.rgb,half3(.52,.79,.79),.18)*lerp(.8,1,l);
                color=lerp(color,glassColor,_Glass);
                color=lerp(color,_EdgeColor.rgb,rim*lerp(.08,.5,_Glass));
                half alpha=lerp(1,saturate(_GlassOpacity+rim*.22),_Glass);
                return half4(color,alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster071"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex071
            #pragma fragment ShadowFragment071
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;half4 _EdgeColor;half _Glass;half _GlassOpacity;half _Smoothness;half _Specular;half _ShadowCoverage071;half _Glass073;
            CBUFFER_END
            // URP ShadowUtils supplies these for the active shadow-casting light.
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowInput071 { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct ShadowOutput071 { float4 positionCS:SV_POSITION; };
            ShadowOutput071 ShadowVertex071(ShadowInput071 input)
            {
                ShadowOutput071 output;
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS=TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS=normalize(_LightPosition-positionWS);
                #else
                float3 lightDirectionWS=_LightDirection;
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,lightDirectionWS));
                output.positionCS=ApplyShadowClamping(output.positionCS);
                return output;
            }
            half4 ShadowFragment071(ShadowOutput071 input):SV_TARGET
            {
                // Fixed ordered coverage in shadow-map pixels; no frame/time/face-dependent noise.
                // Ordinary dice keep every sample. Existing PCF performs the softening.
                static const half bayer[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                uint2 cell=((uint2)floor(input.positionCS.xy))&3u;
                half threshold=(bayer[cell.y*4u+cell.x]+0.5)/16.0;
                clip(saturate(_ShadowCoverage071)-threshold);
                return 0;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
