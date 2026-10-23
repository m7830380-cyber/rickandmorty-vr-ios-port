// Shared code for the replacement OwlchemyVR toon shaders (the originals only exist as compiled DirectX
// programs; their property blocks survived, so these take the same properties). Lighting reads the per-light
// arrays the game's Valve Lab renderer (ValveCamera) fills every frame, so scenes are lit by their own lights;
// ambient comes from RenderSettings and reflections from the scene's reflection probes, like the original.
#ifndef FLAT_TOON_INCLUDED
#define FLAT_TOON_INCLUDED

#include "UnityCG.cginc"

#define FLAT_MAX_LIGHTS 18

int g_nNumLights;
float4 g_vLightColor[FLAT_MAX_LIGHTS];
float4 g_vLightPosition_flInvRadius[FLAT_MAX_LIGHTS];
float4 g_vLightDirection[FLAT_MAX_LIGHTS];
float4 g_vLightFalloffParams[FLAT_MAX_LIGHTS];
float4 g_vSpotLightInnerOuterConeCosines[FLAT_MAX_LIGHTS];

sampler2D _MainTex;
float4 _MainTex_ST;
sampler2D _SecondaryTex;
half _LerpFactor;
fixed4 _Color;
half _Glossiness;
half _Metallic;
sampler2D _MetallicGlossMap;
half g_flCubeMapScalar;
sampler2D _OcclusionMap;
half _OcclusionStrength;
sampler2D _EmissionMap;
half4 _EmissionColor;
half _AlphaCutoff;
half _BlendMode;

struct appdata_toon
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct v2f_toon
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 posWs : TEXCOORD1;
    half3 normalWs : TEXCOORD2;
    half3 vertexDiffuse : TEXCOORD3;
    half3 vertexSpecular : TEXCOORD4;
    UNITY_FOG_COORDS(5)
};

// Diffuse and specular from every active Valve light (directional, point, spot; area treated as point).
void FlatValveLights(float3 posWs, half3 n, half3 v, half gloss, out half3 diffuse, out half3 specular)
{
    diffuse = 0;
    specular = 0;
    half specPower = exp2(10.0 * gloss + 1.0);
    half specNorm = (specPower + 8.0) * (1.0 / 25.0);
    for (int i = 0; i < FLAT_MAX_LIGHTS; i++)
    {
        if (i >= g_nNumLights)
            break;
        float4 posInvRadius = g_vLightPosition_flInvRadius[i];
        float3 toLight = posInvRadius.xyz - posWs;
        float distSq = dot(toLight, toLight);
        if (distSq > g_vLightFalloffParams[i].z)
            continue;
        float3 l = toLight * rsqrt(max(distSq, 1e-8));
        half atten = 1;
        if (posInvRadius.w > 0)
        {
            float t = saturate(distSq * posInvRadius.w * posInvRadius.w);
            atten = (1 - t) * (1 - t) / (1 + 8 * t);
        }
        else if (posInvRadius.w == 0)
        {
            atten = 1 / (1 + distSq);
        }
        half cone = dot(l, -g_vLightDirection[i].xyz) - g_vSpotLightInnerOuterConeCosines[i].y;
        if (cone <= 0)
            continue;
        atten *= saturate(cone * g_vSpotLightInnerOuterConeCosines[i].z);

        half ndl = saturate(dot(n, l));
        half3 c = g_vLightColor[i].rgb * atten * ndl;
        diffuse += c;
        half3 h = normalize(l + v);
        specular += c * pow(saturate(dot(n, h)), specPower) * specNorm;
    }
}

v2f_toon vert_toon(appdata_toon v)
{
    v2f_toon o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.posWs = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.normalWs = UnityObjectToWorldNormal(v.normal);
    o.vertexDiffuse = 0;
    o.vertexSpecular = 0;
#if defined(FLAT_VERTEX_LIGHTING) && !defined(FLAT_UNLIT)
    FlatValveLights(o.posWs, normalize(o.normalWs), normalize(_WorldSpaceCameraPos - o.posWs), _Glossiness, o.vertexDiffuse, o.vertexSpecular);
#endif
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 frag_toon(v2f_toon i, fixed facing : VFACE) : SV_Target
{
    fixed4 albedo = tex2D(_MainTex, i.uv);
#if defined(FLAT_LERP)
    albedo = lerp(albedo, tex2D(_SecondaryTex, i.uv), _LerpFactor);
#endif
    albedo *= _Color;
#if defined(FLAT_CUTOUT)
    clip(albedo.a - _AlphaCutoff - 0.002);
#endif
#if defined(FLAT_UNLIT)
    fixed4 col = albedo;
#else
    half4 mg = tex2D(_MetallicGlossMap, i.uv);
    half metallic = _Metallic * mg.r;
    half gloss = _Glossiness * mg.a;
    half occlusion = lerp(1, tex2D(_OcclusionMap, i.uv).g, _OcclusionStrength);

    half3 n = normalize(i.normalWs) * (facing > 0 ? 1 : -1);
    half3 v = normalize(_WorldSpaceCameraPos - i.posWs);
    half3 diffuse = i.vertexDiffuse;
    half3 specular = i.vertexSpecular;
#if !defined(FLAT_VERTEX_LIGHTING)
    FlatValveLights(i.posWs, n, v, gloss, diffuse, specular);
#endif
    half3 specColor = lerp(half3(0.04, 0.04, 0.04), albedo.rgb, metallic);
    half3 diffColor = albedo.rgb * (1 - metallic);
    half3 ambient = ShadeSH9(half4(n, 1));

    half3 r = reflect(-v, n);
    half4 probe = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, r, (1 - gloss) * 6);
    half3 reflection = DecodeHDR(probe, unity_SpecCube0_HDR) * g_flCubeMapScalar;
    half fresnel = pow(1 - saturate(dot(n, v)), 4);
    half3 envSpec = reflection * lerp(specColor, half3(1, 1, 1), fresnel * gloss) * gloss;

    half3 rgb = diffColor * (diffuse + ambient * occlusion) + specColor * specular + envSpec * occlusion;
    rgb += tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
    fixed4 col = fixed4(rgb, albedo.a);
#if defined(FLAT_BLEND_SRC_FROM_MODE)
    // _BlendMode One = premultiplied (glass): scale by alpha here
    if (_BlendMode < 2)
        col.rgb *= albedo.a;
#endif
#endif
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif
