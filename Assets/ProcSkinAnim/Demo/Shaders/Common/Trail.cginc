#ifndef __TRAIL_COMMON_INCLUDED__

#define __TRAIL_COMMON_INCLUDED__

struct GPUTrail
{
    float3 position;
    float3 velocity;
    float3 normal;
    float3 tangent;
    float3 binormal;
    // float4 rotation;
    float speed;
};

#endif

