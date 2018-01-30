#ifndef __TRAIL_COMPUTE_COMMON_INCLUDED__

#define __TRAIL_COMPUTE_COMMON_INCLUDED__

#define THREAD [numthreads(8, 1, 1)]
#define DISCARD if(id.x > (uint)_InstancesCount) return;

#ifndef PI
#define PI 3.14159265359
#endif

#include "UnityCG.cginc"
#include "../../../Shaders/Common/Random.cginc"
#include "../../../Shaders/Common/Matrix.cginc"
#include "../../../Shaders/Common/Quaternion.cginc"
// #include "../../../Shaders/Common/Noise/SimplexNoise3D.cginc"
#include "../../../Shaders/Common/Noise/SimplexNoiseGrad3D.cginc"
#include "../../../Shaders/Common/ProcSkinAnim.cginc"
#include "./Trail.cginc"

RWStructuredBuffer<GPUBone> _Bones;
RWStructuredBuffer<GPUTrail> _Trails;

CBUFFER_START(Params)

    int _InstancesCount;

    int _BonesCount;
    float _BonesCountInv;

    float4x4 _WorldToLocal, _LocalToWorld;

    float3 _Max, _Min, _Center;
    float _UnitLength;

    half _TrailFollowIntensity;
    half2 _TrailFollowDelay;

    float2 _DT;
    half2 _SpeedRange;

    float3 _NoiseOffset, _NoiseParams;
    half2 _Damper;

    half3 _Gravity;

CBUFFER_END

GPUTrail Create(int id, int i)
{
    GPUTrail tr;
    tr.position = float3(_Center.x, _Max.y - _UnitLength * i, _Center.z);
    tr.velocity = float3(0, 0, 0);
    tr.normal = float3(0, 0, 1);
    tr.tangent = float3(0, 1, 0);
    tr.binormal = float3(1, 0, 0);
    tr.speed = lerp(_SpeedRange.x, _SpeedRange.y, abs(nrand(float2(id, 0))));
    return tr;
}

#endif

