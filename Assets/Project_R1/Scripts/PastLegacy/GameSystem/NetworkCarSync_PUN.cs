// 10_NetworkCarSync_PUN.cs
// PUN2용 차량 네트워킹(소유자-권한 + 원격 보간)
// - CarController는 네트코드 비침투(수정 없이 공용). 이 컴포넌트를 추가해서 멀티 지원.
// - 로컬(소유자): 입력/물리 실행 + 스냅샷 전송
// - 원격: Rigidbody를 kinematic으로 두고 스냅샷 보간으로 시각화
// 참고: 더 정밀한 레이싱엔 보간 버퍼 크기/보간 지연값/보간법 확장 권장


using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace R1
{
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkCarSync_PUN : MonoBehaviour
    {
        [Header("Interpolation")]
        public float interpolationBackTime = 0.1f;
        [Tooltip("최대 버퍼 프레임")] public int bufferSize = 20;


        private PhotonView view;
        private Rigidbody rb;
        private Queue<State> buffer = new Queue<State>();


        struct State
        {
            public double time; // PhotonNetwork.Time
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 vel;
            public Vector3 angVel;
        }


        private void Awake()
        {
            view = GetComponent<PhotonView>();
            rb = GetComponent<Rigidbody>();
        }


        private void OnEnable()
        {
            // 원격 객체는 물리 비활성화 (시각화만)
            if (!view.IsMine)
            {
                rb.isKinematic = true;
            }
        }


        private void FixedUpdate()
        {
            if (view.IsMine) return; // 원격만 보간
            if (buffer.Count == 0) return;


            double interpTime = PhotonNetwork.Time - interpolationBackTime;


            // 목표 상태 찾기: interpTime보다 바로 이후의 샘플
            State newer = buffer.Peek();
            State older = newer;
            foreach (var s in buffer)
            {
                if (s.time <= interpTime) older = s; else { newer = s; break; }
            }


            if (older.time == newer.time)
            {
            // 보간 가능한 샘플이 1개뿐 → 그대로 적용
                rb.position = older.pos;
                rb.rotation = older.rot;
            }
            else
            {
                float t = 0f;
                double length = newer.time - older.time;
                if (length > 0.0001) t = (float)((interpTime - older.time) / length);
                t = Mathf.Clamp01(t);
                rb.position = Vector3.Lerp(older.pos, newer.pos, t);
                rb.rotation = Quaternion.Slerp(older.rot, newer.rot, t);
            }
        }


        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(rb.position);
                stream.SendNext(rb.rotation);
                stream.SendNext(rb.velocity);
                stream.SendNext(rb.angularVelocity);
            }
            else
            {
                var s = new State
                {
                    time = info.SentServerTime,
                    pos = (Vector3)stream.ReceiveNext(),
                    rot = (Quaternion)stream.ReceiveNext(),
                    vel = (Vector3)stream.ReceiveNext(),
                    angVel = (Vector3)stream.ReceiveNext()
                };
                buffer.Enqueue(s);
                while (buffer.Count > bufferSize) buffer.Dequeue();
            }
        }
    }
}
