Shader "DiceCraft/Art07/Felt"
{
    Properties { _BaseMap("Felt",2D)="white"{} _TableMap("Table composition",2D)="white"{} _UseTableMap("Use table composition",Float)=0 _BaseColor("Color",Color)=(.09,.243,.25,1) _Grain("Grain",Float)=1 _Scanlines("Scanlines",Float)=0 _MenuDriftPixels("Menu drift pixels",Float)=0 _MenuBreath("Menu light breath",Float)=0 _Sweep072("Slow light sweep (v0.72)",Float)=0 }
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
            TEXTURE2D(_TableMap);SAMPLER(sampler_TableMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;float4 _TableMap_TexelSize;half4 _BaseColor;half _UseTableMap;half _Grain;half _Scanlines;half _MenuDriftPixels;half _MenuBreath;half _Sweep072;
            CBUFFER_END
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            V vert(float4 p:POSITION,float2 uv:TEXCOORD0){V v;v.world=TransformObjectToWorld(p.xyz);v.pos=TransformWorldToHClip(v.world);v.uv=TRANSFORM_TEX(uv,_BaseMap);return v;}
            half4 frag(V v):SV_Target
            {
                half3 cloth=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv+ddx(v.uv)*_MenuDriftPixels).rgb;
                half3 color=lerp(_BaseColor.rgb,cloth,_Grain);
                // Quiet light pool belongs to the table only; Canvas ink and dice stay untouched.
                float2 screen=v.pos.xy/GetScaledScreenParams().xy;
                float2 fromCenter=(screen-float2(.59,.47))*float2(1.12,1);
                half edge=smoothstep(.24,.78,length(fromCenter));
                color*=lerp(1.10,.86,edge);
                if(_UseTableMap>.5)
                {
                    // Contain the authored table without stretching. Extra aspect-ratio space
                    // is the dim room beyond the rim; UI and dice keep their existing design map.
                    float2 uv=GetNormalizedScreenSpaceUV(v.pos);
                    float aspect=GetScaledScreenParams().x/GetScaledScreenParams().y;
                    float authored=_TableMap_TexelSize.z/max(1,_TableMap_TexelSize.w);
                    float2 fit=float2(max(1,aspect/authored),max(1,authored/aspect));
                    float2 tableUV=(uv-.5)*fit+.5;
                    half inside=step(0,tableUV.x)*step(tableUV.x,1)*step(0,tableUV.y)*step(tableUV.y,1);
                    half3 tabletop=SAMPLE_TEXTURE2D(_TableMap,sampler_TableMap,saturate(tableUV)).rgb;
                    color=lerp(_BaseColor.rgb*.24,tabletop,inside);
                    // Grain/reduced-motion preferences affect only optional microdetail,
                    // never the table silhouette. Do not double the generated felt texture.
                    color*=1+_Grain*.006*(sin(v.pos.x*1.73)*sin(v.pos.y*2.13));
                    // v0.72: one wide, very soft band of light drifts across the felt (about 24s per pass).
                    // Felt only: the rim keeps its authored tones. Off under reduced motion.
                    half onFelt=inside*smoothstep(.03,.07,tableUV.x)*smoothstep(.97,.93,tableUV.x)*smoothstep(.05,.11,tableUV.y)*smoothstep(.95,.89,tableUV.y);
                    float sweep=frac((tableUV.x*.8+tableUV.y*.35)-_Time.y*.042);
                    color*=1+_Sweep072*onFelt*.045*smoothstep(0,.5,sweep)*smoothstep(1,.5,sweep);
                }
                Light key=GetMainLight(TransformWorldToShadowCoord(v.world));color*=lerp(.7,1,key.shadowAttenuation);
                color*=1-_Scanlines*.025*(.5+.5*sin(v.pos.y*1.57));color*=1+_MenuBreath;return half4(color,1);
            }
            ENDHLSL
        }
    }
}
