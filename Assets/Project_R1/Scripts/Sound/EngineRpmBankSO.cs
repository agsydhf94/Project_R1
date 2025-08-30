using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// ScriptableObject that defines a sound bank of engine RPM samples.
    /// Each sample corresponds to a specific RPM step, used for blending audio playback.
    /// </summary>
    [CreateAssetMenu(fileName = "EngineRpmBank", menuName = "R1/Engine RPM Sound Bank", order = 1)]
    public class EngineRpmBankSO : ScriptableObject
    {
        /// <summary>
        /// The RPM interval (step size) between each audio sample in the bank.
        /// </summary>
        public int stepRpm = 500;

        /// <summary>
        /// List of audio clips representing sampled engine sounds at increasing RPM levels.
        /// </summary>
        public List<AudioClip> rpmSamples = new List<AudioClip>();
    }
}
