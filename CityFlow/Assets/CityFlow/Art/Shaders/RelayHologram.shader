Shader "CityFlow/Relay Hologram"
{
    Properties
    {
        _BaseColor ("Projection Tint", Color) = (0.36, 0.72, 0.82, 0.18)
        [HDR] _EdgeColor ("Projection Lines", Color) = (1.0, 2.1, 2.4, 0.65)
        _TopOpacity ("Opacity at Ceiling", Range(0, 1)) = 0.45
        _BandSpacing ("Height Band Spacing (m)", Float) = 1
        _LineWidth ("Line Width (m)", Float) = 0.035
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "RelayProjection"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _TopOpacity;
                float _BandSpacing;
                float _LineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 localMeters : TEXCOORD2;
                float4 shape : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                // The unit cylinder spans Y -1..1; bands are measured upward from the Node, in meters.
                output.localMeters = (input.positionOS.xyz + float3(0, 1, 0)) * scale;
                output.shape = float4(saturate(input.positionOS.y * 0.5 + 0.5),
                    abs(input.normalOS.y), scale.x * 0.5, scale.y * 2);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = input.localMeters.y;
                float fade = lerp(1, _TopOpacity, smoothstep(0.15, 1, input.shape.x));
                float pixel = max(fwidth(height), 0.001);
                float spacing = max(_BandSpacing, 0.1);
                float bandDistance = abs(frac(height / spacing + 0.5) - 0.5) * spacing;
                float band = 1 - smoothstep(_LineWidth, _LineWidth + pixel, bandDistance);
                band *= saturate(spacing / (pixel * 4) - 0.5);
                float endDistance = min(height, input.shape.w - height);
                float ends = 1 - smoothstep(_LineWidth, _LineWidth + pixel, endDistance);
                float railDistance = min(abs(input.localMeters.x), abs(input.localMeters.z));
                float rail = 1 - smoothstep(_LineWidth, _LineWidth + max(fwidth(railDistance), 0.001), railDistance);
                float radius = length(input.localMeters.xz);
                float capRim = 1 - smoothstep(_LineWidth, _LineWidth + max(fwidth(radius), 0.001), input.shape.z - radius);
                float isCap = step(0.5, input.shape.y);
                float structure = lerp(max(ends, max(band * 0.45, rail * 0.35)), capRim, isCap);
                float fresnel = pow(1 - saturate(abs(dot(normalize(input.normalWS),
                    GetWorldSpaceNormalizeViewDir(input.positionWS)))), 3) * (1 - isCap);
                half3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, max(structure, fresnel * 0.45));
                float alpha = (_BaseColor.a * lerp(0.65 + fresnel, 0.35, isCap) + structure * _EdgeColor.a) * fade;
                return half4(MixFog(color, input.fog), saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
