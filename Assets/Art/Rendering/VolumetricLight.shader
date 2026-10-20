// 体积光：沿视线步进，每一步采样主光源阴影贴图，累积被空气散射的光（Henyey-Greenstein 相函数）。
// 阳光穿过树冠形成光柱，夜里月光下的薄雾。由 VolumetricLightFeature 在后处理前执行。
Shader "Island/VolumetricLight"
{
    Properties
    {
        _Density ("Density", Float) = 0.01
        _Intensity ("Intensity", Float) = 1.6
        _Anisotropy ("Anisotropy", Range(0, 0.95)) = 0.72
        _MaxDistance ("Max Distance", Float) = 110
        _HeightFalloff ("Height Falloff", Float) = 0.045
        _NightScale ("Night Scale", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "VolumetricLight"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #define _MAIN_LIGHT_SHADOWS_CASCADE 1
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Density, _Intensity, _Anisotropy, _MaxDistance, _HeightFalloff, _NightScale;
            float _IslandUnderwater; // 水下时光柱更浓、带蓝绿色
            float _IslandVolumetricDim; // 天气：1 - 光柱倍数（阴雨天变淡，浓雾天变浓），0 为不影响

            float Hash(float2 p) { return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715)))); }

            float Hash2Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + 1);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(uv);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                float3 wpos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 cam = _WorldSpaceCameraPos;
                float3 ray = wpos - cam;
                float dist = length(ray);
                float3 dir = ray / max(dist, 1e-4);
                dist = min(dist, lerp(_MaxDistance, 35, _IslandUnderwater));

                Light L = GetMainLight();
                float g = _Anisotropy;
                float cosT = dot(dir, L.direction);
                float phase = (1 - g * g) / (4 * PI * pow(max(1 + g * g - 2 * g * cosT, 1e-3), 1.5));
                phase += 0.05; // 少量各向同性散射，背光方向也有一点雾感

                const int STEPS = 24;
                float stepLen = dist / STEPS;
                float jitter = Hash(input.positionCS.xy + frac(_Time.y * 61.7) * 100);
                float accum = 0, trans = 1;
                [loop]
                for (int i = 0; i < STEPS; i++)
                {
                    float3 p = cam + dir * ((i + jitter) * stepLen);
                    float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    float dens = _Density * exp(-max(p.y, 0) * _HeightFalloff);
                    if (_IslandUnderwater > 0 && p.y < 0)
                    {
                        // 水下：浓得多的散射，加上水面波纹把阳光切成一条条光柱（沿光线方向投到水面上取噪声）
                        float2 q = p.xz - L.direction.xz * (p.y / max(L.direction.y, 0.2));
                        float shafts = Hash2Noise(q * 0.35 + _Time.y * 0.15) * Hash2Noise(q * 0.9 - _Time.y * 0.22);
                        dens *= lerp(1, 14 * (0.25 + shafts * 2.2) * exp(p.y * 0.05), _IslandUnderwater);
                    }
                    accum += shadow * dens * trans * stepLen;
                    trans *= exp(-dens * stepLen);
                }
                float lum = max(L.color.r, max(L.color.g, L.color.b));
                float night = saturate(1 - lum * 2); // 月光很弱时放大一点，夜雾更明显
                half3 scatter = accum * phase * L.color * _Intensity * max(0, 1 - _IslandVolumetricDim) * (1 + night * _NightScale) * 4 * PI;
                scatter *= lerp(half3(1, 1, 1), half3(0.35, 0.85, 0.9), _IslandUnderwater);
                return half4(scene.rgb * lerp(1, trans, 0.25) + scatter, scene.a);
            }
            ENDHLSL
        }
    }
}
