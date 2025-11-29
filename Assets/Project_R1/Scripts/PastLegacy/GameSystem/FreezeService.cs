// 05_Freeze.cs
// 리지드바디 얼림/해제 서비스
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class FreezeService : IFreezeService
    {
        public void FreezeAll(IEnumerable<GameObject> targets)
        {
            foreach (var go in targets)
            {
                if (!go) continue;
                var rb = go.GetComponent<Rigidbody>();
                if (rb) rb.isKinematic = true;
            }
        }


        public void UnfreezeAll(IEnumerable<GameObject> targets)
        {
            foreach (var go in targets)
            {
                if (!go) continue;
                var rb = go.GetComponent<Rigidbody>();
                if (rb) rb.isKinematic = false;
            }
        }
    }
}
