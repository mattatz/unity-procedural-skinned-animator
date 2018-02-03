#ifndef __PROC_SKIN_ANIM_COMMON_INCLUDED__

#define __PROC_SKIN_ANIM_COMMON_INCLUDED__

#include "Quaternion.cginc"

struct GPUBone
{
    float3 position;
    // float4 rotation;
    float4x4 rotation;
    float3 scale;
    float4x4 combined, local, offset;
};

struct GPUBoneWeight
{
    float weight0, weight1, weight2, weight3;
    uint boneIndex0, boneIndex1, boneIndex2, boneIndex3;
};

float4x4 GetBoneMatrix(GPUBone bone) {
    float4x4 T = float4x4(
        1, 0, 0, bone.position.x,
        0, 1, 0, bone.position.y,
        0, 0, 1, bone.position.z,
        0, 0, 0, 1
    );
    // float4x4 R = quaternion_to_matrix(bone.rotation);
    float4x4 R = bone.rotation;
    float4x4 S = float4x4(
        bone.scale.x, 0, 0, 0,
        0, bone.scale.y, 0, 0,
        0, 0, bone.scale.z, 0,
        0, 0, 0, 1
    );
    return mul(mul(T, R), S);
}

#endif

