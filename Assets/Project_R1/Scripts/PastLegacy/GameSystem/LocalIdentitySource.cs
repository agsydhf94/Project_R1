using UnityEngine;

namespace R1
{
    public class LocalIdentitySource : MonoBehaviour
    {
        public string LocalUserId { get; }
        public string LocalDisplayName { get; }

        public LocalIdentitySource(string localName = "Player")
        {
            LocalUserId = "LOCAL";
            LocalDisplayName = localName;
        }

        public string GetDisplayName(string userId)
        {
            return userId == LocalUserId ? LocalDisplayName : userId;
        }
    }
}
