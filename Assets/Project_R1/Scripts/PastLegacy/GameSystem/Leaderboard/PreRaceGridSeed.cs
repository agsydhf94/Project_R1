using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class PreRaceGridSeed : MonoBehaviour
    {
        public struct Entry
        {
            public int gridIndex;   // 0 = 폴
            public bool isAI;
            public string userId;        // 선택: 없으면 null/빈값
            public string displayName;   // 선택: 없으면 null/빈값
        }

        private static List<Entry> _entries;
        public static IReadOnlyList<Entry> Entries => _entries;

        public static bool HasData => _entries != null && _entries.Count > 0;

        public static void Build(int aiCount, string[] aiPool, string localUserId = null, string localName = null)
        {
            _entries = new List<Entry>(1 + aiCount);

            // 플레이어(폴)
            _entries.Add(new Entry {
                gridIndex = 0,
                isAI = false,
                userId = string.IsNullOrEmpty(localUserId) ? "LOCAL" : localUserId,
                displayName = string.IsNullOrEmpty(localName) ? "Player" : localName
            });

            // AI (그 뒤)
            int n = aiPool != null ? System.Math.Min(aiCount, aiPool.Length) : aiCount;
            for (int i = 0; i < n; i++)
            {
                _entries.Add(new Entry {
                    gridIndex = 1 + i,
                    isAI = true,
                    userId = $"AI_{i + 1}",
                    displayName = $"AI Player #{i + 1}"
                });
            }
        }

        // 한 번 보여주면 비워서 재사용 이슈 방지
        public static void Clear() => _entries = null;
    }
}
