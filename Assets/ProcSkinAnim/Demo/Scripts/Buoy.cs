using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSkinAnim.Demo
{

    public class Buoy : MonoBehaviour
    {

        [SerializeField] protected float speed = 0.25f;

        void Update()
        {
            transform.position += Vector3.up * speed * Time.deltaTime;
        }

    }

}


