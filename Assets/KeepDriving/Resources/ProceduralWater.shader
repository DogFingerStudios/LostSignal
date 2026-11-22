Shader "KeepDriving/Procedural Water"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Water Surface"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.worldPosition = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.worldPosition);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.worldPosition.xz;
                float time = _Time.y;
                float bend = sin(p.y * .13 + p.x * .025);
                float wave = sin(p.x * .31 + p.y * .19 - time * 1.6 + bend * 1.4);
                float crossWave = sin(p.x * .57 - p.y * .29 - time * 1.2);
                float ripple = smoothstep(.56, .97, wave * .62 + crossWave * .38);
                float3 view = normalize(GetWorldSpaceViewDir(input.worldPosition));
                float grazing = pow(saturate(1.0 - abs(view.y)), 3.0);
                half3 blue = lerp(half3(.035, .19, .24), half3(.14, .34, .39), grazing);
                return half4(blue + ripple * half3(.035, .075, .08), 1.0);
            }
            ENDHLSL
        }
    }
}
