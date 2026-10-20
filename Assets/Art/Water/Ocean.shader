// 写实海面（URP）：Gerstner 波浪位移 + 深度吸收 + 折射 + 天空反射 + 太阳高光 + 岸边/浪尖泡沫
// + 岸边涌浪（水位周期性漫上沙滩）与推岸的白沫浪线 + 浅水海底的焦散光斑。
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

        [Header(Surf  waves rolling onto the beach)]
        _SurfDepth ("Surf Zone Depth (m)", Float) = 4
        _SurfPeriod ("Surf Period (s)", Float) = 7
        _SurfSpacing ("Surf Band Spacing (m of depth)", Float) = 1.6
        _SurfFoam ("Surf Foam", Range(0, 1.5)) = 0.9
        _SurgeHeight ("Surge Height (m)", Float) = 0.14

        [Header(Caustics on the shallow seabed)]
        _CausticStrength ("Caustic Strength", Range(0, 4)) = 1.6
        _CausticScale ("Caustic Scale (cells per m)", Float) = 0.45
        _CausticDepth ("Caustic Fade Depth (m)", Float) = 9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off // 水下能看到水面的背面
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
                float _SurfDepth, _SurfPeriod, _SurfSpacing, _SurfFoam, _SurgeHeight;
                float _CausticStrength, _CausticScale, _CausticDepth;
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
                float  seabed     : TEXCOORD4; // 高度图里的水深（米）
            };

            float _IslandWaveBoost; // 天气：海浪倍数 - 1（0 为不影响）
            float _IslandRain;      // 天气：雨量 0~1，水面起雨点涟漪

            float3 Gerstner(float4 wave, float3 p, float fade, inout float3 tangent, inout float3 binormal)
            {
                float steep = wave.z * _WaveScale * max(0, 1 + _IslandWaveBoost) * fade;
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

            // 涌浪相位：一组浪线从 _SurfDepth 深处推向岸边，周期 _SurfPeriod 秒；水深越浅相位越靠后
            float SurfPhase(float depth) { return _Time.y / _SurfPeriod + depth / _SurfSpacing; }

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
                // 岸边涌浪：浪线到岸时水位抬高、退去时回落，水线在沙滩上来回推移
                float surfZone = saturate(1 - depth / _SurfDepth) * step(-0.5, depth);
                offs.y += _SurgeHeight * surfZone * sin(TWO_PI * (SurfPhase(max(depth, 0)) - 0.7)); // 浪线前沿（相位 0.95）到达时水位最高
                p += offs;

                o.positionWS = p;
                o.seabed = depth;
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

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 焦散：动态 Voronoi 的 F2-F1，在细胞边界处是一条条亮线
            float CausticLayer(float2 p, float t)
            {
                float2 i = floor(p), f = frac(p);
                float d1 = 8, d2 = 8;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = 0.5 + 0.42 * sin(t + TWO_PI * Hash2(i + g));
                    float d = length(g + o - f);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
                return 1 - smoothstep(0.0, 0.14, d2 - d1);
            }

            float Caustics(float2 p)
            {
                float t = _Time.y * 0.9;
                float a = CausticLayer(p, t);
                float b = CausticLayer(p * 1.37 + float2(3.1, 7.7), -t * 1.2);
                return pow(saturate((a + b) * 0.62), 2.2);
            }

            // 雨点涟漪：每个格子里一滴雨，落下后一圈波纹向外扩散；返回法线的 xz 偏移
            float2 RippleHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float2 RainRipples(float2 p, float t)
            {
                float2 i = floor(p), f = frac(p);
                float2 n = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 h = RippleHash(i + g);
                    float2 c = g + 0.2 + 0.6 * h - f;
                    float life = frac(t + h.x * 7.31 + h.y * 3.17);
                    float r = length(c);
                    float d = r - life * 0.9;
                    float wave = sin(d * 38) * exp(-d * d * 160) * (1 - life) * (1 - life);
                    n += c / max(r, 1e-3) * wave;
                }
                return n;
            }
            float2 Rain2(float2 xz) { return RainRipples(xz * 2.3, _Time.y * 1.3) + RainRipples(xz * 3.4 + 5.3, _Time.y * 1.6) * 0.7; }

            float _IslandUnderwater; // UnderwaterFX 写入：摄像机在水下时为 1

            // 从水下往上看：斯涅尔窗口内透出天空和太阳，窗口外是全反射的深水色
            half3 Underside(Varyings i, float3 N, float3 V, float2 screenUV)
            {
                float3 Nb = -N;
                float cosT = saturate(dot(Nb, V));
                float window = smoothstep(0.6, 0.76, cosT); // 临界角约 48.6°
                // 折射进来的方向（从水下看出去）
                float3 refr = refract(-V, Nb, 1.33);
                if (dot(refr, refr) < 1e-4) refr = reflect(-V, Nb);
                half3 sky = GlossyEnvironmentReflection(refr, 0.05, 1);
                Light mainLight = GetMainLight();
                half3 lightCol = mainLight.color * mainLight.distanceAttenuation;
                half3 ambient = SampleSH(half3(0, 1, 0));
                float sunUp = saturate(mainLight.direction.y);
                float sunGlint = pow(saturate(dot(refr, mainLight.direction)), 400) * 40 + pow(saturate(dot(refr, mainLight.direction)), 20) * 0.6;
                half3 through = (sky * 0.85 + lightCol * sunGlint * sunUp) * half3(0.75, 0.95, 1.0);
                half3 tir = _DeepColor.rgb * (ambient * 0.9 + lightCol * sunUp * 0.25);
                // 波纹边缘的亮线
                float ripple = pow(1 - abs(dot(N, float3(0, 1, 0))), 3) * 2;
                half3 col = lerp(tir, through, window) + lightCol * ripple * 0.06 * sunUp;
                return col;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
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
                if (_IslandRain > 0.001)
                {
                    float2 rp = Rain2(posWS.xz);
                    N = normalize(N + float3(rp.x, 0, rp.y) * _IslandRain * 0.5 * saturate(1 - camDist / 70));
                }

                // 屏幕空间：场景深度、折射
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                if (!IS_FRONT_VFACE(face, true, false) || _WorldSpaceCameraPos.y < posWS.y - 0.02)
                {
                    half3 uc = Underside(i, N, V, screenUV);
                    return half4(MixFog(uc, i.fogFactor), 1);
                }
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
                // 浅水海底的焦散：沿视线把折射处的场景深度还原成海底位置，沿阳光方向投影后叠加光斑（受阴影遮挡）
                float3 bed = _WorldSpaceCameraPos + (posWS - _WorldSpaceCameraPos) * (refrEye / max(surfaceEye, 1e-3));
                float bedDepth = -bed.y;
                float causticFade = saturate(bedDepth * 2) * saturate(1 - bedDepth / _CausticDepth) * saturate(thickness * 3);
                if (causticFade > 0.001 && sunAmount > 0.02)
                {
                    float2 cp = (bed.xz - mainLight.direction.xz / max(mainLight.direction.y, 0.2) * bed.y) * _CausticScale;
                    float bedShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(bed));
                    sceneColor *= 1 + Caustics(cp) * _CausticStrength * causticFade * sunAmount * bedShadow * lightCol;
                }
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
                // 推岸的白沫浪线：浪线前沿是一道亮白的碎浪，身后拖着逐渐散开的泡沫
                float surfZone = saturate(1 - i.seabed / _SurfDepth) * saturate(i.seabed * 1.5 + 0.3);
                float band = frac(SurfPhase(max(i.seabed, 0)) + Noise(posWS.xz * 0.08) * 0.25);
                float front = smoothstep(0.9, 0.97, band) * (1 - smoothstep(0.97, 1.0, band));
                float trail = (1 - smoothstep(0.0, 0.22, band)) * 0.35;
                float surfFoam = saturate((front + trail) * surfZone * _SurfFoam) * smoothstep(0.38, 0.62, foamNoise + front * 0.25);
                float foam = saturate(shoreFoam + crestFoam + surfFoam);
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
