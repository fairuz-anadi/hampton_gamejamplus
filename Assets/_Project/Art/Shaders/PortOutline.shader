// Selective outline: an inverted hull drawn behind the object. Owner: Samprity.
// Added as an extra material on characters (thin, dark warm brown) and on interactable stations (thicker gold glow
// when a player is close, driven by InteractHighlight). Width stays about the same on screen at any distance.
// _CenterExtrude pushes the hull out from the object's centre instead of along normals, so boxy props
// (hard edges, split normals) get a closed outline.
Shader "Port/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Colour", Color) = (0.12,0.08,0.06,1)
        _OutlineWidth ("Width (screen-ish)", Range(0,5)) = 1
        _CenterExtrude ("Extrude From Centre (boxy props)", Range(0,1)) = 0
        _Center ("Centre (object space)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _CenterExtrude;
                float4 _Center;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 dirOS = normalize(lerp(v.normalOS, v.positionOS.xyz - _Center.xyz + 1e-5, _CenterExtrude));
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 dirWS = normalize(TransformObjectToWorldNormal(dirOS));
                // Scale with distance so the line keeps its thickness on screen.
                float dist = length(GetCameraPositionWS() - positionWS);
                positionWS += dirWS * _OutlineWidth * dist * 0.0018;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                if (_OutlineWidth <= 0.001) discard;
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }
}
