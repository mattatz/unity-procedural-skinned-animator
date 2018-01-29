using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSkinAnim.Demo
{

    [RequireComponent (typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkinningSiblingTest : MonoBehaviour {

        [SerializeField] protected int division = 8;
        [SerializeField] protected List<Transform> bones;

        new protected Renderer renderer;
        protected Material material;

        protected ComputeBuffer boneBuffer, weightBuffer;

        protected List<TRS> locals = new List<TRS>();
        protected List<TRS> globals = new List<TRS>();

        #region Shader property keys

        protected const string kWeightsKey = "_Weights";
        protected const string kBonesKey = "_Bones", kBonesCountKey = "_BonesCount", kBonesCountInvKey = "_BonesCountInv";
        protected const string kBindMatrixKey = "_BindMatrix", kBindMatrixInvKey = "_BindMatrixInv";
        protected const string kWorldToLocalKey = "_WorldToLocal", kLocalToWorldKey = "_LocalToWorld";

        #endregion

        void Start () {
            renderer = GetComponent<Renderer>();
            material = renderer.material;

            var filter = GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;

            var bounds = mesh.bounds;
            var center = bounds.center;
            Vector3 min = bounds.min, max = bounds.max;
            var unit = (max.y - min.y) / (division - 1);

            var vertices = mesh.vertices;
            var weights = new GPUBoneWeight[mesh.vertexCount];
            for(int i = 0, n = vertices.Length; i < n; i++)
            {
                var p = vertices[i];
                var u = (division - 1) - (p.y - min.y) / unit;
                if(u > 0f)
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
                    weights[i].boneIndex1 = 0;
                    weights[i].weight1 = 0;
                }
            }
            weightBuffer = new ComputeBuffer(mesh.vertexCount, Marshal.SizeOf(typeof(GPUBoneWeight)));
            weightBuffer.SetData(weights);

            boneBuffer = new ComputeBuffer(division, Marshal.SizeOf(typeof(GPUBone)));

            for(int i = 0; i < division; i++)
            {
                var bone = new GameObject("Bone" + i);
                bones.Add(bone.transform);

                bone.transform.SetParent(transform, false);
                bone.transform.localPosition = new Vector3(center.x, max.y - unit * i, center.z);

                // keep global initial transform
                locals.Add(new TRS(bone.transform.localPosition, bone.transform.localRotation, bone.transform.localScale));
                globals.Add(new TRS(bone.transform));
            }

            material.SetBuffer(kWeightsKey, weightBuffer);
            material.SetBuffer(kBonesKey, boneBuffer);
            material.SetInt(kBonesCountKey, division);
            material.SetFloat(kBonesCountInvKey, 1f / division);

            // bind initial matrix for bones
            material.SetMatrix(kBindMatrixKey, transform.localToWorldMatrix);
            material.SetMatrix(kBindMatrixInvKey, transform.worldToLocalMatrix);
        }
        
        void Update () {
            UpdateBones();
            material.SetMatrix(kBindMatrixInvKey, transform.worldToLocalMatrix);
            material.SetMatrix(kWorldToLocalKey, transform.worldToLocalMatrix);
            material.SetMatrix(kLocalToWorldKey, transform.localToWorldMatrix);
        }

        void UpdateBones ()
        {
            var s1 = Vector3.one;
            var data = new GPUBone[bones.Count];

            var offset = transform.localToWorldMatrix;
            for(int i = 0, n = bones.Count; i < n; i++)
            {
                var local = locals[i]; // initial local transform
                var global = globals[i]; // initial global transform
                var current = bones[i]; // current transform
                var bone = new GPUBone(global.T, global.R, global.S);
                var curM = Matrix4x4.TRS(current.transform.localPosition, current.transform.localRotation, current.transform.localScale);
                var diff = (curM * local.IM);
                bone.comb = (offset * (diff * local.M)) * global.IM;
                data[i] = bone;
            }
            boneBuffer.SetData(data);
        }

        void OnDestroy ()
        {
            if(material != null)
            {
                Destroy(material);
                material = null;
            }

            if(boneBuffer != null)
            {
                boneBuffer.Release();
                boneBuffer = null;
            }

            if(weightBuffer != null)
            {
                weightBuffer.Release();
                weightBuffer = null;
            }
        }

        void OnDrawGizmos ()
        {
            if (bones == null) return;

            bones.ForEach(bone =>
            {
                Gizmos.matrix = bone.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.045f);
            });
        }

    }

}


