Shader "DiceCraft/Art07/Felt"
{
    Properties { _BaseMap("Felt",2D)="white"{} _TableMap("Table composition",2D)="white"{} _UseTableMap("Use table composition",Float)=0 _BaseColor("Color",Color)=(.09,.243,.25,1) _Grain("Grain",Float)=1 _Scanlines("Scanlines",Float)=0 _MenuDriftPixels("Menu drift pixels",Float)=0 _MenuBreath("Menu light breath",Float)=0 _Sweep072("Slow light sweep (v0.72)",Float)=0 _Dim11("Boss stage dim (v1.1)",Float)=0 _Vignette11("Boss stage vignette (v1.1)",Float)=0
        _ChapterMasks18("Four chapter material masks (linear RGBA)",2D)="white"{}
        _ChapterShift18("Chapter colour change and surface mask",Vector)=(0,0,0,0)
        _MenuTray17("Menu tray",2D)="black"{} _ShowMenuTray17("Draw menu tray",Float)=0 }
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
            TEXTURE2D(_MenuTray17);SAMPLER(sampler_MenuTray17);
            TEXTURE2D(_ChapterMasks18);SAMPLER(sampler_ChapterMasks18);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;float4 _TableMap_TexelSize;half4 _BaseColor;half _UseTableMap;half _Grain;half _Scanlines;half _MenuDriftPixels;half _MenuBreath;half _Sweep072;half _Dim11;half _Vignette11;
            float4 _ChapterShift18;half _ShowMenuTray17;
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
                    if(_ChapterShift18.w>0)
                    {
                        // The default felt starts inside the authored rounded wooden rail
                        // (20 px inset, radius 13). Swiss ink and red stay outside its paper mask.
                        float2 size=_TableMap_TexelSize.zw;
                        float2 q=abs((tableUV-.5)*size)-(size*.5-33);
                        float inward=13-length(max(q,0))-min(max(q.x,q.y),0);
                        float mask=_ChapterShift18.w<1.5 ? smoothstep(0,12,inward)
                            : smoothstep(.12,.65,min(tabletop.r,min(tabletop.g,tabletop.b)));
                        if(_ChapterShift18.w>=3)
                        {
                            // Shared native-resolution RGBA image selects only each style's surface material.
                            half4 coverage=SAMPLE_TEXTURE2D(_ChapterMasks18,sampler_ChapterMasks18,saturate(tableUV));
                            mask=_ChapterShift18.w<3.5?coverage.r:_ChapterShift18.w<4.5?coverage.g:_ChapterShift18.w<5.5?coverage.b:coverage.a;
                        }
                        tabletop=saturate(tabletop+mask*_ChapterShift18.rgb);
                    }
                    if (_ShowMenuTray17 > .5)
                    {
                        // The approved design rect is (90,424,1060,552) in the 1920x1080 top-left map.
                        float2 trayUV=(tableUV*float2(1920,1080)-float2(90,104))/float2(1060,552);
                        if (all(trayUV>=0) && all(trayUV<=1))
                        {
                            half4 tray=SAMPLE_TEXTURE2D(_MenuTray17,sampler_MenuTray17,trayUV);
                            tabletop=lerp(tabletop,tray.rgb,tray.a);
                        }
                    }
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
                // V1.1 (W11, B2): a boss stage darkens the table and closes a vignette in. Felt only: dice and the interface
                // keep their own light. Both are 0 outside a boss stage, which leaves the table exactly as authored.
                half vignette11=smoothstep(.22,.8,length((screen-float2(.5,.47))*float2(1.18,1)));
                color*=(1-_Dim11)*(1-_Vignette11*vignette11);
                Light key=GetMainLight(TransformWorldToShadowCoord(v.world));color*=lerp(.7,1,key.shadowAttenuation);
                color*=1-_Scanlines*.025*(.5+.5*sin(v.pos.y*1.57));color*=1+_MenuBreath;return half4(color,1);
            }
            ENDHLSL
        }
    }
}
