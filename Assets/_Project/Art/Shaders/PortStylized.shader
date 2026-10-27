// The Promised Port's surface shader. Owner: Samprity.
// Painterly stylized PBR-ish shading: soft toon light (blend between smooth Lambert and a soft ramp), cool-tinted
// shadows, controlled specular for brass and iron, subtle rim, and world-space painterly variation that works on
// atlas-textured models (KayKit) as well as on plain colours:
//   colour blotches + fine grain, per-tile/plank tint, wood grain streaks, dirt gathering near the floor,
//   rust/patina patches, a soft dusty top light, boots wearing a path down the middle of the deck, and an optional
//   per-piece tint from vertex colours (generated panels and boards).
// URP 17 (Unity 6): Forward+ light loop, soft shadows, SSAO, fog. Shadow, depth and depth-normal passes included.
Shader "Port/Stylized"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0

        [Header(Soft toon)]
        _ToonAmount ("Toon Amount (0 = smooth, 1 = banded)", Range(0,1)) = 0.3
        _ToonCenter ("Light/Shadow Edge", Range(0,1)) = 0.5
        _ToonSoftness ("Edge Softness", Range(0.01,0.5)) = 0.14
        _ShadowTint ("Shadow Tint", Color) = (0.42,0.47,0.62,1)
        _SpecStrength ("Highlight Strength", Range(0,2)) = 0.6
        _RimStrength ("Rim Light", Range(0,1)) = 0.12

        [Header(Painterly variation)]
        _NoiseTex ("Painterly Noise (R blotch, G grain, B streak, A patches)", 2D) = "gray" {}
        _NoiseScale ("Noise Scale (per metre)", Float) = 0.35
        _ColorVariation ("Colour Variation", Range(0,0.5)) = 0.14
        _Grain ("Fine Grain", Range(0,0.6)) = 0.18
        _CellSize ("Tile/Plank Size (m)", Vector) = (1,1,1,0)
        _CellVariation ("Tile/Plank Tint Variation", Range(0,0.3)) = 0.05
        _WoodGrain ("Wood Grain", Range(0,0.6)) = 0
        _GrainStretch ("Grain Stretch (along, across)", Vector) = (0.15,1.6,0,0)
        _TopHighlight ("Dusty Top Light", Range(0,0.4)) = 0.06

        [Header(Wear)]
        _DirtColor ("Dirt Colour", Color) = (0.42,0.34,0.27,1)
        _DirtAmount ("Dirt Near Floor", Range(0,1)) = 0.35
        _DirtHeight ("Dirt Height (m)", Float) = 0.55
        _FloorY ("Floor Height (world Y)", Float) = 0.05
        _RustColor ("Rust / Patina Colour", Color) = (0.45,0.24,0.12,1)
        _RustAmount ("Rust / Patina", Range(0,1)) = 0
        _EdgeWear ("Edge Wear (lighter worn patches)", Range(0,0.5)) = 0.08
        _PathWear ("Worn Walkway (floors)", Range(0,1)) = 0
        _PathHalfWidth ("Walkway Half Width (m, around x = 0)", Float) = 1.6
        _VertexTint ("Per-Piece Tint From Vertex Colour", Range(0,1)) = 0

        [HideInInspector] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale, _Smoothness, _Metallic;
            half _ToonAmount, _ToonCenter, _ToonSoftness;
            half4 _ShadowTint;
            half _SpecStrength, _RimStrength;
            float _NoiseScale;
            half _ColorVariation, _Grain;
            float4 _CellSize;
            half _CellVariation, _WoodGrain;
            float4 _GrainStretch;
            half _TopHighlight;
            half4 _DirtColor;
            half _DirtAmount;
            float _DirtHeight, _FloorY;
            half4 _RustColor;
            half _RustAmount, _EdgeWear;
            half _PathWear;
            float _PathHalfWidth;
            half _VertexTint;
            half _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 tint : TEXCOORD5;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.tint = lerp(half3(1, 1, 1), v.color.rgb, _VertexTint);
                return o;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Triplanar lookup of the painterly noise, blended by the surface normal.
            half4 Tri(float3 wp, half3 n, float scale)
            {
                half3 w = pow(abs(n), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                half4 x = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wp.zy * scale);
                half4 y = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wp.xz * scale);
                half4 z = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wp.xy * scale);
                return x * w.x + y * w.y + z * w.z;
            }

            // Wood grain: the streak channel stretched along the board.
            half Streak(float3 wp, half3 n, float2 stretch, float offset)
            {
                half3 w = pow(abs(n), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                half x = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(wp.z * stretch.x, wp.y * stretch.y) + offset).b;
                half y = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(wp.z * stretch.x, wp.x * stretch.y) + offset).b;
                half z = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(wp.x * stretch.x, wp.y * stretch.y) + offset).b;
                return x * w.x + y * w.y + z * w.z;
            }

            // Soft toon response for one light. lightAmount comes back for the specular and shadow tint.
            half3 ShadeLight(Light light, half3 albedo, half3 N, half3 V, half smooth, half metallic, half tintShadows)
            {
                half ndl = dot(N, light.direction);
                half lambert = saturate(ndl);
                half ramp = smoothstep(_ToonCenter - _ToonSoftness, _ToonCenter + _ToonSoftness, ndl * 0.5 + 0.5);
                half shape = lerp(lambert, ramp, _ToonAmount);
                half lightAmount = shape * light.shadowAttenuation;
                half3 radiance = light.color * light.distanceAttenuation;

                // Shadowed sides keep a cool tint instead of going grey (main light only).
                half3 shadowFill = _ShadowTint.rgb * 0.22 * tintShadows * (1.0 - lightAmount);
                half3 diffuse = albedo * radiance * (lightAmount + shadowFill);

                half3 H = SafeNormalize(light.direction + V);
                half spec = pow(saturate(dot(N, H)), exp2(smooth * 9.0 + 2.0));
                spec = smoothstep(0.02, 0.55, spec) * smooth;
                half3 specColor = lerp(half3(0.05, 0.05, 0.05), albedo * 1.2, metallic);
                half3 specular = specColor * spec * radiance * lightAmount * _SpecStrength;
                return diffuse + specular;
            }

            half4 Frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                if (!frontFace) N = -N;
            #if defined(_NORMALMAP)
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 T = normalize(i.tangentWS.xyz);
                half3 B = cross(N, T) * i.tangentWS.w;
                N = normalize(mul(nTS, half3x3(T, B, N)));
            #endif
                float3 wp = i.positionWS;
                half3 V = SafeNormalize(GetWorldSpaceViewDir(wp));

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * i.tint;
                half smooth = _Smoothness;
                half metallic = _Metallic;

                // ---- Painterly variation ----
                half4 nb = Tri(wp, N, _NoiseScale);
                half4 nf = Tri(wp, N, _NoiseScale * 7.0);
                float cellHash = Hash31(floor(wp / max(_CellSize.xyz, 0.01)) + 0.5);

                albedo *= 1.0 + (nb.r - 0.5) * 2.0 * _ColorVariation;
                // A slight warm/cool drift, like paint mixed by hand.
                albedo *= lerp(half3(0.96, 0.99, 1.05), half3(1.05, 1.0, 0.94), nb.g * 0.5 + nb.r * 0.5) * _ColorVariation * 2.0 + (1.0 - _ColorVariation * 2.0);
                albedo *= 1.0 + (nf.g - 0.5) * _Grain;
                albedo *= 1.0 + (cellHash - 0.5) * 2.0 * _CellVariation;
                if (_WoodGrain > 0.001)
                {
                    half streak = Streak(wp, N, _GrainStretch.xy, cellHash * 13.0);
                    albedo *= 1.0 + (streak - 0.5) * 2.0 * _WoodGrain;
                }

                // Worn, lighter patches where hands and boots rub.
                half worn = smoothstep(0.66, 0.82, nf.a) * _EdgeWear;
                albedo = lerp(albedo, albedo * 1.35 + 0.03, worn);

                // Boots polish a walkway down the middle of the deck: paler, a touch smoother, ragged at the edges.
                if (_PathWear > 0.001)
                {
                    half up = smoothstep(0.7, 0.95, N.y);
                    half lane = 1.0 - smoothstep(_PathHalfWidth * 0.45, _PathHalfWidth, abs(wp.x) + (nb.g - 0.5) * 0.9);
                    half path = up * lane * saturate(0.55 + (nf.b - 0.5) * 1.4) * _PathWear;
                    half luma = dot(albedo, half3(0.3, 0.59, 0.11));
                    albedo = lerp(albedo, lerp(albedo, luma.xxx, 0.3) * 1.18 + 0.015, path);
                    smooth = lerp(smooth, smooth + 0.12, path);
                }

                // Dirt gathers near the floor and in the low corners.
                half dirt = saturate(1.0 - (wp.y - _FloorY) / max(_DirtHeight, 0.01));
                dirt = dirt * dirt * saturate(0.35 + nb.b);
                albedo = lerp(albedo, albedo * _DirtColor.rgb, dirt * _DirtAmount);

                // Rust on iron, verdigris on brass: patches from the large noise, broken up by the fine.
                half rust = smoothstep(0.58, 0.78, nb.a * 0.7 + nf.r * 0.3) * _RustAmount;
                albedo = lerp(albedo, _RustColor.rgb * (0.75 + 0.5 * nf.g), rust);
                smooth *= 1.0 - rust * 0.85;
                metallic *= 1.0 - rust;

                albedo *= 1.0 + saturate(N.y) * _TopHighlight;
                albedo = saturate(albedo);

                // ---- Lighting ----
                InputData inputData = (InputData)0;
                inputData.positionWS = wp;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(wp);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                half4 shadowMask = half4(1, 1, 1, 1);
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0);

                half3 diffuseAlbedo = albedo * (1.0 - metallic * 0.45);
                half3 ambient = SampleSH(N) * ao.indirectAmbientOcclusion;
                half3 color = diffuseAlbedo * ambient;

                // Reflections of the sky and the reflection probe, so brass and iron catch light like metal.
                half perceptualRoughness = 1.0 - smooth;
                half fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
                half3 envSpec = lerp(half3(0.04, 0.04, 0.04), albedo, metallic);
                half3 env = GlossyEnvironmentReflection(reflect(-V, N), perceptualRoughness, ao.indirectAmbientOcclusion);
                color += env * lerp(envSpec, half3(1, 1, 1) * 0.25 * smooth, fresnel) * smooth * _SpecStrength;

                Light mainLight = GetMainLight(inputData, shadowMask, ao);
                color += ShadeLight(mainLight, diffuseAlbedo, N, V, smooth, metallic, 1.0);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
                    color += ShadeLight(light, diffuseAlbedo, N, V, smooth, metallic, 0.0);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
                    color += ShadeLight(light, diffuseAlbedo, N, V, smooth, metallic, 0.0);
                LIGHT_LOOP_END
            #endif

                // A soft rim from the sky so silhouettes read against dark rooms.
                half rim = pow(1.0 - saturate(dot(N, V)), 4.0) * _RimStrength;
                color += rim * SampleSH(half3(0, 1, 0)) * albedo * 1.5;

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };

            Varyings DNVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 DNFrag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(n);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                return half4(packedNormalWS, 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
