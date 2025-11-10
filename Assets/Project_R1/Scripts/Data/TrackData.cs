using UnityEngine;
using UnityEngine.AddressableAssets;

namespace R1
{
    [CreateAssetMenu(fileName = "New Track", menuName = "Racing/Track Data")]
    public class TrackData : ScriptableObject
    {
        [Header("Identity")]
        public string trackID;
        public string displayName;

        [Header("Addressable References")]
        public AssetReference sceneReference;  // Scene도 Addressable 가능!
        public AssetReference previewImageReference;

        [Header("Track Info")]
        public float trackLength;

        [Header("Unlock")]
        public bool isDLC;
        public string dlcPackID;
    }
}
