using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcAnimSkin.Demo
{

    public class Attractor : MonoBehaviour {

        [SerializeField] protected float speed = 0.2f;
        [SerializeField] protected float distance = 3f;

        Vector3 center;

        void Start () {
            center = transform.position;
        }
        
        void Update () {
            var t = Time.timeSinceLevelLoad * speed;
            transform.position = center + new Vector3(
                Mathf.PerlinNoise(t, 0) - 0.5f,
                Mathf.PerlinNoise(0, t) - 0.5f,
                Mathf.PerlinNoise(13.7f, -t) - 0.5f
            ) * distance;
        }

    }

}


