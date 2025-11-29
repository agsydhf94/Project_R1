using UnityEngine;

namespace R1
{
    public class WheelDebugProbe : MonoBehaviour
    {
        public CarController car;
        public WheelCollider[] wheels; // FL, FR, RL, RR
        public bool drawGizmos = true;

        void Update()
        {
            if (car == null || wheels == null || wheels.Length < 4) return;

            for (int i = 0; i < 4; i++)
            {
                var wc = wheels[i];
                if (wc == null) continue;

                WheelHit hit;
                bool grounded = wc.GetGroundHit(out hit);

                if (grounded)
                {
                    Debug.DrawLine(wc.transform.position, hit.point, Color.green, 0, false);
                    Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.cyan, 0, false);

                    // 압축량(근사)
                    float travel = (-wc.transform.InverseTransformPoint(hit.point).y - wc.radius) / Mathf.Max(0.0001f, wc.suspensionDistance);
                    travel = Mathf.Clamp01(travel);

                    // 지면 콜라이더 이름/레이어/슬립 값 확인
                    Debug.Log($"Wheel[{i}] grounded on '{hit.collider.name}' L{hit.collider.gameObject.layer} | " +
                            $"travel={travel:0.00} | fSlip={hit.forwardSlip:0.00} sSlip={hit.sidewaysSlip:0.00}");
                }
                else
                {
                    Debug.DrawRay(wc.transform.position, -wc.transform.up * (wc.suspensionDistance + wc.radius), Color.red, 0, false);
                }
            }
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos || wheels == null) return;
            Gizmos.color = Color.yellow;
            foreach (var wc in wheels)
            {
                if (wc == null) continue;
                Gizmos.DrawWireSphere(wc.transform.position - wc.transform.up * wc.suspensionDistance, 0.03f);
            }
        }
    }
}
