using UnityEngine;

namespace R1
{
    public class AxleLoadEstimator : MonoBehaviour
    {
        public Rigidbody rb;
        public Transform centerOfMass;   // 선택: 지정하면 사용, 비우면 기본값
        void Awake()
        {
            if (!rb) rb = GetComponent<Rigidbody>();
            if (rb)
            {
                if (centerOfMass)
                    rb.centerOfMass = rb.transform.InverseTransformPoint(centerOfMass.position);
                else
                    rb.ResetCenterOfMass();
                rb.ResetInertiaTensor();
            }
        }
    #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (rb)
            {
                var comWorld = rb.transform.TransformPoint(rb.centerOfMass);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(comWorld, 0.04f);
            }
        }
    #endif
    }
}

