using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace R1
{
    /// <summary>
    /// Telemetry adapter for the HUD.
    /// Initially disabled until RaceSessionManager binds a CarController as the data source.
    /// In multiplayer, disables itself for remote cars (HUD telemetry not required).
    /// </summary>
    public class CarTelemetryAdapter : MonoBehaviour, ICarTelemetry
    {
        /// <summary>Current speed in km/h.</summary>
        public float SpeedKmh => source ? source.currentSpeed : 0f;

        /// <summary>Current engine RPM.</summary>
        public float EngineRpm => source ? source.currentEngineRPM : 0f;
        
        /// <summary>Current gear index (0-based).</summary>
        public int GearIndex => source ? source.gearNum : 0;
        
        /// <summary>True if the car is currently reversing.</summary>
        public bool IsReverse => source && source.reverse;
        
        /// <summary>Nitro value (remaining nitrous energy).</summary>
        public float Nitro => source ? source.nitrusValue : 0f;

        [SerializeField] private CarController source;

#if PHOTON_UNITY_NETWORKING
        private PhotonView pv;
#endif

        private void Awake()
        {
#if PHOTON_UNITY_NETWORKING
            pv = GetComponent<PhotonView>();
            if (pv != null && PhotonNetwork.InRoom && !pv.IsMine)
            {
                // Remote vehicles do not need HUD telemetry
                enabled = false;
            }
#endif
        }


        /// <summary>
        /// Bind a CarController as the telemetry source.
        /// </summary>
        /// <param name="controller">CarController providing speed, RPM, gear, etc.</param>
        public void Bind(CarController controller)
        {
            source = controller;
        }

        
    }
}
