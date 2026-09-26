Shader "CityFlow/City Inspection Surface"
{
    Properties
    {
        _BaseColor ("Surface tint", Color) = (0.055, 0.085, 0.115, 1)
        [HDR] _GridColor ("Grid emission", Color) = (0.095, 0.0425, 0.01, 1)
        _PanelSize ("Grid spacing [m]", Vector) = (3.2, 4, 0, 0)
        _GridWidth ("Grid width [m]", Float) = 0.04
        _Ground ("Use ground grid", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _GridColor;
            float4 _PanelSize;
            float _GridWidth;
            float _Ground;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        half4 Frag(Varyings input) : SV_Target
        {
            half3 normal = normalize(input.normalWS);
            float3 axis = abs(normal);
            // World coordinates support the original CityGML geometry without changing its UVs.
            float2 facePoint = (_Ground > 0.5 || axis.y > max(axis.x, axis.z)) ? input.positionWS.xz :
                axis.x > axis.z ? input.positionWS.zy : input.positionWS.xy;
            float2 panel = max(_PanelSize.xy, 0.1);
            float2 pixel = max(fwidth(facePoint), 0.001);
            float2 cell = facePoint / panel;
            float2 distance = min(frac(cell), 1 - frac(cell)) * panel;
            float2 grid = (1 - smoothstep(_GridWidth, _GridWidth + pixel, distance)) *
                saturate(panel / (pixel * 4) - 0.5);
            float detail = max(grid.x * 0.7, grid.y * 0.5);
            if (_Ground > 0.5)
            {
                float2 majorCell = facePoint / 50;
                float2 majorDistance = min(frac(majorCell), 1 - frac(majorCell)) * 50;
                float2 major = 1 - smoothstep(0.12, 0.12 + pixel, majorDistance);
                detail = max(detail * 0.5, max(major.x, major.y)) * 0.4;
            }
            else detail *= lerp(1, 0.35, axis.y);

            float pane = frac(sin(dot(floor(cell), float2(12.9898, 78.233))) * 43758.5453);
            Light light = GetMainLight();
            half illumination = 0.4 + 0.3 * saturate(dot(normal, light.direction));
            half fresnel = pow(1 - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS))), 4);
            half3 tint = _BaseColor.rgb * illumination * lerp(0.82 + pane * 0.18, 1, _Ground);
            tint += half3(0.012, 0.024, 0.038) * fresnel * (1 - _Ground);
            return half4(tint + _GridColor.rgb * detail, _BaseColor.a);
        }
        half4 DepthFragment(Varyings input) : SV_Target { return 0; }
        ENDHLSL

        Pass
        {
            Name "CitySurface"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFragment
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
