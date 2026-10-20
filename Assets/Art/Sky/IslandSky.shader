// 程序化天空：大气渐变 + 日落色 + 太阳圆盘与光晕 + 飘动云层 + 夜晚星空与月亮。
// 太阳/月亮方向由 DayNightCycle 通过全局变量 _IslandSunDir / _IslandMoonDir 传入。
Shader "Island/Sky"
{
    Properties
    {
        _DayZenith ("Day Zenith", Color) = (0.16, 0.38, 0.78, 1)
        _DayHorizon ("Day Horizon", Color) = (0.62, 0.78, 0.93, 1)
        _SunsetHorizon ("Sunset Horizon", Color) = (1.0, 0.48, 0.2, 1)
        _SunsetZenith ("Sunset Zenith", Color) = (0.28, 0.3, 0.55, 1)
        _NightZenith ("Night Zenith", Color) = (0.004, 0.008, 0.022, 1)
        _NightHorizon ("Night Horizon", Color) = (0.02, 0.035, 0.07, 1)
        _GroundColor ("Below Horizon", Color) = (0.28, 0.33, 0.38, 1)
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun Size", Range(0.0001, 0.01)) = 0.00045
        _SunIntensity ("Sun Disk Intensity", Float) = 14
        _MoonSize ("Moon Size", Range(0.0002, 0.005)) = 0.0007
        _MoonColor ("Moon Color", Color) = (0.92, 0.94, 1, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.48
        _CloudSoftness ("Cloud Softness", Range(0.01, 1)) = 0.3
        _CloudScale ("Cloud Scale", Float) = 0.9
        _CloudSpeed ("Cloud Speed", Float) = 0.004
        _CloudBrightness ("Cloud Brightness", Float) = 1.15
        _StarDensity ("Star Grid", Float) = 220
        _Exposure ("Exposure", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _MoonColor, _DayZenith, _DayHorizon, _SunsetHorizon, _SunsetZenith, _NightZenith, _NightHorizon, _GroundColor, _SunColor;
            float _SunSize, _SunIntensity, _MoonSize, _CloudCoverage, _CloudSoftness, _CloudScale, _CloudSpeed, _CloudBrightness, _StarDensity, _Exposure;
            float4 _IslandSunDir;   // xyz 指向太阳
            float4 _IslandMoonDir;  // xyz 指向月亮
            float _IslandSkyFlash;  // 闪电时整片天空瞬间变亮（StormFX / WeatherSystem 写入）
            float _IslandCloudCover; // 天气的云量（>0 时代替材质上的 _CloudCoverage）
            float _IslandOvercast;   // 阴沉程度：整片天空盖上一层灰云

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash2(i), hash2(i + float2(1, 0)), u.x), lerp(hash2(i + float2(0, 1)), hash2(i + 1), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 6; k++) { s += a * noise2(p); p = p * 2.03 + float2(17.3, 9.1); a *= 0.5; }
                return s;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sun = normalize(_IslandSunDir.xyz + float3(0, 1e-4, 0));
                float3 moon = normalize(_IslandMoonDir.xyz + float3(0, 1e-4, 0));
                float sunH = sun.y;

                float day = smoothstep(-0.12, 0.22, sunH);
                float sunset = saturate(1 - abs(sunH - 0.02) / 0.22) * smoothstep(-0.2, 0.0, sunH);
                float up = saturate(dir.y);
                float horizonT = pow(1 - up, 4);

                // 大气
                half3 dayCol = lerp(_DayZenith.rgb, _DayHorizon.rgb, horizonT);
                float towardSun = pow(saturate(dot(normalize(float3(dir.x, 0, dir.z) + 1e-5), normalize(float3(sun.x, 0, sun.z) + 1e-5)) * 0.5 + 0.5), 3);
                half3 sunsetCol = lerp(_SunsetZenith.rgb, _SunsetHorizon.rgb, horizonT * (0.35 + 0.65 * towardSun));
                half3 nightCol = lerp(_NightZenith.rgb, _NightHorizon.rgb, horizonT);
                half3 col = lerp(nightCol, dayCol, day);
                col = lerp(col, sunsetCol, sunset * (0.45 + 0.55 * horizonT));

                // 地平线以下（海面反射会用到）
                if (dir.y < 0)
                {
                    half3 below = _GroundColor.rgb * lerp(0.03, 1, day);
                    col = lerp(col, below, saturate(-dir.y * 6));
                }

                // 太阳：大气光晕 + 日冕 + 带临边昏暗的圆盘
                float cosSun = dot(dir, sun);
                float sunVis = smoothstep(-0.1, 0.05, sunH);
                half3 sunTint = lerp(_SunsetHorizon.rgb * 1.3, _SunColor.rgb, saturate(sunH * 4));
                col += sunTint * (pow(saturate(cosSun), 10) * 0.18 + pow(saturate(cosSun), 300) * 0.35 + pow(saturate(cosSun), 3000) * 1.2) * sunVis;
                float sunR = saturate((1 - cosSun) / _SunSize);            // 0 = 圆心，1 = 边缘
                float disk = (1 - smoothstep(0.85, 1.0, sunR)) * smoothstep(-0.015, 0.005, dir.y);
                float limb = lerp(0.55, 1, sqrt(saturate(1 - sunR * sunR)));
                half3 sunDisk = sunTint * disk * limb * _SunIntensity * lerp(0.35, 1, saturate(sunH * 6 + 0.3));

                // 星空
                float night = 1 - smoothstep(-0.18, 0.05, sunH);
                if (night > 0 && dir.y > 0)
                {
                    float3 p = dir * _StarDensity;
                    float3 cell = floor(p);
                    float h = hash3(cell);
                    float3 center = cell + 0.5 + (float3(hash3(cell + 3.1), hash3(cell + 7.7), hash3(cell + 11.3)) - 0.5) * 0.6;
                    float d = length(p - center);
                    float star = step(0.985, h) * smoothstep(0.18, 0.0, d) * (0.5 + 0.5 * sin(_Time.y * (2 + h * 5) + h * 40));
                    col += star * night * smoothstep(0, 0.2, dir.y) * 2.5;
                }

                // 云层（平面投影到天穹）
                float cloudCover = 0;
                if (dir.y > 0)
                {
                    float2 uv = dir.xz / (dir.y + 0.08) * _CloudScale;
                    float2 wind = float2(1, 0.35) * _Time.y * _CloudSpeed;
                    float n = fbm(uv + wind);
                    float coverage = 1 - (_IslandCloudCover > 0.001 ? _IslandCloudCover : _CloudCoverage);
                    float density = smoothstep(coverage - 0.05, coverage + _CloudSoftness, n);
                    // 朝太阳方向偏移采样，估算云的自阴影
                    float n2 = fbm(uv + wind + sun.xz * 0.08);
                    float lit = saturate(1 - (n2 - n) * 6 - density * 0.35);
                    half3 skyAmb = lerp(nightCol, _DayHorizon.rgb, day);
                    half3 sunLight = lerp(_SunsetHorizon.rgb * 1.2, half3(1, 1, 1), saturate(sunH * 3)) * day;
                    half3 cloudCol = skyAmb * 0.55 + sunLight * lit * _CloudBrightness + half3(0.02, 0.025, 0.035) * night;
                    cloudCol += sunTint * pow(saturate(cosSun), 8) * (1 - density) * 0.8 * day; // 云边透光
                    float cloudLum = dot(cloudCol, half3(0.3, 0.55, 0.15));
                    cloudCol = lerp(cloudCol, cloudLum * half3(0.78, 0.8, 0.84), _IslandOvercast * 0.75); // 阴天的云是灰的
                    float fade = smoothstep(0.0, 0.12, dir.y);
                    cloudCover = density * fade;
                    col = lerp(col, cloudCol, cloudCover * 0.95);
                }

                // 阴天：整片天空盖上一层均匀的灰云，地平线附近略亮
                if (_IslandOvercast > 0.001)
                {
                    half3 grey = lerp(nightCol * 1.3 + 0.004, half3(0.52, 0.55, 0.6), day) * lerp(1.08, 0.82, saturate(dir.y));
                    float layer = _IslandOvercast * smoothstep(-0.05, 0.1, dir.y);
                    col = lerp(col, grey, layer);
                    cloudCover = max(cloudCover, layer);
                }

                // 太阳圆盘和月亮画在云后：薄云能透出来，厚云挡住
                float clear = 1 - saturate(cloudCover * 1.1);
                col += sunDisk * lerp(0.2, 1, clear);

                // 月亮：球面法线 + 太阳照明得到月相，噪声做月海和环形山；白天也淡淡挂在天上
                float cosMoon = dot(dir, moon);
                if (dir.y > -0.02 && cosMoon > 0.9)
                {
                    float3 mUp = abs(moon.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                    float3 mRight = normalize(cross(mUp, moon));
                    mUp = cross(moon, mRight);
                    float mRad = sqrt(2 * _MoonSize);                      // 角半径（弧度近似）
                    float2 mp = float2(dot(dir, mRight), dot(dir, mUp)) / mRad;
                    float r2 = dot(mp, mp);
                    float moonA = (1 - smoothstep(0.92, 1.0, sqrt(r2))) * smoothstep(-0.02, 0.01, dir.y);
                    float3 n = mRight * mp.x + mUp * mp.y - moon * sqrt(saturate(1 - r2));
                    float lit = smoothstep(-0.08, 0.12, dot(n, sun));      // 被太阳照亮的一侧
                    float maria = fbm(mp * 2.2 + 3.7);
                    float craters = fbm(mp * 7.0 + 11.0);
                    half3 surface = _MoonColor.rgb * lerp(0.38, 1.0, smoothstep(0.3, 0.62, maria)) * lerp(0.7, 1.1, craters);
                    half3 earthshine = surface * 0.02 * night;                // 暗面的地照
                    half3 moonCol = surface * lit * lerp(0.9, 1.25, night) + earthshine;
                    col = lerp(col, col * (1 - 0.6 * night) + moonCol, moonA * lerp(0.45, 1, night) * clear);
                }
                col += _MoonColor.rgb * pow(saturate(cosMoon), 120) * 0.05 * night * step(0, dir.y);


                col += half3(0.55, 0.6, 0.78) * _IslandSkyFlash * (0.4 + 0.6 * saturate(dir.y + 0.3));
                return half4(col * _Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
