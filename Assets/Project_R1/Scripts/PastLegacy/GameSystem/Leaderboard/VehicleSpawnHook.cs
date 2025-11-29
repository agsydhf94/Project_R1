#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using UnityEngine;

namespace R1
{
    public class VehicleSpawnHook : MonoBehaviour
    {
        // AI 이름 규칙
        private const string AiUidPrefix     = "AI_";
        private const string AiDisplayPrefix = "AI Player #";
        private static int _aiSeq = 1;

        private bool _registered;

        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            // 1) instantiationData 파싱 (선택적)
            bool?   isAiArg     = null;
            int     gridIndex   = int.MaxValue;
            string  userIdArg   = null;
            string  displayArg  = null;

            var view = GetComponent<PhotonView>();
            object[] data = view?.InstantiationData;

            // 권장: PhotonNetwork.Instantiate 할 때 new object[]{ isAI, gridIndex, userId, displayName }
            if (data != null)
            {
                if (data.Length > 0 && data[0] is bool b)      isAiArg = b;
                if (data.Length > 1 && data[1] is int gi)      gridIndex = gi;
                if (data.Length > 2 && data[2] is string uid)  userIdArg = uid;
                if (data.Length > 3 && data[3] is string nick) displayArg = nick;
            }

            // 2) 사람/AI 판별
            bool isAI = isAiArg ?? gameObject.CompareTag("AI");

            // Photon Owner 정보(사람일 때)
            var owner = view ? view.Owner : null;
            bool isLocal = owner != null && owner == PhotonNetwork.LocalPlayer;

            string userId   = userIdArg   ?? (owner?.UserId   ?? null);
            string nickname = displayArg  ?? (owner?.NickName ?? null);

            // 3) ID/이름 세팅 (AI는 규칙 적용, 사람은 Photon 정보 우선)
            var ident = gameObject.GetComponent<PlayerIdentity>();
            if (!ident) ident = gameObject.AddComponent<PlayerIdentity>();

            if (isAI)
            {
                string uid = string.IsNullOrEmpty(userId)   ? $"{AiUidPrefix}{_aiSeq}"     : userId;
                string nm  = string.IsNullOrEmpty(nickname) ? $"{AiDisplayPrefix}{_aiSeq}" : nickname;
                ident.SetProfile(uid, nm, false);
                _aiSeq++;
            }
            else
            {
                // 사람(로컬/원격): 입력 닉/스토브 연동 시 여기 값만 교체하면 됨
                string uid = !string.IsNullOrEmpty(userId) ? userId : (isLocal ? "LOCAL" : gameObject.name);
                string nm  = !string.IsNullOrEmpty(nickname) ? nickname : gameObject.name;
                ident.SetProfile(uid, nm, isLocal);
            }

            // 4) GridSlot 주입
            var slot = gameObject.GetComponent<GridSlot>() ?? gameObject.AddComponent<GridSlot>();
            slot.index = gridIndex;

            // 5) TrackContext → ProgressTracker/LapTracker 주입
            InjectTrackDeps();

            // 6) 리더보드 등록
            //RegisterToLeaderboard();
        }

        private void InjectTrackDeps()
        {
            var ctx = TrackContext.Instance;
            var prog = GetComponent<ProgressTracker>();
            var lap  = GetComponent<LapTracker>();

            if (ctx && prog)
            {
                // 부트스트래퍼 주입 필드 유지
                prog.checkpoints = ctx.checkpoints;
                prog.lap = lap;
            }

            if (ctx && lap)
            {
                lap.Init(ctx.checkpoints != null ? ctx.checkpoints.Length : 0);
                lap.targetTotalLaps = ctx.targetTotalLaps;
            }
        }

        /*
        private void RegisterToLeaderboard()
        {
            if (_registered) return;
            var adapter = FindObjectOfType<LeaderboardAdapter>();
            if (adapter)
            {
                adapter.RegisterParticipant(gameObject);
                _registered = true;
            }
        }
        */

        /*
        // 오프라인/에디터 테스트(Photon 미사용)에서도 동작하게 보강
        void Start()
        {
            if (!_registered)
            {
                // Tag "AI" 붙어 있으면 AI로 간주하여 이름 보정
                var id = GetComponent<PlayerIdentity>() ?? gameObject.AddComponent<PlayerIdentity>();
                if (string.IsNullOrEmpty(id.PlayerId) || string.IsNullOrEmpty(id.DisplayName))
                {
                    bool isAI = gameObject.CompareTag("AI");
                    if (isAI)
                    {
                        string uid = $"{AiUidPrefix}{_aiSeq}";
                        string nm  = $"{AiDisplayPrefix}{_aiSeq}";
                        id.SetProfile(uid, nm, false);
                        _aiSeq++;
                    }
                    else
                    {
                        id.SetProfile(id.IsLocal ? "LOCAL" : gameObject.name, gameObject.name, id.IsLocal);
                    }
                }

                InjectTrackDeps();
                RegisterToLeaderboard();
            }
        }
        */
    }
}
#endif