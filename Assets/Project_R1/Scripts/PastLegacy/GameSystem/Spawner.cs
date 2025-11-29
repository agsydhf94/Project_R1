// 03_Spawner.cs
// 스폰: 싱글(Local Instantiate) / 멀티(PhotonNetwork.Instantiate)
using System.Collections.Generic;
using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace R1
{
    public sealed class LocalSpawner : ISpawner
    {
        public GameObject SpawnPlayer(string prefabName, Transform slot)
        {
            var prefab = Resources.Load<GameObject>(prefabName);
            return Object.Instantiate(prefab, slot.position, slot.rotation);
        }


        public List<GameObject> SpawnAIs(string[] prefabNames, Transform[] slots, int startIndex)
        {
            var list = new List<GameObject>();
            for (int i = 0; i < prefabNames.Length && (startIndex + i) < slots.Length; i++)
            {
                var prefab = Resources.Load<GameObject>(prefabNames[i]);
                var go = Object.Instantiate(prefab, slots[startIndex + i].position, slots[startIndex + i].rotation);
                go.tag = "AI";
                list.Add(go);
            }
            return list;
        }
    }

#if PHOTON_UNITY_NETWORKING
    public sealed class PhotonSpawner : ISpawner
    {
        public GameObject SpawnPlayer(string prefabName, Transform slot)
        {
            return PhotonNetwork.Instantiate(prefabName, slot.position, slot.rotation);
        }


    public List<GameObject> SpawnAIs(string[] prefabNames, Transform[] slots, int startIndex)
        {
            var list = new List<GameObject>();
            if (!PhotonNetwork.IsMasterClient) return list; // 마스터만 AI 생성
            
            for (int i = 0; i < prefabNames.Length && (startIndex + i) < slots.Length; i++)
                {
                    var go = PhotonNetwork.Instantiate(prefabNames[i], slots[startIndex + i].position, slots[startIndex + i].rotation);
                    go.tag = "AI";
                    list.Add(go);
                }
            return list;
        }
    }
    public class Spawner : MonoBehaviour
    {

    }
#endif
}
