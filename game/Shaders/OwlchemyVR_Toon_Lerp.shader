// Replacement for the game's "OwlchemyVR/Toon (Lerp)" shader (see FlatToon.cginc).
Shader "OwlchemyVR/Toon (Lerp)"
{
    Properties
    {
        _Color ("Albedo Color", Color) = (1,1,1,1)
        _MainTex ("Albedo Map", 2D) = "white" {}
        _SecondaryTex ("Secondary", 2D) = "white" {}
        _LerpFactor ("Lerp", Range(0,1)) = 0
        _Glossiness ("Glossiness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _MetallicGlossMap ("Metallic Gloss Map", 2D) = "white" {}
        g_flCubeMapScalar ("Reflection Strength", Range(0,2)) = 1
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _ZOffsetScale ("Z Offset Scale", Float) = 0
        _ZOffsetBias ("Z Offset Bias", Float) = 0
        [Enum(Back, 2, Front, 1, Off, 0)] _CullMode ("Cull Mode", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend Off
            ZWrite On
            Cull [_CullMode]
            Offset [_ZOffsetScale], [_ZOffsetBias]
            CGPROGRAM
            #pragma vertex vert_toon
            #pragma fragment frag_toon
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile __ FLAT_VERTEX_LIGHTING
            #define FLAT_LERP 1
            #include "FlatToon.cginc"
            ENDCG
        }
    }
}
