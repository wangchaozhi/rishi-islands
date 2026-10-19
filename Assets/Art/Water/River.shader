// 溪流水面（URP）：沿网格 UV 的 v 方向流动的法线、深度吸收、折射、天空反射、浅处与岸边白水。
// 网格约定：u = 横向 0~1，v = 沿水流方向的距离（米）。需要 URP 资产开启 Depth / Opaque Texture。
Shader "Island/River"
{
    Properties
    {
        _ShallowColor ("Shallow Tint", Color) = (0.75, 0.95, 0.85, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.2, 0.18, 1)
        _Absorption ("Absorption (per meter)", Vector) = (0.6, 0.18, 0.2, 0)
        [Normal] _NormalMap ("Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.8
        _FlowSpeed ("Flow Speed (m/s)", Float) = 1.2
        _Tiling ("Normal Tiling", Float) = 0.35
        _Refraction ("Refraction", Range(0, 0.1)) = 0.03
        _FoamColor ("Foam Color", Color) = (0.92, 0.95, 0.95, 1)
        _FoamDepth ("Foam Depth", Float) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-110" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off // 水面网格的三角形朝向不重要，两面都画
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float4 _Absorption, _NormalMap_ST;
                float _NormalStrength, _FlowSpeed, _Tiling, _Refraction, _FoamDepth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float flow = _Time.y * _FlowSpeed;
                // 两层沿水流方向滚动、速度不同的法线
                float2 uv1 = float2(i.uv.x * 2.0, i.uv.y - flow) * _Tiling;
                float2 uv2 = float2(i.uv.x * 3.1 + 0.37, i.uv.y * 1.3 - flow * 1.6) * _Tiling * 1.7;
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                float2 dn = (n1.xy + n2.xy) * _NormalStrength;
                float3 N = normalize(float3(dn.x, 1, dn.y));
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);

                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float surf = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float thick = max(0, scene - surf);
                float2 ruv = suv + N.xz * _Refraction * saturate(thick);
                float rscene = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (rscene < surf) { ruv = suv; rscene = scene; }
                float path = max(0, rscene - surf);

                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 amb = SampleSH(half3(0, 1, 0));
                half3 lightCol = L.color * L.distanceAttenuation;
                half sun = saturate(L.direction.y);

                half3 below = SampleSceneColor(ruv) * exp(-_Absorption.xyz * path) * _ShallowColor.rgb;
                below = lerp(below, _DeepColor.rgb * (amb + lightCol * sun * 0.3), 1 - exp(-0.35 * path));

                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                half3 refl = GlossyEnvironmentReflection(R, 0.08, 1);
                float fres = 0.02 + 0.98 * pow(1 - saturate(dot(N, V)), 5);
                half3 col = lerp(below, refl, fres);
                float3 H = normalize(L.direction + V);
                col += lightCol * pow(saturate(dot(N, H)), 300) * 8 * L.shadowAttenuation * fres;

                // 浅处与岸边的白水，随水流移动
                float foamNoise = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1 * 2.3).r;
                float foam = saturate(1 - thick / _FoamDepth) * step(0.45, foamNoise);
                foam = max(foam, saturate(abs(i.uv.x - 0.5) * 2 - 0.85) * 4 * step(0.5, foamNoise));
                col = lerp(col, _FoamColor.rgb * (amb + lightCol * sun * 0.7), saturate(foam) * 0.75);

                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
