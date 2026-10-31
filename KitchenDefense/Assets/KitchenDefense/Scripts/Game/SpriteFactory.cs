using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 아트 리소스 없이 쓰는 절차적 스프라이트(흰색, 색은 SpriteRenderer/Image에서 입힌다).
    /// 월드용은 1 유닛 = 1칸 크기, UI용은 9-slice 둥근 사각형.
    /// </summary>
    public static class SpriteFactory
    {
        enum Shape { Circle, Ring, Rounded, RoundedOutline }

        static Sprite circle;
        static Sprite ring;
        static Sprite rounded;
        static Sprite roundedOutline;
        static Sprite square;
        static Sprite uiRounded;

        public static Sprite Circle { get { if (circle == null) circle = Make(128, Shape.Circle, 0f, 0, 128f); return circle; } }
        public static Sprite Ring { get { if (ring == null) ring = Make(128, Shape.Ring, 10f, 0, 128f); return ring; } }
        public static Sprite Rounded { get { if (rounded == null) rounded = Make(128, Shape.Rounded, 22f, 0, 128f); return rounded; } }
        public static Sprite RoundedOutline { get { if (roundedOutline == null) roundedOutline = Make(128, Shape.RoundedOutline, 22f, 0, 128f); return roundedOutline; } }
        public static Sprite Square { get { if (square == null) square = MakeSquare(); return square; } }

        /// <summary>UI용 둥근 사각형(9-slice). Image.pixelsPerUnitMultiplier로 모서리 크기를 조절한다.</summary>
        public static Sprite UiRounded { get { if (uiRounded == null) uiRounded = Make(128, Shape.Rounded, 30f, 40, 100f); return uiRounded; } }

        static Sprite Make(int size, Shape shape, float param, int border, float pixelsPerUnit)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "KD_" + shape;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float edge = half - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - half;
                    float fy = y + 0.5f - half;
                    float a;
                    switch (shape)
                    {
                        case Shape.Circle:
                        {
                            float d = Mathf.Sqrt(fx * fx + fy * fy);
                            a = Mathf.Clamp01(edge - d + 0.5f);
                            break;
                        }
                        case Shape.Ring:
                        {
                            float d = Mathf.Sqrt(fx * fx + fy * fy);
                            a = Mathf.Clamp01(Mathf.Min(edge - d, d - (edge - param)) + 0.5f);
                            break;
                        }
                        default:
                        {
                            float r = param;
                            float qx = Mathf.Abs(fx) - (edge - r);
                            float qy = Mathf.Abs(fy) - (edge - r);
                            float ox = Mathf.Max(qx, 0f);
                            float oy = Mathf.Max(qy, 0f);
                            float dist = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                            a = Mathf.Clamp01(0.5f - dist);
                            if (shape == Shape.RoundedOutline) a = Mathf.Min(a, Mathf.Clamp01(dist + 8.5f));
                            break;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var b = new Vector4(border, border, border, border);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, b);
        }

        static Sprite MakeSquare()
        {
            const int size = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "KD_Square";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, Vector4.zero);
        }
    }

    /// <summary>월드 공간 글자(TextMesh). height는 월드 단위 글자 높이.</summary>
    public static class WorldText
    {
        const int FontSize = 64;

        public static TextMesh Create(Transform parent, string name, string text, float height, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = GameFonts.Main;
            tm.fontSize = FontSize;
            tm.characterSize = height * 10f / FontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameFonts.Main.material;
            mr.sortingOrder = sortingOrder;
            return tm;
        }

        public static void SetHeight(TextMesh tm, float height)
        {
            tm.characterSize = height * 10f / FontSize;
        }
    }
}
