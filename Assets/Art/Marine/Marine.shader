// 海洋生物通用着色器：鱼、珊瑚、海葵、海草、海带共用。
// 颜色来自顶点色 × 贴图 × _BaseColor；顶点色 alpha 是形变权重（海草根部 0、梢部 1；鱼头 0、鱼尾 1）。
// 两种动画：Sway 世界空间随水流摆动（植物），Swim 物体空间左右摆尾（鱼，鱼头朝 +Z）。
// 光照：主光（含阴影和焦散 cookie）+ 球谐环境光 + 包裹漫反射 + 背光透射 + 菲涅尔鳞光，雾。
Shader "Island/Marine"
{
    Properties
    {
        _BaseMap ("Base Map (RGB, A = cutout)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _SpecColor2 ("Specular Color", Color) = (0.25, 0.25, 0.25, 1)
        _Sheen ("Fresnel Sheen (fish scales)", Range(0, 2)) = 0
        _Wrap ("Wrap Lighting", Range(0, 1)) = 0.3
        _Translucency ("Translucency", Range(0, 2)) = 0
        _Emission ("Emission (bioluminescence)", Color) = (0, 0, 0, 0)

        [Header(Sway)]
        _SwayAmount ("Sway Amount (m)", Float) = 0
        _SwaySpeed ("Sway Speed", Float) = 0.8
        _SwayScale ("Sway Spatial Scale", Float) = 0.35

        [Header(Swim)]
        _SwimAmount ("Swim Amount (m)", Float) = 0
        _SwimSpeed ("Swim Speed", Float) = 8
        _SwimFreq ("Swim Wave Frequency", Float) = 6

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _SpecColor2, _Emission;
        half _Cutoff, _Smoothness, _Sheen, _Wrap, _Translucency;
        float _SwayAmount, _SwaySpeed, _SwayScale;
        float _SwimAmount, _SwimSpeed, _SwimFreq;
    CBUFFER_END

    // 每个物体一个相位，免得一群鱼 / 一片海草动作完全一致
    float ObjectPhase()
    {
        float3 o = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
        return frac(sin(dot(o.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831;
    }

    // 返回形变后的物体空间位置
    float3 Deform(float3 posOS, float weight)
    {
        float phase = ObjectPhase();
        // 鱼：沿身体的行波，越靠尾巴摆幅越大
        float wave = sin(posOS.z * _SwimFreq - _Time.y * _SwimSpeed + phase);
        posOS.x += wave * _SwimAmount * weight * weight;
        posOS.x += sin(_Time.y * _SwimSpeed * 0.5 + phase) * _SwimAmount * 0.15; // 身体整体轻微摆动

        // 植物：世界空间水流，梢部摆得多；两个频率叠加更自然
        if (_SwayAmount > 0)
        {
            float3 ws = TransformObjectToWorld(posOS);
            float t = _Time.y * _SwaySpeed;
            float s1 = sin(t + dot(ws.xz, float2(0.7, 0.5)) * _SwayScale + phase);
            float s2 = sin(t * 2.3 + ws.y * 0.9 + phase * 1.7);
            float w = pow(saturate(weight), 1.5);
            float3 offs = float3(s1 * 0.8 + s2 * 0.25, -abs(s1) * 0.15, cos(t * 0.8 + phase) * 0.5 + s2 * 0.2) * _SwayAmount * w;
            ws += offs;
            posOS = TransformWorldToObject(ws);
        }
        return posOS;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half4  color      : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 p = Deform(v.positionOS.xyz, v.color.a);
                o.positionWS = TransformObjectToWorld(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                half3 albedo = tex.rgb * i.color.rgb * _BaseColor.rgb;

                float3 N = normalize(i.normalWS) * (IS_FRONT_VFACE(face, 1, -1));
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1, 1, 1, 1));
                half3 lightCol = L.color * L.distanceAttenuation * L.shadowAttenuation;

                half ndl = dot(N, L.direction);
                half diff = saturate((ndl + _Wrap) / (1 + _Wrap));
                half3 ambient = SampleSH(N);
                half3 col = albedo * (lightCol * diff + ambient);

                // 背光透射（海草、海带、鳍在逆光下发亮）
                half back = pow(saturate(dot(V, -L.direction)), 4) * _Translucency;
                col += albedo * lightCol * back;

                // 高光
                float3 H = normalize(L.direction + V);
                half spec = pow(saturate(dot(N, H)), exp2(1 + _Smoothness * 10)) * _Smoothness;
                col += _SpecColor2.rgb * lightCol * spec;

                // 鱼鳞：掠射角的银色反光
                half fres = pow(1 - saturate(dot(N, V)), 3);
                col += _Sheen * fres * (GlossyEnvironmentReflection(reflect(-V, N), 0.25, 1) * 0.6 + lightCol * 0.15);

                col += _Emission.rgb;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = Deform(v.positionOS.xyz, v.color.a);
                float3 ws = TransformObjectToWorld(p);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - ws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(Deform(v.positionOS.xyz, v.color.a));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
