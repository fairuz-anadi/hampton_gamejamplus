// The Promised Port's sea. Owner: Anadi (look matched to Samprity's Port/Stylized).
// Rolling Gerstner swells moved in the vertex shader (calm against the hull, flat toward the horizon), streaming aft
// so the ship reads as under way; two layers of scrolling ripples on top; deep-to-crest colour, white caps on the
// wave tops, sky and reflection-probe reflections with a fresnel, a sharp sun/moon glint, main-light shadows from the
// ship, and fog. SeaScroll still drives _BaseMap_ST, which slides the ripples.
// URP 17 (Unity 6): Forward+ light loop, soft shadows, fog. Depth and depth-normal passes follow the waves.
Shader "Port/Sea"
{
    Properties
    {
        [Header(Colour)]
        _DeepColor ("Deep Water", Color) = (0.02, 0.11, 0.2, 1)
        _ShallowColor ("Crest Water", Color) = (0.07, 0.33, 0.42, 1)
        _FoamColor ("Foam", Color) = (0.92, 0.95, 0.97, 1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.92
        _SunGlint ("Sun Glint", Range(0,4)) = 1.6
        _Reflection ("Sky Reflection", Range(0,1.5)) = 0.75

        [Header(Swell)]
        _WaveA ("Wave A (dir x, dir z, steepness, wavelength)", Vector) = (0.3, 1, 0.22, 26)
        _WaveB ("Wave B", Vector) = (-0.6, 0.8, 0.18, 15)
        _WaveC ("Wave C", Vector) = (0.9, 0.35, 0.14, 9)
        _WaveD ("Wave D", Vector) = (-0.2, -1, 0.1, 5.5)
        _Height ("Swell Height", Range(0,2)) = 0.7
        _Flow ("Ship Speed (m/s, sea streams aft)", Float) = 4
        _HullRect ("Calm Zone round the Hull (xMin, zMin, xMax, zMax)", Vector) = (-6.8, -3, 6.8, 84)
        _CalmDistance ("Distance to full swell (m)", Float) = 14
        _FarFade ("Flatten from / to (m from ship)", Vector) = (160, 380, 0, 0)

        [Header(Ripples and foam)]
        [Normal] _BumpMap ("Ripple Normals", 2D) = "bump" {}
        _BaseMap ("Ripple Tiling (driven by SeaScroll)", 2D) = "white" {}
        _BumpScale ("Ripple Strength", Range(0,2)) = 0.55
        _FoamAmount ("White Caps", Range(0,1)) = 0.55
        _FoamHeight ("Cap Height (of the swell)", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor, _ShallowColor, _FoamColor;
            half _Smoothness, _SunGlint, _Reflection;
            float4 _WaveA, _WaveB, _WaveC, _WaveD;
            float _Height, _Flow;
            float4 _HullRect;
            float _CalmDistance;
            float4 _FarFade;
            float4 _BumpMap_ST, _BaseMap_ST;
            half _BumpScale, _FoamAmount, _FoamHeight;
        CBUFFER_END

        // One Gerstner wave: adds its displacement and the partial derivatives used for the normal.
        void Gerstner(float4 w, float2 p, float amp, inout float3 disp, inout float3 tangent, inout float3 binormal)
        {
            float k = 6.2831853 / w.w;
            float c = sqrt(9.8 / k);
            float2 d = normalize(w.xy);
            float a = w.z / k * amp;
            float f = k * (dot(d, p) - c * _Time.y);
            float s = sin(f), co = cos(f);
            disp += float3(d.x * a * co, a * s, d.y * a * co);
            tangent += float3(-d.x * d.x * w.z * amp * s, d.x * w.z * amp * co, -d.x * d.y * w.z * amp * s);
            binormal += float3(-d.x * d.y * w.z * amp * s, d.y * w.z * amp * co, -d.y * d.y * w.z * amp * s);
        }

        // How much swell a point gets: none against the hull, full past _CalmDistance, flat again far out.
        float SwellAmount(float3 wp)
        {
            float2 q = max(float2(_HullRect.x - wp.x, _HullRect.y - wp.z), float2(wp.x - _HullRect.z, wp.z - _HullRect.w));
            float toHull = length(max(q, 0.0));
            float near = smoothstep(0.0, _CalmDistance, toHull);
            float2 centre = float2((_HullRect.x + _HullRect.z) * 0.5, (_HullRect.y + _HullRect.w) * 0.5);
            float far = 1.0 - smoothstep(_FarFade.x, _FarFade.y, distance(wp.xz, centre));
            return near * far;
        }

        // Moves a world position by the swell; returns the normal and how high (0-1) the point sits on its wave.
        float3 Swell(float3 wp, out float3 normal, out float crest)
        {
            float amp = SwellAmount(wp) * _Height;
            float2 p = wp.xz + float2(0, _Flow * _Time.y); // the sea streams aft past a ship that never moves
            float3 disp = 0, t = float3(1, 0, 0), b = float3(0, 0, 1);
            Gerstner(_WaveA, p, amp, disp, t, b);
            Gerstner(_WaveB, p, amp, disp, t, b);
            Gerstner(_WaveC, p, amp, disp, t, b);
            Gerstner(_WaveD, p, amp, disp, t, b);
            normal = normalize(cross(b, t));
            float maxH = (_WaveA.z / (6.2831853 / _WaveA.w) + _WaveB.z / (6.2831853 / _WaveB.w)
                        + _WaveC.z / (6.2831853 / _WaveC.w) + _WaveD.z / (6.2831853 / _WaveD.w)) * max(amp, 1e-3);
            crest = saturate(disp.y / maxH * 0.5 + 0.5);
            return wp + disp;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float crest : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                float3 n; float crest;
                wp = Swell(wp, n, crest);
                o.positionWS = wp;
                o.normalWS = n;
                o.crest = crest;
                o.uv = v.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.positionCS = TransformWorldToHClip(wp);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 wp = i.positionWS;
                float3 V = normalize(GetWorldSpaceViewDir(wp));

                // Two ripple layers at different scales and drifts, laid over the swell's normal.
                half3 r1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 r2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv * 0.37 + float2(_Time.y * 0.011, -_Time.y * 0.007)), _BumpScale * 0.8);
                half3 ripple = normalize(half3(r1.xy + r2.xy, r1.z * r2.z));
                float3 N = normalize(i.normalWS + float3(ripple.x, 0, ripple.y));

                // Colour: deep in the troughs, lighter and greener up the crests and where the light passes through.
                half fresnel = pow(1.0 - saturate(dot(N, V)), 5.0);
                half3 water = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(i.crest * 1.2 - 0.1));

                InputData inputData = (InputData)0;
                inputData.positionWS = wp;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(wp);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0);

                Light mainLight = GetMainLight(inputData, shadowMask, ao);
                half lit = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 ambient = SampleSH(N);
                half3 color = water * (ambient + mainLight.color * saturate(dot(N, mainLight.direction)) * 0.35 * lit);

                // White caps: thin and patchy, only on the very tops of the swells, and only in some patches of sea
                // (a slow, large noise from the ripple map), broken up again by the fine ripples.
                half patches = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, wp.xz * 0.006 + float2(0, _Flow * _Time.y * 0.006)).r;
                half capNoise = saturate(r1.x * r2.y * 6.0 + 0.5);
                half foam = smoothstep(_FoamHeight, _FoamHeight + 0.08, i.crest + (capNoise - 0.5) * 0.18);
                foam *= smoothstep(0.45, 0.75, patches) * _FoamAmount * SwellAmount(wp);
                color = lerp(color, _FoamColor.rgb * (ambient + mainLight.color * 0.6 * lit), foam);

                // The sky and the reflection probe, stronger at grazing angles; the sun or moon as a sharp glint.
                half3 env = GlossyEnvironmentReflection(reflect(-V, N), 1.0 - _Smoothness, 1.0);
                color += env * lerp(0.04, 1.0, fresnel) * _Reflection * (1.0 - foam);
                float3 H = normalize(mainLight.direction + V);
                half glint = pow(saturate(dot(N, H)), 380.0) * _SunGlint * lit;
                color += mainLight.color * glint * (1.0 - foam);

            #if defined(_ADDITIONAL_LIGHTS)
                // Lamplight and portholes on the water close to the hull.
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
                    color += water * light.color * light.distanceAttenuation * light.shadowAttenuation * saturate(dot(N, light.direction)) * 0.6;
                LIGHT_LOOP_END
            #endif

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct A { float4 positionOS : POSITION; };
            struct VO { float4 positionCS : SV_POSITION; };
            VO DepthVert(A v)
            {
                VO o;
                float3 n; float crest;
                o.positionCS = TransformWorldToHClip(Swell(TransformObjectToWorld(v.positionOS.xyz), n, crest));
                return o;
            }
            half DepthFrag(VO i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DNVert
            #pragma fragment DNFrag
            struct A { float4 positionOS : POSITION; };
            struct VO { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            VO DNVert(A v)
            {
                VO o;
                float3 n; float crest;
                o.positionCS = TransformWorldToHClip(Swell(TransformObjectToWorld(v.positionOS.xyz), n, crest));
                o.normalWS = n;
                return o;
            }
            half4 DNFrag(VO i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
