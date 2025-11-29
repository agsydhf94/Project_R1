using System;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Holds and manages a player's identity (UserId, DisplayName, IsLocal).
    /// Raises change events when profile data is updated.
    /// </summary>
    public class PlayerIdentity : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private UserProfile profile;

        /// <summary>
        /// Raised when the profile changes (e.g., spawn, nickname update).
        /// </summary>
        public event Action<PlayerIdentity> OnProfileChanged;

        /// <summary>Read-only UserId of the player.</summary>
        public string UserId => profile.userId;

        /// <summary>Read-only display name of the player.</summary>
        public string DisplayName => profile.displayName;

        /// <summary>True if this player is the local user.</summary>
        public bool IsLocal => profile.isLocal;

        /// <summary>Get or set the entire profile object.</summary>
        public UserProfile Profile
        {
            get => profile;
            set { profile = value; RaiseChanged(); }
        }

        /// <summary>
        /// Helper to set full profile fields at once.
        /// </summary>
        /// <param name="userId">Unique user identifier</param>
        /// <param name="displayName">Display name</param>
        /// <param name="isLocal">Whether this is the local user</param>
        public void SetProfile(string userId, string displayName, bool isLocal)
        {
            profile.userId = userId;
            profile.displayName = displayName;
            profile.isLocal = isLocal;
            RaiseChanged();
        }

        /// <summary>
        /// Set only the UserId and raise change event.
        /// </summary>
        public void SetUserId(string userId)
        {
            profile.userId = userId; RaiseChanged();
        }


        /// <summary>
        /// Set only the display name and raise change event.
        /// </summary>
        public void SetDisplayName(string displayName)
        {
            profile.displayName = displayName; RaiseChanged();
        }


        /// <summary>
        /// Set only the local flag and raise change event.
        /// </summary>
        public void SetIsLocal(bool isLocal)
        {
            profile.isLocal = isLocal; RaiseChanged();
        }

        void OnValidate()
        {
            // Convenience defaults
            if (string.IsNullOrWhiteSpace(profile.userId))
                profile.userId = profile.isLocal ? "LOCAL" : gameObject.name;

            if (string.IsNullOrWhiteSpace(profile.displayName))
                profile.displayName = gameObject.name;
        }

        private void RaiseChanged() => OnProfileChanged?.Invoke(this);
    }
}
