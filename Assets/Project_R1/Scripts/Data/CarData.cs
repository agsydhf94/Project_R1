using UnityEngine;
using UnityEngine.AddressableAssets;

namespace R1
{
    [CreateAssetMenu(fileName = "New Car", menuName = "Racing/Car Data")]
    public class CarData : ScriptableObject
    {
        [Header("Identity")]
        public string carID;
        public string displayName;

        [Header("Addressable References")]
        public AssetReference prefabReference;      // Addressables
        public AssetReference iconReference;        // Addressables
        public AssetReference engineSoundReference; // Addressables

        [Header("Performance Stats")]
        public float topSpeed = 280f;
        public float acceleration = 8f;
        public float handling = 7f;
        public float braking = 8f;
        public float weight = 1200f;

        [Header("Physics Setup")]
        public float centerOfMassY = -0.5f;
        public float downforce = 2000f;

        [Header("Unlock")]
        public int unlockCost;
        public bool isDLC;
        public string dlcPackID;
    }
}
