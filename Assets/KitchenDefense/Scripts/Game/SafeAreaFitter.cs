using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>노치·홈 인디케이터를 피하도록 Screen.safeArea에 맞춘다.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect lastSafe;
        int lastWidth;
        int lastHeight;

        void Awake() { Apply(); }

        void Update()
        {
            if (Screen.safeArea != lastSafe || Screen.width != lastWidth || Screen.height != lastHeight) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            lastSafe = Screen.safeArea;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            var min = lastSafe.position;
            var max = lastSafe.position + lastSafe.size;
            rt.anchorMin = new Vector2(min.x / lastWidth, min.y / lastHeight);
            rt.anchorMax = new Vector2(max.x / lastWidth, max.y / lastHeight);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
