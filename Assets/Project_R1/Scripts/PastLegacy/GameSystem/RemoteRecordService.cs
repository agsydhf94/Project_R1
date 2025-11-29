using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace R1
{
    public class RemoteRecordService : IRecordService
    {
        public RaceRecord PersonalBest { get; private set; }
        public RaceRecord CurrentRun { get; private set; }
        public RaceRecord OverallBest { get; private set; }
        public IReadOnlyList<RaceRecord> Leaderboard { get; private set; } = new List<RaceRecord>();

        public event System.Action<IReadOnlyList<RaceRecord>> OnLeaderboardUpdated;

        private string _userId;
        private string _playerName;

        public void StartRun(string playerId, string playerName)
        {
            _userId     = playerId;
            _playerName = string.IsNullOrEmpty(playerName) ? playerId : playerName;
            CurrentRun  = new RaceRecord(_userId, _playerName);
        }

        public void CompleteLap(float lapTime, IReadOnlyList<float> currentLapSplits)
        {
            if (CurrentRun == null) return;

            CurrentRun.lapTimes.Add(lapTime);
            if (lapTime < CurrentRun.bestLap) CurrentRun.bestLap = lapTime;

            // (선택) currentLapSplits도 서버에 보낼 예정이면 내부 큐에 보관하거나 즉시 업로드 예약

            var shadow = CurrentRun.Clone();
            shadow.bestTotal = float.PositiveInfinity;
            OnLeaderboardUpdated?.Invoke(new List<RaceRecord> { shadow });
        }

        public void FinishRun() => FinishRunAsync().Forget();

        public async UniTask FinishRunAsync()
        {
            if (CurrentRun == null) return;

            float total = 0f;
            foreach (var t in CurrentRun.lapTimes) total += t;

            CurrentRun.bestLap   = CurrentRun.lapTimes.Count > 0 ? Mathf.Min(CurrentRun.lapTimes.ToArray()) : 0f;
            CurrentRun.bestTotal = total;

            await UploadToServerAsync(CurrentRun);

            var leaderboard = await DownloadLeaderboardAsync();
            Leaderboard = leaderboard.OrderBy(r => r.bestTotal).ToList();
            OverallBest = Leaderboard.Count > 0 ? Leaderboard[0] : null;

            // 내 기록 찾아서 PB 표시용으로 보관
            PersonalBest = Leaderboard.FirstOrDefault(r => r.userId == _userId);

            OnLeaderboardUpdated?.Invoke(Leaderboard);
        }

        // ---- 서버 I/O (stub) ----
        private async UniTask UploadToServerAsync(RaceRecord record) { await UniTask.Yield(); }
        private async UniTask<List<RaceRecord>> DownloadLeaderboardAsync()
        {
            await UniTask.Yield();
            return new List<RaceRecord>();
        }
    }
}
