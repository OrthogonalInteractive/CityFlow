Shader "CityFlow/City Inspection Contour"
{
    Properties
    {
        [HDR] _BaseColor ("Contour emission", Color) = (3.2, 1.35, 0.25, 1)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+10" }
        Pass
        {
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }
            half4 Frag() : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
