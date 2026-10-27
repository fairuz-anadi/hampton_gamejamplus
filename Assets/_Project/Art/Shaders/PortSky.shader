// The game's sky. Owner: Samprity.
// Physically based: single scattering through a thin shell of air round the planet (Rayleigh for the blue, Mie for
// the haze and glow round the sun), marched per pixel, so the colour of noon, the orange of a low sun and the
// twilight after it falls out of the sun's real height. On top: a drifting layer of cumulus lit from the sun's side
// (silver linings, darker bases), the moon lighting the air faintly at night, and stars. The horizon blends into the
// fog colour so the sea meets it without a seam. Driven by PartMood from the day-night clock and the story parts.
Shader "Port/Sky"
{
    Properties
    {
        _SunDir ("Sun Direction (towards it, true height)", Vector) = (0,0.6,0.8,0)
        _SunStrength ("Sun Strength (overcast < 1)", Range(0,1.5)) = 1
        _MoonDir ("Moon Direction", Vector) = (0,0.7,-0.7,0)
        _MoonStrength ("Moon Light", Range(0,1)) = 0
        _Exposure ("Exposure", Range(0.1,4)) = 1.2
        _Turbidity ("Haze (Mie amount)", Range(0.2,4)) = 1
        _MieG ("Sun Glow Tightness", Range(0.5,0.95)) = 0.76
        [HDR] _SkyTint ("Tint (white = as is)", Color) = (1,1,1,1)
        _HorizonColor ("Horizon Haze (= fog)", Color) = (0.74,0.83,0.92,1)
        _HazeAmount ("Horizon Haze", Range(0,1)) = 0.65
        _Stars ("Stars", Range(0,1)) = 0

        [Header(Clouds)]
        _NoiseTex ("Noise (tileable RGBA)", 2D) = "gray" {}
        _CloudCover ("Cover", Range(0,1)) = 0.35
        _CloudScale ("Scale", Float) = 0.6
        _CloudWind ("Drift (xz per second, in noise tiles)", Vector) = (0.004,-0.012,0,0)
        _CloudDarkness ("Base Darkness", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunDir, _MoonDir;
                float _SunStrength, _MoonStrength, _Exposure, _Turbidity, _MieG;
                float4 _SkyTint, _HorizonColor;
                float _HazeAmount, _Stars;
                float4 _NoiseTex_ST;
                float _CloudCover, _CloudScale, _CloudDarkness;
                float4 _CloudWind;
            CBUFFER_END

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            // ---------- Atmosphere (single scattering, after Nishita / glsl-atmosphere) ----------
            #define R_PLANET 6371e3
            #define R_ATMOS 6471e3
            #define H_RAYLEIGH 8e3
            #define H_MIE 1.2e3
            #define SUN_I 22.0
            #define VIEW_STEPS 12
            #define LIGHT_STEPS 6
            static const float3 K_RAYLEIGH = float3(5.5e-6, 13.0e-6, 22.4e-6);
            static const float K_MIE = 21e-6;

            float2 RaySphere(float3 ro, float3 rd, float r)
            {
                float b = 2.0 * dot(rd, ro);
                float c = dot(ro, ro) - r * r;
                float d = b * b - 4.0 * c;
                if (d < 0.0) return float2(1e5, -1e5);
                d = sqrt(d);
                return float2((-b - d) * 0.5, (-b + d) * 0.5);
            }

            // Light scattered towards the eye along rd, from a light in direction l with intensity i.
            float3 Scatter(float3 rd, float3 l, float i, out float3 transmittance)
            {
                float3 ro = float3(0, R_PLANET + 30.0, 0);
                float2 p = RaySphere(ro, rd, R_ATMOS);
                float2 g = RaySphere(ro, rd, R_PLANET);
                float tMax = p.y;
                if (g.x > 0.0) tMax = min(tMax, g.x);
                float dt = tMax / VIEW_STEPS;
                float kMie = K_MIE * _Turbidity;

                float mu = dot(rd, l);
                float gg = _MieG * _MieG;
                float phaseR = 3.0 / (16.0 * PI) * (1.0 + mu * mu);
                float phaseM = 3.0 / (8.0 * PI) * ((1.0 - gg) * (mu * mu + 1.0)) / (pow(max(1e-4, 1.0 + gg - 2.0 * mu * _MieG), 1.5) * (2.0 + gg));

                float3 sumR = 0, sumM = 0;
                float odR = 0, odM = 0;
                for (int s = 0; s < VIEW_STEPS; s++)
                {
                    float3 pos = ro + rd * (dt * (s + 0.5));
                    float h = length(pos) - R_PLANET;
                    float dR = exp(-h / H_RAYLEIGH) * dt;
                    float dM = exp(-h / H_MIE) * dt;
                    odR += dR; odM += dM;

                    float lStep = RaySphere(pos, l, R_ATMOS).y / LIGHT_STEPS;
                    float lR = 0, lM = 0;
                    for (int j = 0; j < LIGHT_STEPS; j++)
                    {
                        float3 lp = pos + l * (lStep * (j + 0.5));
                        float lh = max(0.0, length(lp) - R_PLANET);
                        lR += exp(-lh / H_RAYLEIGH) * lStep;
                        lM += exp(-lh / H_MIE) * lStep;
                    }
                    float3 att = exp(-(kMie * (odM + lM) + K_RAYLEIGH * (odR + lR)));
                    sumR += dR * att;
                    sumM += dM * att;
                }
                transmittance = exp(-(kMie * odM + K_RAYLEIGH * odR));
                // Mie at 0.6: the forward glow at full strength washes a low sun out to white under ACES.
                return i * (phaseR * K_RAYLEIGH * sumR + 0.6 * phaseM * kMie * sumM);
            }

            // Colour of direct light arriving from direction l at sea level (white overhead, orange low down).
            float3 LightColour(float3 l)
            {
                float3 ro = float3(0, R_PLANET + 30.0, 0);
                float tMax = RaySphere(ro, l, R_ATMOS).y;
                float dt = tMax / LIGHT_STEPS;
                float odR = 0, odM = 0;
                for (int j = 0; j < LIGHT_STEPS; j++)
                {
                    float h = length(ro + l * (dt * (j + 0.5))) - R_PLANET;
                    odR += exp(-h / H_RAYLEIGH) * dt;
                    odM += exp(-h / H_MIE) * dt;
                }
                return exp(-(K_MIE * _Turbidity * odM + K_RAYLEIGH * odR));
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv, 0).r; }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 rd = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float3 moon = normalize(_MoonDir.xyz);
                float y = rd.y;
                // Looking below the horizon we only see past the edge of the sea: treat it as the horizon.
                float3 rdSky = normalize(float3(rd.x, max(y, 0.002), rd.z));

                float3 trans;
                float3 col = Scatter(rdSky, sun, SUN_I * _SunStrength, trans);
                float3 moonTrans;
                col += Scatter(rdSky, moon, SUN_I * 0.0035 * _MoonStrength, moonTrans) * float3(0.75, 0.85, 1.15);
                col += float3(0.0015, 0.002, 0.004) * (1.0 - saturate(sun.y * 4.0 + 0.5)); // faint airglow at night
                float3 skyCol = col;

                // Stars, dimmed by the air near the horizon and by any light in the sky.
                float3 g = rdSky * 170.0;
                float3 cell = floor(g);
                float h = Hash(cell);
                float star = step(0.9925, h) * smoothstep(0.32, 0.0, length(frac(g) - 0.5)) * (0.4 + 0.6 * Hash(cell + 7.1));
                col += star * _Stars * saturate(y * 4.0) * moonTrans * float3(0.9, 0.93, 1.0) * 0.6;

                // Sun and moon discs, seen through the air in front of them.
                float sd = dot(rdSky, sun);
                col += smoothstep(0.99988, 0.99993, sd) * trans * 30.0 * _SunStrength * saturate(sun.y * 20.0 + 1.0);
                float md = dot(rdSky, moon);
                col += smoothstep(0.99955, 0.9997, md) * moonTrans * float3(0.9, 0.92, 1.0) * 1.6 * _MoonStrength;

                // ---------- Clouds: a layer overhead, its texture drifting aft with the ship's way ----------
                if (y > 0.0 && _CloudCover > 0.001)
                {
                    float t = 1.0 / max(y, 0.04);                 // distance to a flat cloud deck at height 1
                    float2 uv = rd.xz * t * _CloudScale * 0.12 + _CloudWind.xy * _Time.y;
                    float n = Noise(uv) * 0.55 + Noise(uv * 2.3 + 0.37) * 0.3 + Noise(uv * 5.7 + 0.71) * 0.15;
                    float edge = 1.0 - _CloudCover;
                    float density = smoothstep(edge - 0.08, edge + 0.22, n);
                    // A step towards the light: thicker there means this side is in shadow.
                    float2 toLight = normalize(sun.xz + 1e-4) * 0.06;
                    float nl = Noise(uv + toLight) * 0.55 + Noise((uv + toLight) * 2.3 + 0.37) * 0.3 + Noise((uv + toLight) * 5.7 + 0.71) * 0.15;
                    float lit = saturate(1.0 - (nl - n) * 6.0 - density * _CloudDarkness * 0.6);

                    float3 sunLight = LightColour(sun) * SUN_I * 0.06 * _SunStrength * saturate(sun.y * 6.0 + 0.4);
                    float3 moonLight = float3(0.5, 0.6, 0.85) * 0.02 * _MoonStrength;
                    float3 skyLight = skyCol * 0.9 + moonLight; // the sky around them fills their shadows
                    float silver = pow(saturate(dot(rdSky, sun)), 12.0) * (1.0 - density) * 3.0;
                    float3 cloud = skyLight + sunLight * (lit * (0.6 + silver) + 0.15) + moonLight * lit;
                    cloud *= lerp(1.0, 1.0 - _CloudDarkness, density * (1.0 - lit));

                    // Far clouds thin out into the haze.
                    float fade = smoothstep(0.02, 0.22, y);
                    col = lerp(col, cloud, density * fade * 0.95);
                }

                col *= _Exposure;
                // The horizon fades into the fog the sea uses, so the two meet without a line.
                float haze = (1.0 - smoothstep(-0.02, 0.12, y)) * _HazeAmount;
                col = lerp(col, _HorizonColor.rgb, haze);
                col *= _SkyTint.rgb;
                col += (Hash(float3(i.positionCS.xy, 1.7)) - 0.5) / 255.0;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
