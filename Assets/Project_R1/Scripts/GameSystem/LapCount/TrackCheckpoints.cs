using System.Linq;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Collects and orders all <see cref="CheckpointTrigger"/> components in the scene
    /// or under a specified root transform. Ensures index 0 (start/finish) is present
    /// and placed first in the list.
    /// </summary>
    public class TrackCheckpoints : MonoBehaviour
    {
        /// <summary>
        /// Optional root transform. If set, only child <see cref="CheckpointTrigger"/>s
        /// will be collected. If null, all triggers in the scene are collected.
        /// </summary>
        public Transform root;

        [SerializeField] private CheckpointTrigger[] triggers = new CheckpointTrigger[0];

        /// <summary>
        /// Number of collected checkpoint triggers.
        /// </summary>
        public int Count => (triggers != null) ? triggers.Length : 0;

        void Awake() { Refresh(); }
        void OnValidate() { Refresh(); }


        /// <summary>
        /// Refreshes the list of checkpoint triggers.  
        /// Collects triggers from the scene or root, sorts them by index,  
        /// and warns if index 0 (start/finish) is missing or not first.
        /// </summary>
        public void Refresh()
        {
            if (!root)
            {
                triggers = FindObjectsOfType<CheckpointTrigger>(true);
            }
            else
            {
                triggers = root.GetComponentsInChildren<CheckpointTrigger>(true);
            }

            // Sort by index as defined in the inspector
            triggers = triggers.OrderBy(t => t.index).ToArray();

            // Warn if index 0 (start/finish) is missing or not first
            if (triggers.Length == 0 || triggers[0].index != 0)
                Debug.LogWarning("[TrackCheckpoints] Ensure index=0 (start/finish) is placed first.");
        }
    }
}
