// 写实海面（URP）：Gerstner 波浪位移 + 深度吸收 + 折射 + 天空反射 + 太阳高光 + 岸边/浪尖泡沫。
// 需要 URP 资产开启 Depth Texture 和 Opaque Texture。
Shader "Island/Ocean"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Tint", Color) = (0.55, 0.95, 0.9, 1)
        _DeepColor ("Deep Scatter Color", Color) = (0.02, 0.16, 0.24, 1)
        _Absorption ("Absorption (per meter, RGB)", Vector) = (0.45, 0.09, 0.07, 0)
        _ScatterDensity ("Scatter Density", Float) = 0.18

        [Header(Surface)]
        [Normal] _NormalMap ("Detail Normal", 2D) = "bump" {}
        _NormalStrength ("Detail Normal Strength", Range(0, 3)) = 1.2
        _NormalTiling ("Detail Tiling (two layers)", Vector) = (0.045, 0.11, 0, 0)
        _NormalSpeed ("Detail Speed", Float) = 0.025
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _RefractionStrength ("Refraction", Range(0, 0.15)) = 0.04
        _ReflectionStrength ("Reflection", Range(0, 1.5)) = 1

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (0.95, 0.97, 1, 1)
        _ShoreFoamDistance ("Shore Foam Distance", Float) = 1.6
        _CrestFoam ("Crest Foam", Range(0, 2)) = 0.6

        [Header(Waves  dir.xy  steepness  wavelength)]
        _WaveA ("Wave A", Vector) = (1, 0.35, 0.16, 48)
        _WaveB ("Wave B", Vector) = (0.75, 1, 0.13, 27)
        _WaveC ("Wave C", Vector) = (-0.35, 0.9, 0.11, 15)
        _WaveD ("Wave D", Vector) = (0.3, -0.95, 0.08, 7.5)
        _WaveScale ("Wave Scale", Range(0, 1.5)) = 1

        [Header(Island)]
        _IslandHeight ("Island Height (R = terrain height, normalized)", 2D) = "black" {}
        _IslandRect ("Island Rect (minX, minZ, sizeX, sizeZ)", Vector) = (-300, -300, 600, 600)
        _IslandHeightRange ("Height Range (min, size)", Vector) = (-20, 80, 0, 0)
        _ShoreWaveDepth ("Waves Fade Depth", Float) = 7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_IslandHeight); SAMPLER(sampler_IslandHeight);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float4 _Absorption;
                float _ScatterDensity;
                float _NormalStrength;
                float4 _NormalTiling;
                float _NormalSpeed, _Smoothness, _RefractionStrength, _ReflectionStrength;
                float _ShoreFoamDistance, _CrestFoam;
                float4 _WaveA, _WaveB, _WaveC, _WaveD;
                float _WaveScale;
                float4 _IslandHeight_ST;
                float4 _IslandRect, _IslandHeightRange;
                float _ShoreWaveDepth;
                float4 _NormalMap_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  crest      : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            float3 Gerstner(float4 wave, float3 p, float fade, inout float3 tangent, inout float3 binormal)
            {
                float steep = wave.z * _WaveScale * fade;
                float k = TWO_PI / wave.w;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y);
                float a = steep / k;
                float s = sin(f), co = cos(f);
                tangent  += float3(-d.x * d.x * steep * s, d.x * steep * co, -d.x * d.y * steep * s);
                binormal += float3(-d.x * d.y * steep * s, d.y * steep * co, -d.y * d.y * steep * s);
                return float3(d.x * a * co, a * s, d.y * a * co);
            }

            float WaterDepthAt(float2 xz)
            {
                float2 uv = (xz - _IslandRect.xy) / _IslandRect.zw;
                if (any(uv < 0) || any(uv > 1)) return 100;
                float h = SAMPLE_TEXTURE2D_LOD(_IslandHeight, sampler_IslandHeight, uv, 0).r;
                return -(_IslandHeightRange.x + h * _IslandHeightRange.y); // 海平面 y=0 下方的水深
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 grid = p;

                // 浅水区和远处减弱波浪（远处网格稀疏，短波会走样）
                float depth = WaterDepthAt(grid.xz);
                float shore = saturate(depth / _ShoreWaveDepth);
                float dist = distance(grid.xz, _WorldSpaceCameraPos.xz);

                float3 tangent = float3(1, 0, 0), binormal = float3(0, 0, 1);
                float3 offs = 0;
                offs += Gerstner(_WaveA, grid, shore * saturate(1 - dist / (_WaveA.w * 25)), tangent, binormal);
                offs += Gerstner(_WaveB, grid, shore * saturate(1 - dist / (_WaveB.w * 20)), tangent, binormal);
                offs += Gerstner(_WaveC, grid, shore * saturate(1 - dist / (_WaveC.w * 16)), tangent, binormal);
                offs += Gerstner(_WaveD, grid, shore * saturate(1 - dist / (_WaveD.w * 12)), tangent, binormal);
                p += offs;

                o.positionWS = p;
                o.normalWS = normalize(cross(binormal, tangent));
                o.crest = offs.y;
                o.positionCS = TransformWorldToHClip(p);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 posWS = i.positionWS;
                float3 V = normalize(_WorldSpaceCameraPos - posWS);

                // 细节法线：两层反向滚动
                float t = _Time.y * _NormalSpeed;
                float2 uv1 = posWS.xz * _NormalTiling.x + float2(t, t * 0.6);
                float2 uv2 = posWS.xz * _NormalTiling.y + float2(-t * 0.7, t * 1.1);
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                float camDist = distance(posWS, _WorldSpaceCameraPos);
                float detail = _NormalStrength * lerp(1, 0.35, saturate(camDist / 300));
                float2 dn = (n1.xy + n2.xy) * detail;
                float3 N = normalize(i.normalWS + float3(dn.x, 0, dn.y));

                // 屏幕空间：场景深度、折射
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float surfaceEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(0, sceneEye - surfaceEye);

                float2 refrUV = screenUV + N.xz * _RefractionStrength * saturate(thickness / 2);
                float refrEye = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrEye < surfaceEye) { refrUV = screenUV; refrEye = sceneEye; }
                float refrThickness = max(0, refrEye - surfaceEye);
                // 视线方向的水中路程换算成大致垂直深度，吸收更自然
                float pathLen = refrThickness;

                half3 sceneColor = SampleSceneColor(refrUV);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(posWS));
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 lightCol = mainLight.color * mainLight.distanceAttenuation;
                half sunAmount = saturate(dot(half3(0, 1, 0), mainLight.direction));

                // 吸收与散射
                float3 transmit = exp(-_Absorption.xyz * pathLen);
                float scatterAmt = 1 - exp(-_ScatterDensity * pathLen);
                half3 scatter = _DeepColor.rgb * (ambient * 1.2 + lightCol * sunAmount * 0.35);
                half3 underwater = sceneColor * transmit * _ShallowColor.rgb;
                underwater = lerp(underwater, scatter, scatterAmt);

                // 反射
                float3 R = reflect(-V, N);
                R.y = abs(R.y); // 避免采样到地平线以下
                half perceptualRoughness = 1 - _Smoothness;
                half3 refl = GlossyEnvironmentReflection(R, perceptualRoughness, 1) * _ReflectionStrength;
                float NdotV = saturate(dot(N, V));
                float fresnel = 0.02 + 0.98 * pow(1 - NdotV, 5);

                half3 col = lerp(underwater, refl, fresnel);

                // 太阳高光
                float3 H = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(N, H)), 900) * 60 + pow(saturate(dot(N, H)), 120) * 0.6;
                col += lightCol * spec * mainLight.shadowAttenuation * fresnel * 4;

                // 泡沫：岸边 + 浪尖
                float foamNoise = Noise(posWS.xz * 1.7 + _Time.y * 0.4) * 0.6 + Noise(posWS.xz * 4.3 - _Time.y * 0.3) * 0.4;
                float shoreFoam = saturate(1 - thickness / _ShoreFoamDistance);
                shoreFoam *= step(0.35 + (1 - shoreFoam) * 0.5, foamNoise + shoreFoam * 0.5);
                float crestFoam = saturate((i.crest - 0.9) * _CrestFoam) * step(0.55, foamNoise);
                float foam = saturate(shoreFoam + crestFoam);
                half3 foamLit = _FoamColor.rgb * (ambient + lightCol * (0.4 + 0.6 * mainLight.shadowAttenuation) * sunAmount);
                col = lerp(col, foamLit, foam * 0.9);

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // 写深度给阴影接收、SSAO 等（海面本身不投射阴影）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct Vt { float4 positionCS : SV_POSITION; };
            Vt vert(A v) { Vt o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag(Vt i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
