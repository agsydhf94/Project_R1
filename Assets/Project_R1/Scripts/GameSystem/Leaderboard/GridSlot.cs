using UnityEngine;

namespace R1
{
    /// <summary>
    /// Minimal tag component for grid-based UIs.
    /// Stores a stable, zero-based index identifying this slot within a grid.
    /// The default value (int.MaxValue) acts as a sentinel for "unset".
    /// </summary>
    public class GridSlot : MonoBehaviour
    {
        /// <summary>
        /// Zero-based index of this slot in its grid. 
        /// Defaults to int.MaxValue to indicate "uninitialized/unassigned".
        /// Set this from the grid builder or at scene load.
        /// </summary>
        public int index = int.MaxValue;
    }
}
