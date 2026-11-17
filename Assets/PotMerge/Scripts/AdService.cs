using System;
using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Rewarded ads. No ad SDK is connected yet: in this placeholder mode the reward is granted right away
    /// so the flow can be tested. Replace ShowRewarded with the real SDK call (e.g. Unity LevelPlay) later.
    /// </summary>
    public static class AdService
    {
        public static bool IsPlaceholder => true;

        public static void ShowRewarded(Action onRewarded, Action onFailed = null)
        {
            if (IsPlaceholder)
            {
                Debug.Log("[PotMerge] Rewarded ad placeholder: granting reward without an ad.");
                onRewarded?.Invoke();
                return;
            }
            onFailed?.Invoke();
        }
    }
}
