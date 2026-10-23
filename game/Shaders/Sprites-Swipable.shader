// Replacement for the game's Sprites-Swipable shader (see FlatEffect.cginc).
Shader "OwlchemyVR/Sprites Swipable"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _TintColor ("Tint", Color) = (1,1,1,1)
        _WireColor ("Wire color", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert_fx
            #pragma fragment frag_fx
            #pragma multi_compile_fog
            #include "FlatEffect.cginc"
            ENDCG
        }
    }
}
