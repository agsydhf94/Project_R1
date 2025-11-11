using UnityEngine;
using UnityEngine.AddressableAssets;

namespace R1
{
    [CreateAssetMenu(fileName = "New Championship Data", menuName = "ScriptableObjects/ChampionshipData")]
    public class ChampionshipData : ScriptableObject
    {
        public string championshipID;
        public string displayName;
        public AssetReference bannerImage;

        public TrackData[] tracks;
        public int requiredPoints;

        [Header("Rewards")]
        public int prizeMoney;
        public CarData[] unlockedCars;
    }
}
