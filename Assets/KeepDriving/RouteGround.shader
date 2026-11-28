Shader "KeepDriving/Route Ground"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Base Color", 2D) = "white" {}
        [NoScaleOffset] [Normal] _NormalMap ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap ("Roughness", 2D) = "gray" {}
        [NoScaleOffset] _HeightMap ("Height", 2D) = "gray" {}
        _RepeatMeters ("Texture Repeat (m)", Float) = 2.5
        _ParallaxMeters ("Parallax Depth (m)", Range(0, 0.06)) = 0.025
        _EdgeFeather ("Edge Feather", Range(0.01, 0.25)) = 0.1
        [Toggle] _WheelTracks ("Split Wheel Tracks", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        Pass
        {
            Name "Route Forward"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            TEXTURE2D(_RoughnessMap);
            TEXTURE2D(_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                float _RepeatMeters;
                float _ParallaxMeters;
                float _EdgeFeather;
                float _WheelTracks;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 route : TEXCOORD0; // Map east/north in meters, then fraction across the ribbon.
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 route : TEXCOORD2;
                half fog : TEXCOORD3;
                half3 vertexLights : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.route = input.route;
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.vertexLights = VertexLighting(position.positionWS, output.normalWS);
                output.shadowCoord = GetShadowCoord(position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 meters = input.route.xy;
                float across = input.route.z;
                float edge = min(across, 1.0 - across);
                float edgeNoise = sin(meters.x * 1.29 + meters.y * 0.71) * sin(meters.y * 1.43 - meters.x * 0.54);
                float feather = _EdgeFeather * (1.0 + edgeNoise * 0.25);
                float coverage = smoothstep(0.0, feather, edge);
                float centerGap = 0.11 + edgeNoise * 0.025;
                coverage *= lerp(1.0, smoothstep(centerGap - 0.035, centerGap + 0.035, abs(across - 0.5)), _WheelTracks);
                float dither = frac(52.9829189 * frac(dot(floor(input.positionCS.xy), float2(0.06711056, 0.00583715))));
                clip(coverage - max(dither, 0.001));

                half3 normal = normalize(input.normalWS);
                half3 tangent = normalize(half3(normal.y, -normal.x, 0.0));
                half3 bitangent = cross(tangent, normal);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float repeatMeters = max(_RepeatMeters, 0.01);
                float2 uv = meters / repeatMeters;
                float uvFootprint = max(length(ddx(uv)), length(ddy(uv)));
                float parallaxFade = 1.0 - smoothstep(0.04, 0.2, uvFootprint);
                float height = SAMPLE_TEXTURE2D(_HeightMap, sampler_BaseMap, uv).r - 0.5;
                float2 viewAlongSurface = float2(dot(view, tangent), dot(view, bitangent));
                float viewAboveSurface = max(dot(view, normal), 0.28);
                uv += height * (_ParallaxMeters / repeatMeters) * parallaxFade * viewAlongSurface / viewAboveSurface;

                SurfaceData surface = (SurfaceData)0;
                half variation = 1.0 + 0.07 * sin(meters.x * 0.16 + meters.y * 0.07) * sin(meters.y * 0.11 - meters.x * 0.09);
                surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * variation;
                surface.normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, uv));
                surface.smoothness = 1.0 - saturate(SAMPLE_TEXTURE2D(_RoughnessMap, sampler_BaseMap, uv).r);
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normalize(TransformTangentToWorld(surface.normalTS, half3x3(tangent, bitangent, normal)));
                lighting.viewDirectionWS = view;
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                        lighting.shadowCoord = input.shadowCoord;
                    #else
                        lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    #endif
                #endif
                lighting.vertexLighting = input.vertexLights;
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.shadowMask = unity_ProbesOcclusion;
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, input.fog);
                return color;
            }
            ENDHLSL
        }
    }
}
