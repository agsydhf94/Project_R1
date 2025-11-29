using UnityEngine;

namespace R1
{
    public class StartFinishTrigger : MonoBehaviour
    {
        public RaceSessionManager rsm;
        [Tooltip("치팅/되돌이 방지용 최소 랩 시간(sec)")]
        public float minLapSeconds = 8f;

        // 라인 방향(씬 기준). 기본은 트리거의 forward를 사용하도록.
        public Vector3 forward = Vector3.zero;

        private float lastLapAt = -999f;
        private bool exitedSinceRaceStart = false;   // 레이스 시작 후 트리거 밖으로 나갔다가 다시 들어왔는가?
        Rigidbody localRb;                   // 성능 위해 캐시

        // RSM에서 Racing 진입 시 호출해 주세요.
        public void OnRaceStart()
        {
            exitedSinceRaceStart = false;
            // 바로 랩 카운트 가능하게 무장(첫 랩이 짧아도 최소 시간에 걸리지 않도록 여유)
            lastLapAt = Time.time - minLapSeconds;

            // forward 기본값이 (0,0,0) 이면, 트리거 오브젝트의 forward를 사용
            if (forward == Vector3.zero)
                forward = transform.forward;

            // 로컬 차량 리지드바디 캐시
            localRb = rsm && rsm.localCar ? rsm.localCar.GetComponent<Rigidbody>() : null;
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[S/F] ENTER raw by {other.name}");

            if (rsm == null) { Debug.Log("[S/F] rsm null"); return; }
            if (rsm.localState != RaceState.Racing) { Debug.Log($"[S/F] not racing {rsm.localState}"); return; }
            if (!IsLocalCar(other)) { Debug.Log("[S/F] not local car"); return; }

            if (!exitedSinceRaceStart) { Debug.Log("[S/F] not armed yet (need EXIT first)"); return; }

            var v = (localRb != null) ? localRb.velocity : Vector3.zero;
            float dot = (v.sqrMagnitude > 0.01f) ? Vector3.Dot(v.normalized, forward.normalized) : 1f;
            float elapsed = Time.time - lastLapAt;

            Debug.Log($"[S/F] armed ENTER: speed={v.magnitude:0.00}, dot={dot:0.00}, elapsed={elapsed:0.00}");

            if (v.sqrMagnitude > 0.01f && dot < 0.2f) { Debug.Log("[S/F] rejected by dot"); return; }
            if (elapsed < minLapSeconds) { Debug.Log("[S/F] rejected by min"); return; }

            lastLapAt = Time.time;
            exitedSinceRaceStart = false; // 다음 랩을 위해 다시 EXIT가 필요
            Debug.Log("[S/F] LAP COMPLETED!");
            rsm.OnLapCompleted();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalCar(other)) return;
            if (rsm == null || rsm.localState != RaceState.Racing) return;

            exitedSinceRaceStart = true;
            Debug.Log("[S/F] EXIT -> armed = true");
        }

        private bool IsLocalCar(Collider col)
        {
            // 차량 루트에서 CarController 찾기
            var rb = col.attachedRigidbody;
            if (!rb) return false;
            var car = rb.GetComponentInParent<CarController>();
            return car != null && car == rsm.localCar;
        }
    }
}
