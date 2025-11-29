// 01_TimeSources.cs
// 시간 소스: 싱글(Local) / 멀티(Photon)
using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace R1
{
    public class LocalTimeSource : ITimeSource
    {
        public double Now => Time.timeAsDouble;
    }


#if PHOTON_UNITY_NETWORKING
    public class PhotonTimeSource : ITimeSource
    {
        public double Now => PhotonNetwork.Time;
    }
#endif
    public class TimeSources : MonoBehaviour
    {

    }
}
