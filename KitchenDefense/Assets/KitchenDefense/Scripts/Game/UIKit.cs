using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 코드로 uGUI를 만드는 도우미. 프리팹/씬 편집 없이도 화면 전체가 만들어지도록
    /// 모든 UI를 여기 함수로 조립한다. 기준 해상도는 세로 1080 x 1920.
    /// </summary>
    public static class UIKit
    {
        public const int UILayer = 5;

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>부모 위쪽에 붙는 띠.</summary>
        public static RectTransform Top(RectTransform rt, float height, float top = 0f, float left = 0f, float right = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>부모 아래쪽에 붙는 띠.</summary>
        public static RectTransform Bottom(RectTransform rt, float height, float bottom = 0f, float left = 0f, float right = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
            return rt;
        }

        /// <summary>부모 기준 비율 앵커(0~1)로 붙이고 안쪽 여백을 준다.</summary>
        public static RectTransform Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float pad = 0f)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, float cornerScale = 1f)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
                img.pixelsPerUnitMultiplier = cornerScale;
            }
            return img;
        }

        /// <summary>둥근 모서리 패널. cornerScale이 클수록 모서리가 작아진다.</summary>
        public static Image Panel(Transform parent, string name, Color color, float cornerScale = 1f)
        {
            return Image(parent, name, color, SpriteFactory.UiRounded, cornerScale);
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GameFonts.Main;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            t.text = text;
            return t;
        }

        /// <summary>글자 크기를 칸에 맞춰 줄이는 라벨(한 줄 버튼·HUD용).</summary>
        public static Text FitLabel(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = Label(parent, name, text, size, color, anchor);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(16, size / 2);
            t.resizeTextMaxSize = size;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, int size, Color bg, Color fg, UnityAction onClick)
        {
            var img = Panel(parent, name, bg, 1.3f);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            if (onClick != null) btn.onClick.AddListener(onClick);
            if (label != null)
            {
                var t = FitLabel(img.transform, "Label", label, size, fg, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, 14f, 6f, 14f, 6f);
            }
            return btn;
        }

        public static Text LabelOf(Button b)
        {
            var t = b.transform.Find("Label");
            return t != null ? t.GetComponent<Text>() : null;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = LabelOf(b);
            if (t != null && t.text != text) t.text = text;
        }

        public static void SetText(Text t, string text)
        {
            if (t != null && t.text != text) t.text = text;
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1f, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0f)
            {
                le.preferredHeight = preferredHeight;
                le.minHeight = preferredHeight;
            }
            if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing, int padX, int padY, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padX, padX, padY, padY);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing, int padX, int padY, bool expand = true)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padX, padX, padY, padY);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expand;
            h.childForceExpandHeight = true;
            return h;
        }

        /// <summary>세로 스크롤 영역. 반환값(Content)에 VerticalLayoutGroup이 붙어 있어 자식을 그냥 넣으면 된다.</summary>
        public static RectTransform ScrollContent(RectTransform area, float spacing, int pad)
        {
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;

            var viewport = Node("Viewport", area);
            Stretch(viewport);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VStack(content.gameObject, spacing, pad, pad, TextAnchor.UpperCenter);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
