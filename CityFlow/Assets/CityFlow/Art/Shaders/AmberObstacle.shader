Shader "CityFlow/Amber Obstacle"
{
    Properties
    {
        _BaseColor ("Glass tint", Color) = (0.055, 0.085, 0.115, 1)
        [HDR] _EdgeColor ("Amber frame emission", Color) = (3.2, 1.35, 0.25, 1)
        [HDR] _GridColor ("Facade grid emission", Color) = (0.095, 0.0425, 0.01, 1)
        _EdgeWidth ("Frame width [m]", Range(0.01, 0.2)) = 0.065
        _GridWidth ("Grid width [m]", Range(0.005, 0.06)) = 0.012
        _PanelSize ("Panel width / height [m]", Vector) = (0.9, 1.6, 0, 0)
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "DisableBatching" = "True" }
        Cull Back
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EdgeColor;
            half4 _GridColor;
            float4 _PanelSize;
            float _EdgeWidth;
            float _GridWidth;
            float _Surface;
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
            float4 face : TEXCOORD2;
            float fog : TEXCOORD3;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            // Unity's unit cube stays inside the authored Bounds; all decoration is on its faces.
            float3 dimensions = float3(length(unity_ObjectToWorld._m00_m10_m20),
                length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
            float3 facePoint = (input.positionOS.xyz + 0.5) * dimensions;
            float3 normal = abs(input.normalOS);
            output.face = normal.y > 0.5 ? float4(facePoint.xz, dimensions.xz) :
                normal.x > 0.5 ? float4(facePoint.zy, dimensions.zy) : float4(facePoint.xy, dimensions.xy);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float2 facePoint = input.face.xy;
            float2 size = input.face.zw;
            float2 pixel = max(fwidth(facePoint), 0.001);
            float2 border = min(facePoint, size - facePoint);
            float edgeDistance = min(border.x, border.y);
            float edgePixel = border.x < border.y ? pixel.x : pixel.y;
            float frame = 1 - smoothstep(_EdgeWidth, _EdgeWidth + edgePixel, edgeDistance);
            float halo = exp(-max(0, edgeDistance) / max(_EdgeWidth * 4, 0.001)) * 0.075;

            float2 panel = max(_PanelSize.xy, 0.1);
            float2 cell = facePoint / panel;
            float2 gridDistance = min(frac(cell), 1 - frac(cell)) * panel;
            // Derivative filtering keeps the thin grid stable in both Overview and Node 360.
            float2 grid = 1 - smoothstep(_GridWidth, _GridWidth + pixel, gridDistance);
            float2 fade = saturate(panel / (pixel * 4) - 0.5);
            grid *= fade;
            float2 index = floor(cell);
            float2 origin = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m23);
            float pane = frac(sin(dot(index + origin, float2(12.9898, 78.233))) * 43758.5453);
            float major = 1 - smoothstep(_GridWidth * 1.8, _GridWidth * 1.8 + pixel.y,
                min(frac(cell.y / 4), 1 - frac(cell.y / 4)) * panel.y * 4);
            float detail = max(grid.x * 0.7, grid.y * 0.42) + major * 0.45;
            detail *= lerp(1, 0.3, abs(input.normalWS.y));
            float pinLight = grid.x * grid.y * step(0.95, pane) * 0.1;

            half3 normal = normalize(input.normalWS);
            half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
            Light light = GetMainLight();
            half illumination = 0.4 + 0.3 * saturate(dot(normal, light.direction));
            half fresnel = pow(1 - saturate(dot(normal, view)), 4);
            half3 glass = _BaseColor.rgb * illumination * (0.78 + pane * 0.22);
            glass += half3(0.012, 0.024, 0.038) * fresnel;
            half3 color = glass + _EdgeColor.rgb * (frame + halo + pinLight) + _GridColor.rgb * detail;
            return half4(MixFog(color, input.fog), _BaseColor.a);
        }
        half4 DepthFragment(Varyings input) : SV_Target { return 0; }
        ENDHLSL

        Pass
        {
            Name "AmberGlass"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
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
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 packed = saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5);
                    return half4(PackFloat2To888(packed), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - output.positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
