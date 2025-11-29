// 02_StateSync.cs
// ���̽� ���� ����ȭ: �̱�(Local) / ��Ƽ(Photon RoomProperties)
using System;
using UnityEngine;


#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using ExitGames.Client.Photon;
#endif

namespace R1
{
    // ---- Local(�̱�) ----
    public sealed class LocalStateSync : IStateSync
    {
        public RaceState State { get; private set; } = RaceState.PreRace;
        public double StartTime { get; private set; } = 0;
        public event Action<RaceState> OnStateChanged;
        public event Action<double> OnStartTimeChanged;


        public void ScheduleCountdown(double startAtAbsolute)
        {
            StartTime = startAtAbsolute;
            OnStartTimeChanged?.Invoke(StartTime);
            SetState(RaceState.Countdown);
        }


        public void SetState(RaceState s)
        {
            if (State == s) return;
            State = s;
            OnStateChanged?.Invoke(State);
        }
    }

#if PHOTON_UNITY_NETWORKING
    // ---- Photon(PUN) ----
    public static class RoomKeys
    {
        public const string RaceState = "raceState"; // byte
        public const string StartTime = "raceStartTime"; // double(PhotonNetwork.Time)
    }


    public sealed class PhotonRoomStateSync : IStateSync
    {
        public RaceState State { get; private set; } = RaceState.PreRace;
        public double StartTime { get; private set; } = 0;
        public event Action<RaceState> OnStateChanged;
        public event Action<double> OnStartTimeChanged;


        public PhotonRoomStateSync()
        {
            ReadFromRoom();
            PhotonNetwork.AddCallbackTarget(new CallbackProxy(this));
        }


        public void ScheduleCountdown(double startAtAbsolute)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            var rp = PhotonNetwork.CurrentRoom.CustomProperties ?? new Hashtable();
            rp[RoomKeys.StartTime] = startAtAbsolute;
            rp[RoomKeys.RaceState] = (byte)RaceState.Countdown;
            PhotonNetwork.CurrentRoom.SetCustomProperties(rp);
        }


        public void SetState(RaceState s)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            var rp = PhotonNetwork.CurrentRoom.CustomProperties ?? new Hashtable();
            rp[RoomKeys.RaceState] = (byte)s;
            PhotonNetwork.CurrentRoom.SetCustomProperties(rp);
        }


        private void ReadFromRoom()
        {
            var rp = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (rp == null) return;
            if (rp.ContainsKey(RoomKeys.StartTime))
            {
                StartTime = (double)rp[RoomKeys.StartTime];
                OnStartTimeChanged?.Invoke(StartTime);
            }
            if (rp.ContainsKey(RoomKeys.RaceState))
            {
                var st = (RaceState)(byte)rp[RoomKeys.RaceState];
                if (st != State) { State = st; OnStateChanged?.Invoke(State); }
            }
        }


        private sealed class CallbackProxy : MonoBehaviourPunCallbacks
        {
            private readonly PhotonRoomStateSync owner;
            public CallbackProxy(PhotonRoomStateSync o) => owner = o;
            public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
            {
                owner.ReadFromRoom();
            }
        }
#endif
        public class StateSync : MonoBehaviour
        {

        }
    }
}


