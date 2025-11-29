using UnityEngine;

namespace R1
{
    /// <summary>
    /// Trigger placed on each checkpoint collider.
    /// Detects when a car's Rigidbody passes through and notifies its LapCheckpointTracker.
    /// </summary>
    public class CheckpointTrigger : MonoBehaviour
    {
        /// <summary>
        /// Index of the checkpoint.
        /// 0 = start/finish line, followed by 1, 2, ...
        /// </summary>
        public int index = 0;


        /// <summary>
        /// Called when a collider enters this checkpoint trigger.
        /// Retrieves the Rigidbody’s LapCheckpointTracker (if present)
        /// and forwards the checkpoint index to it.
        /// </summary>
        /// <param name="other">The collider that entered the trigger.</param>
        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (!rb) return;

            var tracker = rb.GetComponentInParent<LapCheckpointTracker>();
            if (!tracker) return;

            tracker.HitCheckpoint(index);
        }
    }
}
