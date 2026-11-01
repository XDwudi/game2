Shader "DiceCraft/Art07/Felt"
{
    Properties { _BaseMap("Felt",2D)="white"{} _BaseColor("Color",Color)=(.09,.243,.25,1) _Grain("Grain",Float)=1 _Scanlines("Scanlines",Float)=0 _MenuDriftPixels("Menu drift pixels",Float)=0 _MenuBreath("Menu light breath",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;half4 _BaseColor;half _Grain;half _Scanlines;half _MenuDriftPixels;half _MenuBreath;
            CBUFFER_END
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            V vert(float4 p:POSITION,float2 uv:TEXCOORD0){V v;v.world=TransformObjectToWorld(p.xyz);v.pos=TransformWorldToHClip(v.world);v.uv=TRANSFORM_TEX(uv,_BaseMap);return v;}
            half4 frag(V v):SV_Target
            {
                half3 cloth=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv+ddx(v.uv)*_MenuDriftPixels).rgb;
                half3 color=lerp(_BaseColor.rgb,cloth,_Grain);
                Light key=GetMainLight(TransformWorldToShadowCoord(v.world));color*=lerp(.7,1,key.shadowAttenuation);
                color*=1-_Scanlines*.025*(.5+.5*sin(v.pos.y*1.57));color*=1+_MenuBreath;return half4(color,1);
            }
            ENDHLSL
        }
    }
}
