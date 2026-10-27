// The game's sky. Owner: Samprity.
// A painted gradient (zenith to horizon, horizon to sea-coloured ground) with a sun disc, a moon and stars, all driven
// by PartMood from the day-night clock. Replaces Unity's procedural sky, which only knows one light: at night it
// treated the moon (URP's main light then) as the sun and drew a dim daytime sky.
// The horizon colour is the fog colour, so the sea fades into the sky without a seam.
Shader "Port/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.33,0.53,0.8,1)
        _HorizonColor ("Horizon (= fog)", Color) = (0.74,0.83,0.92,1)
        _GroundColor ("Below Horizon", Color) = (0.2,0.3,0.4,1)
        _HorizonBlend ("Horizon Spread (lower = wider haze)", Range(0.1,2)) = 0.45
        _SunDir ("Sun Direction (towards the sun)", Vector) = (0,0.6,0.8,0)
        [HDR] _SunColor ("Sun Colour (0 = hidden)", Color) = (1,0.95,0.85,1)
        _MoonDir ("Moon Direction (towards the moon)", Vector) = (0,0.7,-0.7,0)
        [HDR] _MoonColor ("Moon Colour (0 = hidden)", Color) = (0,0,0,1)
        _Stars ("Stars", Range(0,1)) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor;
                half _HorizonBlend;
                float4 _SunDir;
                half4 _SunColor;
                float4 _MoonDir;
                half4 _MoonColor;
                half _Stars;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                // Gradient: a wide soft haze at the horizon, deeper colour overhead.
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(y), _HorizonBlend));
                if (y < 0.0) col = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-y * 4.0));
                half above = smoothstep(-0.02, 0.02, y);

                // Sun: a soft disc with a warm halo around it.
                float sd = dot(d, normalize(_SunDir.xyz));
                col += _SunColor.rgb * (smoothstep(0.9990, 0.9995, sd) * 3.0 + pow(saturate(sd), 48.0) * 0.45 + pow(saturate(sd), 5.0) * 0.12) * above;

                // Moon: a small cool disc with a faint glow.
                float md = dot(d, normalize(_MoonDir.xyz));
                col += _MoonColor.rgb * (smoothstep(0.99935, 0.9997, md) * 1.6 + pow(saturate(md), 160.0) * 0.25) * above;

                // Stars: sparse points on a grid of directions, fading out near the horizon haze.
                float3 g = d * 170.0;
                float3 cell = floor(g);
                float h = Hash(cell);
                float star = step(0.9925, h) * smoothstep(0.32, 0.0, length(frac(g) - 0.5)) * (0.4 + 0.6 * Hash(cell + 7.1));
                col += star * _Stars * saturate(y * 4.0) * half3(0.9, 0.93, 1.0);

                // A touch of dither so the slow gradients don't band.
                col += (Hash(float3(i.positionCS.xy, 1.7)) - 0.5) / 255.0;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
