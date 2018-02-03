// Upgrade NOTE: upgraded instancing buffer 'Props' to new syntax.


#include "UnityCG.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardUtils.cginc"

#include "../Common/ProcSkinAnim.cginc"

#ifndef PI
#define PI 3.14159265359
#endif

#ifndef HALF_PI
#define HALF_PI 1.57079632679
#endif

#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
#define PASS_CUBE_SHADOWCASTER
#endif

half4 _Color;

sampler2D _MainTex;
float4 _MainTex_ST;

half _Glossiness;
half _Metallic;

StructuredBuffer<GPUBoneWeight> _Weights;
StructuredBuffer<GPUBone> _Bones;
int _BonesCount;
float _BonesCountInv;

float4x4 _LocalToWorld, _WorldToLocal;
float4x4 _BindMatrix, _BindMatrixInv;

UNITY_INSTANCING_BUFFER_START(Props)
UNITY_INSTANCING_BUFFER_END(Props)

struct Attributes {
    float4 position : POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
    #if defined(BONE_WEIGHT_DEBUG)
    float3 color : COLOR;
    #endif
};

// Fragment varyings
struct Varyings {
    float4 position : POSITION;

#if defined(PASS_CUBE_SHADOWCASTER)
    // Cube map shadow caster pass
    float3 shadow : TEXCOORD0;

#elif defined(UNITY_PASS_SHADOWCASTER)
    // Default shadow caster pass

#else
    // GBuffer construction pass
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
    half3 ambient : TEXCOORD1;
    float3 wpos : TEXCOORD2;
    #if defined(BONE_WEIGHT_DEBUG)
    float3 color : COLOR;
    #endif
#endif
};

//
// Vertex stage
//

float3 GetSkinnedVertex(float3 pos, uint vid, uint iid) {
    GPUBoneWeight weight = _Weights[vid];

    int offset = iid * _BonesCount;
    float4 skinVertex = mul(_BindMatrix, float4(pos, 1));
    // float4 skinVertex = float4(pos, 1);
    float4 skinned = float4(0, 0, 0, 0);
    skinned += mul(_Bones[offset + weight.boneIndex0].combined, skinVertex) * weight.weight0;
    skinned += mul(_Bones[offset + weight.boneIndex1].combined, skinVertex) * weight.weight1;
    return mul(_BindMatrixInv, skinned).xyz;
}

float3 GetSkinnedNormal(float3 normal, uint vid, uint iid) {
    GPUBoneWeight weight = _Weights[vid];

    int offset = iid * _BonesCount;
    float4x4 skinMatrix = float4x4(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    skinMatrix += weight.weight0 * _Bones[offset + weight.boneIndex0].combined;
    skinMatrix += weight.weight1 * _Bones[offset + weight.boneIndex1].combined;
    skinMatrix = mul(mul(_BindMatrixInv, skinMatrix), _BindMatrix);
    return mul(skinMatrix, float4(normal, 0)).xyz;
}

Varyings Vertex(in Attributes IN, uint vid : SV_VertexID, uint iid : SV_InstanceID)
{
    Varyings o;

    unity_ObjectToWorld = _LocalToWorld;
    unity_WorldToObject = _WorldToLocal;

    // float3 pos = IN.position.xyz;
    // float3 normal = IN.normal.xyz;
    float3 pos = GetSkinnedVertex(IN.position.xyz, vid, iid);
    float3 normal = GetSkinnedNormal(IN.normal.xyz, vid, iid);

    GPUBoneWeight weight = _Weights[vid];

    // UNITY_ACCESS_INSTANCED_PROP(_Color);

    float3 wnrm = UnityObjectToWorldNormal(normal);
    float3 wpos = mul(unity_ObjectToWorld, float4(pos, 1)).xyz;

#if defined(PASS_CUBE_SHADOWCASTER)
    // Cube map shadow caster pass: Transfer the shadow vector.
    o.position = UnityObjectToClipPos(float4(pos.xyz, 1));
    o.shadow = wpos - _LightPositionRange.xyz;

#elif defined(UNITY_PASS_SHADOWCASTER)
    // Default shadow caster pass: Apply the shadow bias.
    float scos = dot(wnrm, normalize(UnityWorldSpaceLightDir(wpos)));
    wpos -= wnrm * unity_LightShadowBias.z * sqrt(1 - scos * scos);
    o.position = UnityApplyLinearShadowBias(UnityWorldToClipPos(float4(wpos, 1)));

#else
    // GBuffer construction pass
    o.position = UnityWorldToClipPos(float4(wpos, 1));
    o.normal = wnrm;
    o.texcoord = IN.texcoord;
    o.ambient = ShadeSHPerVertex(wnrm, 0);
    o.wpos = wpos;
    #if defined(BONE_WEIGHT_DEBUG)
    o.color = float3(weight.weight0, weight.weight1, (1.0 * weight.boneIndex0) * _BonesCountInv);
    #endif
#endif

    return o;
}

//
// Fragment phase
//

#if defined(PASS_CUBE_SHADOWCASTER)

// Cube map shadow caster pass
half4 Fragment(Varyings input) : SV_Target
{
    float depth = length(input.shadow) + unity_LightShadowBias.x;
    return UnityEncodeCubeShadowDepth(depth * _LightPositionRange.w);
}

#elif defined(UNITY_PASS_SHADOWCASTER)

// Default shadow caster pass
half4 Fragment() : SV_Target { return 0; }

#else

// GBuffer construction pass
void Fragment (Varyings input, out half4 outGBuffer0 : SV_Target0, out half4 outGBuffer1 : SV_Target1, out half4 outGBuffer2 : SV_Target2, out half4 outEmission : SV_Target3) {
    #if defined(BONE_WEIGHT_DEBUG)
    half3 albedo = tex2D(_MainTex, input.texcoord).rgb * input.color.rgb;
    #else
    half3 albedo = tex2D(_MainTex, input.texcoord).rgb * _Color.rgb;
    #endif

    // PBS workflow conversion (metallic -> specular)
    half3 c_diff, c_spec;
    half refl10;
    c_diff = DiffuseAndSpecularFromMetallic(
        albedo, _Metallic, // input
        c_spec, refl10 // output
    );

    float3 nx = ddx(input.wpos);
    float3 ny = ddy(input.wpos);
    float3 normal = -normalize(cross(nx, ny));

    // Update the GBuffer.
    UnityStandardData data;
    data.diffuseColor = c_diff;
    data.occlusion = 1.0;
    data.specularColor = c_spec;
    data.smoothness = _Glossiness;
    // data.normalWorld = input.normal;
    data.normalWorld = normal;
    UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

    // Calculate ambient lighting and output to the emission buffer.
    half3 sh = ShadeSHPerPixel(data.normalWorld, input.ambient, input.wpos);
    outEmission = half4(sh * c_diff, 1);
}

#endif
