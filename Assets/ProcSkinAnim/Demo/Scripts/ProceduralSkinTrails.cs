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

        #region Trail properties

        [SerializeField] protected ComputeShader trailCompute;
        [SerializeField] protected float speed = 1f;
        [SerializeField, Range(0f, 5f)] protected float trailFollowIntensity = 3.0f;
        [SerializeField, Range(0f, 1f)] protected float trailFollowDelayMin = 0.1f, trailFollowDelayMax = 1.0f;
        [SerializeField] protected float speedLimit = 1.0f;
        [SerializeField, Range(0, 1)] protected float drag = 0.1f;
        [SerializeField] protected Vector3 gravity = Vector3.zero;
        [SerializeField] protected float noiseAmplitude = 1.0f;
        [SerializeField] protected float noiseFrequency = 0.01f;
        [SerializeField] protected float noiseMotion = 1.0f;
        protected Vector3 noiseOffset;

        #endregion

        protected ComputeBuffer trailBuffer;
        protected Kernel setupTrailKernel, updateTrailKernel, applyTrailKernel;

        #region Shader property keys

        protected const string kTrailsKey = "_Trails", kTrailsCountKey = "_TrailsCount";
        protected const string kMaxKey = "_Max", kMinKey = "_Min", kCenterKey = "_Center", kUnitLengthKey = "_UnitLength";

        protected const string kTrailFollowIntensityKey = "_TrailFollowIntensity";
        protected const string kTrailFollowDelayKey = "_TrailFollowDelay";

        protected const string kSpeedKey = "_Speed";
        protected const string kDamperKey = "_Damper";
        protected const string kGravityKey = "_Gravity";
        protected const string kNoiseParamsKey = "_NoiseParams", kNoiseOffsetKey = "_NoiseOffset";

        protected Vector3 min, max, center;
        protected int followingBoneCount;
        protected float unitLength;

        #endregion

        protected override void Start()
        {
            base.Start();

            trailBuffer = new ComputeBuffer(boneBuffer.count, Marshal.SizeOf(typeof(GPUTrail)));
            setupTrailKernel = new Kernel(trailCompute, "Setup");
            updateTrailKernel = new Kernel(trailCompute, "Update");
            applyTrailKernel = new Kernel(trailCompute, "Apply");
            ComputeTrails(setupTrailKernel, 0f);
        }

        protected void CheckInit()
        {
            if (unitLength > 0f) return;

            // Setup bone structure data
            var bounds = mesh.bounds;
            min = bounds.min;
            max = bounds.max;
            center = bounds.center;

            // To avoid bind a head bone to vertices, set followingBoneCount to boneCount - 2.
            followingBoneCount = boneCount - 2;
            unitLength = (max.y - min.y) / followingBoneCount;

            trailCompute.SetVector(kMaxKey, max);
            trailCompute.SetVector(kMinKey, min);
            trailCompute.SetVector(kCenterKey, center);
            trailCompute.SetFloat(kUnitLengthKey, unitLength);
        }

        protected override void Update()
        {
            ComputeTrails(updateTrailKernel, Time.deltaTime * speed);
            ComputeTrails(applyTrailKernel, Time.deltaTime * speed);
            base.Update();
        }

        protected void ComputeTrails(Kernel kernel, float dt) {
            CheckInit();

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

            trailCompute.SetFloat(kTrailFollowIntensityKey, trailFollowIntensity);
            trailCompute.SetVector(kTrailFollowDelayKey, new Vector2(trailFollowDelayMin, trailFollowDelayMax));

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
            CheckInit();

            var vertices = mesh.vertices;
            var weights = new GPUBoneWeight[mesh.vertexCount];
            for (int i = 0, n = vertices.Length; i < n; i++)
            {
                var p = vertices[i];

                // 1f offset avoids binding a head bone.
                var u = 1f + followingBoneCount - ((p.y - min.y) / unitLength);

                if (u > 0f)
                {
                    var lu = Mathf.FloorToInt(u);
                    var cu = Mathf.CeilToInt(u);
                    var t = Mathf.Clamp01(u - lu);

                    weights[i].boneIndex0 = (uint)lu;
                    weights[i].weight0 = 1f - t;
                    weights[i].boneIndex1 = (uint)cu;
                    weights[i].weight1 = t;
                } else
                {
                    // tip case (u <= 0f)
                    weights[i].boneIndex0 = 0;
                    weights[i].weight0 = 1f;
                    weights[i].boneIndex1 = 0;
                    weights[i].weight1 = 0f;
                }
            }
            return weights;
        }

        protected override GPUBone[] BuildBones()
        {
            CheckInit();

            var vertices = mesh.vertices;
            var bones = new GPUBone[instancesCount * boneCount];
            var rot = transform.rotation;
            var scale = transform.lossyScale;
            for (int y = 0; y < instancesCount; y++)
            {
                var ioffset = y * boneCount;
                for (int x = 0; x < boneCount; x++)
                {
                    var p = new Vector3(center.x, max.y - unitLength * x, center.z);
                    // keep world offset transform 
                    bones[ioffset + x] = new GPUBone(transform.TransformPoint(p), rot, scale);
                }
            }

            return bones;
        }

        protected override void OnDrawGizmosSelected ()
        {
            if (trailBuffer == null) return;
            // DrawTrailGizmos();
        }

        void DrawTrailGizmos() {
            Gizmos.matrix = transform.localToWorldMatrix;

            var trails = new GPUTrail[trailBuffer.count];
            trailBuffer.GetData(trails);

            const float size = 0.05f;
            const float length = 0.1f;

            for (int y = 0; y < instancesCount; y++) {
                var offset = y * boneCount;
                for (int x = 0; x < boneCount - 1; x++) {
                    int index = x + offset;
                    var cur = trails[index];
                    Gizmos.color = Color.white;

                    // var next = trails[index + 1];
                    // Gizmos.DrawLine(cur.position, next.position);
                    Gizmos.DrawWireSphere(cur.position, size);

                    Vector3 t = cur.tangent, n = cur.normal, b = cur.binormal;
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(cur.position, cur.position + t * length);
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(cur.position, cur.position + n * length);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(cur.position, cur.position + b * length);
                }
            }

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


