using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSkinAnim.Demo
{

    public class TransformTest : MonoBehaviour {

        List<Transform> bones;
        List<TRS> pose; // world initials

        void Start () {
            bones = new List<Transform>();
            pose = new List<TRS>();

            for(int i = 0; i < 4; i++)
            {
                var bone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bone.name = "Bone" + i;
                if(i == 0)
                {
                    bone.transform.SetParent(transform, false);
                } else {
                    bone.transform.SetParent(bones[i - 1], false);
                }
                bone.transform.localPosition = Vector3.up * 1.5f;
                bones.Add(bone.transform);
                pose.Add(new TRS(bone.transform));
            }
        }

        void OnDrawGizmos ()
        {
            if (bones == null || bones.Count <= 0) return;

            var o2w = transform.localToWorldMatrix;
            var w2o = transform.worldToLocalMatrix;
            var offset = transform.localToWorldMatrix;

            for(int i = 0, n = bones.Count; i < n; i++)
            {
                var bone = bones[i];
                Gizmos.color = Color.red;
                Gizmos.matrix = bone.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

                Gizmos.matrix = Matrix4x4.identity;
                if(i < n - 1)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(bones[i].transform.position, bones[i + 1].transform.position);
                }

                var cur = new TRS(bone.localPosition, bone.localRotation, bone.localScale);
                offset = (offset * cur.M);
                var world = offset.MultiplyPoint(Vector3.zero);

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(world, 0.5f);
            }
        }

    }

}


