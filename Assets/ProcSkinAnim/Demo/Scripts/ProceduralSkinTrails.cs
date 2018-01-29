using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSkinAnim.Demo
{

    public class ProceduralSkinTrails : ProceduralSkinAnimator {

        #region Particle properties

        [SerializeField] protected float speed = 1f;
        [SerializeField] protected float speedScaleMin = 2.0f, speedScaleMax = 5.0f;
        [SerializeField] protected float speedLimit = 1.0f;
        [SerializeField, Range(0, 15)] protected float drag = 0.1f;
        [SerializeField] protected Vector3 gravity = Vector3.zero;
        [SerializeField] protected float noiseAmplitude = 1.0f;
        [SerializeField] protected float noiseFrequency = 0.01f;
        [SerializeField] protected float noiseMotion = 1.0f;
        protected Vector3 noiseOffset;

        #endregion

        [SerializeField] protected ComputeShader trailCompute;

        protected ComputeBuffer trailBuffer;
        protected Kernel setupTrailKernel, updateTrailKernel, applyTrailKernel;

        #region Shader property keys
        protected const string kTrailsKey = "_Trails", kTrailsCountKey = "_TrailsCount";
        protected const string kMaxKey = "_Max", kMinKey = "_Min", kCenterKey = "_Center", kUnitLengthKey = "_UnitLength";

        protected const string kSpeedKey = "_Speed";
        protected const string kDamperKey = "_Damper";
        protected const string kGravityKey = "_Gravity";
        protected const string kNoiseParamsKey = "_NoiseParams", kNoiseOffsetKey = "_NoiseOffset";
        #endregion

        protected override void Start()
        {
            base.Start();

            trailBuffer = new ComputeBuffer(boneBuffer.count, Marshal.SizeOf(typeof(GPUTrail)));
            setupTrailKernel = new Kernel(trailCompute, "Setup");
            updateTrailKernel = new Kernel(trailCompute, "Update");
            applyTrailKernel = new Kernel(trailCompute, "Apply");

            var bounds = mesh.bounds;
            Vector3 min = bounds.min, max = bounds.max;
            var unit = (max.y - min.y) / (boneCount - 1);

            trailCompute.SetVector(kMaxKey, max);
            trailCompute.SetVector(kMinKey, min);
            trailCompute.SetVector(kCenterKey, bounds.center);
            trailCompute.SetFloat(kUnitLengthKey, unit);

            ComputeTrails(setupTrailKernel, 0f);
        }

        protected override void Update()
        {
            ComputeTrails(updateTrailKernel, Time.deltaTime * speed);
            ComputeTrails(applyTrailKernel, Time.deltaTime * speed);
            base.Update();
        }

        protected void ComputeTrails(Kernel kernel, float dt) {
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            trailCompute.SetInt(kInstancesCountKey, instancesCount);
            trailCompute.SetBuffer(kernel.Index, kTrailsKey, trailBuffer);
            trailCompute.SetBuffer(kernel.Index, kBonesKey, boneBuffer);
            trailCompute.SetInt(kBonesCountKey, boneCount);
            trailCompute.SetFloat(kBonesCountInvKey, 1f / boneCount);

            var t = Time.timeSinceLevelLoad;
            trailCompute.SetVector(kTimeKey, new Vector4(t / 20f, t, t * 2f, t * 3f));
            trailCompute.SetVector(kDTKey, new Vector2(dt, (dt < float.Epsilon) ? 0f : 1f / dt));

            trailCompute.SetMatrix(kWorldToLocalKey, transform.worldToLocalMatrix);
            trailCompute.SetMatrix(kLocalToWorldKey, transform.localToWorldMatrix);

            trailCompute.SetMatrix(kBindMatrixKey, material.GetMatrix(kBindMatrixKey));
            trailCompute.SetMatrix(kBindMatrixInvKey, material.GetMatrix(kBindMatrixInvKey));

            trailCompute.SetVector(kDamperKey, new Vector2(Mathf.Exp(-drag * dt), speedLimit));
            trailCompute.SetVector(kGravityKey, gravity * dt);
            trailCompute.SetVector(kNoiseParamsKey, new Vector2(noiseFrequency, noiseAmplitude * dt));

            var noiseDir = (gravity == Vector3.zero) ? Vector3.up : gravity.normalized;
            noiseOffset += noiseDir * noiseMotion * dt;
            trailCompute.SetVector(kNoiseOffsetKey, noiseOffset);

            trailCompute.Dispatch(kernel.Index, instancesCount / kernel.ThreadX + 1, kernel.ThreadY, kernel.ThreadZ);
        }

        protected override GPUBoneWeight[] BuildWeights()
        {
            var bounds = mesh.bounds;
            Vector3 min = bounds.min, max = bounds.max;
            var unit = (max.y - min.y) / (boneCount - 1);
            var vertices = mesh.vertices;

            var weights = new GPUBoneWeight[mesh.vertexCount];
            for (int i = 0, n = vertices.Length; i < n; i++)
            {
                var p = vertices[i];
                var u = (boneCount - 1) - ((p.y - min.y) / unit);
                if (u > 0f)
                {
                    var lu = Mathf.FloorToInt(u);
                    var cu = Mathf.CeilToInt(u);
                    var t = u - lu;

                    weights[i].boneIndex0 = (uint)lu;
                    weights[i].weight0 = 1f - t;
                    weights[i].boneIndex1 = (uint)cu;
                    weights[i].weight1 = t;
                } else
                {
                    // tip case (u <= 0f)
                    weights[i].boneIndex0 = 0;
                    weights[i].weight0 = 1;
                    weights[i].boneIndex1 = 1;
                    weights[i].weight1 = 0;
                }
            }
            return weights;
        }

        protected override GPUBone[] BuildBones()
        {
            var bounds = mesh.bounds;
            Vector3 min = bounds.min, max = bounds.max;
            var unit = (max.y - min.y) / (boneCount - 1);
            var vertices = mesh.vertices;

            var bones = new GPUBone[instancesCount * boneCount];
            var rot = transform.rotation;
            var scale = transform.lossyScale;
            for (int y = 0; y < instancesCount; y++)
            {
                var ioffset = y * boneCount;
                for (int x = 0; x < boneCount; x++)
                {
                    var p = new Vector3(bounds.center.x, max.y - unit * x, bounds.center.z);
                    // keep world offset transform 
                    bones[ioffset + x] = new GPUBone(transform.TransformPoint(p), rot, scale);
                }
            }

            return bones;
        }

        bool Check(GPUBone bone) {
            Matrix4x4 rot = bone.rotation;

            for (int i = 0; i < 16; i++) {
                if (float.IsNaN(rot[i]) || float.IsInfinity(rot[i])) {
                    return true;
                }
            }

            return false;
        } 

        bool Check(GPUTrail tr) {
            Vector3 t = tr.tangent, n = tr.normal, bn = tr.binormal;
            if (
                float.IsNaN(t.x) || float.IsNaN(t.y) || float.IsNaN(t.z) ||
                float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z) ||
                float.IsNaN(bn.x) || float.IsNaN(bn.y) || float.IsNaN(bn.z) ||
                t.sqrMagnitude <= 0f || n.sqrMagnitude <= 0f || bn.sqrMagnitude <= 0f ||
                t.magnitude <= 0f || n.magnitude <= 0f || bn.magnitude <= 0f
            ) {
                return true;
            }
            return false;
        }

        protected override void OnDrawGizmosSelected ()
        {
            if (trailBuffer == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            /*
            var bones = new GPUBone[boneBuffer.count];
            boneBuffer.GetData(bones);
            for (int i = 0, n = bones.Length; i < n; i++) {
                var bone = bones[i];
                if (Check(bone)) {
                    // Debug.Log(bone.rotation);
                }
            }
            */

            /*
            var trails = new GPUTrail[trailBuffer.count];
            trailBuffer.GetData(trails);

            const float size = 0.05f;
            const float length = 0.1f;

            for (int y = 0; y < instancesCount; y++) {
                var offset = y * boneCount;
                for (int x = 0; x < boneCount - 1; x++) {
                    int index = x + offset;
                    var cur = trails[index];
                    var next = trails[index + 1];

                    Gizmos.color = Color.white;

                    Gizmos.DrawLine(cur.position, next.position);
                    Gizmos.DrawWireSphere(cur.position, 0.1f);

                    Gizmos.color = Color.green;
                    Vector3 t = cur.tangent, n = cur.normal, bn = cur.binormal;

                    Gizmos.DrawLine(cur.position, cur.position + t * length);
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(cur.position, cur.position + n * length);
                }
            }
            */
        }
 
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if(trailBuffer != null)
            {
                trailBuffer.Release();
                trailBuffer = null;
            }
        }

    }

}


