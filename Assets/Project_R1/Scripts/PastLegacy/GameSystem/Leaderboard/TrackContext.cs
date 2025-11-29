using UnityEngine;

namespace R1
{
    public class TrackContext : MonoBehaviour
    {
        public static TrackContext Instance { get; private set; }

        [Tooltip("0 = Start/Finish, 폐루프 가정")]
        public Transform[] checkpoints;

        [Tooltip("목표 랩 수(0이면 무제한)")]
        public int targetTotalLaps = 3;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
    }
}
