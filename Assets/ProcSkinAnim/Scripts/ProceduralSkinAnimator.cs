using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSkinAnim
{

    public abstract class ProceduralSkinAnimator : MonoBehaviour {

        [SerializeField] protected Mesh mesh;
        [SerializeField] protected Material material;
        [SerializeField] protected ComputeShader compute;
        [SerializeField] protected int instancesCount = 128;
        [SerializeField] protected int boneCount = 8;

        protected ComputeBuffer boneBuffer, weightBuffer, argsBuffer;
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        Kernel setupKernel, updateKernel, applyKernel;

        #region Shader property keys

        protected const string kInstancesCountKey = "_InstancesCount";
        protected const string kWeightsKey = "_Weights";
        protected const string kBonesKey = "_Bones", kBonesCountKey = "_BonesCount", kBonesCountInvKey = "_BonesCountInv";
        protected const string kBindMatrixKey = "_BindMatrix", kBindMatrixInvKey = "_BindMatrixInv";
        protected const string kWorldToLocalKey = "_WorldToLocal", kLocalToWorldKey = "_LocalToWorld";
        protected const string kTimeKey = "_Time", kDTKey = "_DT";

        #endregion

        protected virtual void Start () {
            args[0] = mesh.GetIndexCount(0);
            args[1] = (uint)instancesCount;
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);

            weightBuffer = new ComputeBuffer(mesh.vertexCount, Marshal.SizeOf(typeof(GPUBoneWeight)));
            weightBuffer.SetData(BuildWeights());

            boneBuffer = new ComputeBuffer(instancesCount * boneCount, Marshal.SizeOf(typeof(GPUBone)));
            boneBuffer.SetData(BuildBones());

            material.SetBuffer(kWeightsKey, weightBuffer);
            material.SetBuffer(kBonesKey, boneBuffer);
            material.SetInt(kBonesCountKey, boneCount);
            material.SetFloat(kBonesCountInvKey, 1f / boneCount);

            setupKernel = new Kernel(compute, "Setup");
            updateKernel = new Kernel(compute, "Update");
            applyKernel = new Kernel(compute, "Apply");

            // bind initial matrix for bones
            material.SetMatrix(kBindMatrixKey, transform.localToWorldMatrix);
            material.SetMatrix(kBindMatrixInvKey, transform.worldToLocalMatrix);

            Compute(setupKernel, 0f);
            Compute(updateKernel, Time.deltaTime);
        }

        protected abstract GPUBoneWeight[] BuildWeights();
        protected abstract GPUBone[] BuildBones();
        
        protected virtual void Update () {
            Compute(updateKernel, Time.deltaTime);
            Compute(applyKernel, Time.deltaTime);

            material.SetMatrix(kBindMatrixInvKey, transform.worldToLocalMatrix);
            material.SetMatrix(kWorldToLocalKey, transform.worldToLocalMatrix);
            material.SetMatrix(kLocalToWorldKey, transform.localToWorldMatrix);

            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, Vector3.one * 1000f), argsBuffer);
        }

        protected virtual void Compute(Kernel kernel, float dt)
        {
            compute.SetInt(kInstancesCountKey, instancesCount);
            compute.SetBuffer(kernel.Index, kBonesKey, boneBuffer);
            compute.SetInt(kBonesCountKey, boneCount);
            compute.SetFloat(kBonesCountInvKey, 1f / boneCount);
            var t = Time.timeSinceLevelLoad;
            compute.SetVector(kTimeKey, new Vector4(t / 20f, t, t * 2f, t * 3f));
            compute.SetVector(kDTKey, new Vector2(dt, (dt < float.Epsilon) ? 0f : 1f / dt));

            compute.SetMatrix(kWorldToLocalKey, transform.worldToLocalMatrix);
            compute.SetMatrix(kLocalToWorldKey, transform.localToWorldMatrix);
            compute.SetMatrix(kBindMatrixKey, material.GetMatrix(kBindMatrixKey));
            compute.SetMatrix(kBindMatrixInvKey, material.GetMatrix(kBindMatrixInvKey));

            compute.Dispatch(kernel.Index, instancesCount / kernel.ThreadX + 1, kernel.ThreadY, kernel.ThreadZ);
        }

        protected virtual void OnDrawGizmosSelected ()
        {
        }

        protected virtual void OnDestroy()
        {
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

            if(argsBuffer != null)
            {
                argsBuffer.Release();
                argsBuffer = null;
            }
        }

    }

}


