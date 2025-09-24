using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace R1
{
    /// <summary>
    /// Initializes the main menu by ensuring Addressables assets are preloaded
    /// before enabling track selection buttons.
    /// </summary>
    public class MenuInitializer : MonoBehaviour
    {
        /// <summary>
        /// Addressables preloader that handles async asset caching.
        /// </summary>
        [SerializeField] private AddressablesPreloader preloader;

        /// <summary>
        /// Buttons for track selection that will be gated until preload finishes.
        /// </summary>
        [SerializeField] private Button[] trackButtons;


        /// <summary>
        /// Disables all track buttons and attempts to find a preloader if not assigned.
        /// </summary>
        private void Awake()
        {
            if (preloader == null)
            {
                preloader = FindObjectOfType<AddressablesPreloader>();
            }
            foreach (var b in trackButtons)
            {
                if (b != null) b.interactable = false;
            }
        }


        /// <summary>
        /// Starts the preload coroutine.
        /// </summary>
        private void Start()
        {
            StartCoroutine(Co_Preload());
        }


        /// <summary>
        /// Coroutine that runs Addressables preload, then re-enables track buttons.
        /// </summary>
        private IEnumerator Co_Preload()
        {
            if (preloader != null)
            {
                yield return preloader.PreloadAll();
            }
            foreach (var b in trackButtons)
            {
                if (b != null) b.interactable = true;
            }
        }
    }
}
