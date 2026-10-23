// Simple unlit stand-in for the game's effect shaders (portals, holograms, wireframes, shockwaves), whose
// originals only exist as compiled DirectX programs: texture x colours + emission, blended per material.
#ifndef FLAT_EFFECT_INCLUDED
#define FLAT_EFFECT_INCLUDED
#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;
fixed4 _Color;
fixed4 _TintColor;
fixed4 _WireColor;
half4 _EmissionColor;

struct v2f_fx
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
    UNITY_FOG_COORDS(1)
};

v2f_fx vert_fx(appdata_full v)
{
    v2f_fx o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.color = v.color;
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 frag_fx(v2f_fx i) : SV_Target
{
    fixed4 c = tex2D(_MainTex, i.uv) * _Color * _TintColor * _WireColor * i.color;
    c.rgb += _EmissionColor.rgb;
    UNITY_APPLY_FOG(i.fogCoord, c);
    return c;
}
#endif
