using KitchenDefense.Core;
using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>색과 글꼴. 아트 없이 회색 박스(색 박스)로 가는 프로토타입이라 색이 곧 정보다.</summary>
    public static class Theme
    {
        public static Color Hex(uint rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        }

        /// <summary>리치 텍스트용 "#RRGGBB".</summary>
        public static string Html(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        public static string Colorize(string text, Color c) { return "<color=" + Html(c) + ">" + text + "</color>"; }

        // 보드
        public static readonly Color Background = Hex(0x1E1714);
        public static readonly Color BoardFrame = Hex(0x2C221E);
        public static readonly Color Floor = Hex(0x3E332D);
        public static readonly Color FloorAlt = Hex(0x453830);
        public static readonly Color Path = Hex(0x8B6B50);
        public static readonly Color PathDot = Hex(0xB8936F, 0.55f);
        public static readonly Color Entrance = Hex(0x3C7D4C);
        public static readonly Color Storage = Hex(0x3F6E8F);
        public static readonly Color Valid = Hex(0x7CFC9A, 0.32f);
        public static readonly Color Selection = Hex(0xFFE082);

        // 화면
        public static readonly Color Paper = Hex(0xFBF1DF);
        public static readonly Color Card = Hex(0xFFFFFF);
        public static readonly Color CardAlt = Hex(0xF3E6D0);
        public static readonly Color Ink = Hex(0x3A2A1F);
        public static readonly Color InkSoft = Hex(0x7D6656);
        public static readonly Color Accent = Hex(0xF57C2B);
        public static readonly Color AccentSoft = Hex(0xFFE3C7);
        public static readonly Color Blue = Hex(0x4F86E8);
        public static readonly Color Good = Hex(0x3FA34D);
        public static readonly Color Bad = Hex(0xE5533D);
        public static readonly Color Gold = Hex(0xE7A11B);
        public static readonly Color Muted = Hex(0xB9AEA3);
        public static readonly Color Dim = Hex(0x000000, 0.55f);

        // 이야기 장면(자정의 주방)
        public static readonly Color StoryNight = Hex(0x14182B);
        public static readonly Color StoryPanel = Hex(0x252C4A);
        public static readonly Color StoryText = Hex(0xF5F0E6);

        // 밤 HUD
        public static readonly Color HudBg = Hex(0x15100E, 0.94f);
        public static readonly Color HudPanel = Hex(0x2A211D);
        public static readonly Color HudText = Hex(0xFFF3E0);
        public static readonly Color HudSoft = Hex(0xC7B5A5);
        public static readonly Color Heart = Hex(0xFF6B6B);
        public static readonly Color Fire = Hex(0xFF9F43);
        public static readonly Color Star = Hex(0xFFD54F);

        public static Color ToolColor(ToolType t) { return Hex(GameData.Tool(t).color); }
        public static Color MonsterColor(MonsterType t) { return Hex(GameData.Monster(t).color); }

        public static Color MethodColor(CookMethod m)
        {
            switch (m)
            {
                case CookMethod.Prep: return Hex(0xF48FB1);
                case CookMethod.Grill: return Hex(0xFF8A50);
                case CookMethod.Freeze: return Hex(0x81D4FA);
                case CookMethod.Boil: return Hex(0x9CCC65);
                default: return Muted;
            }
        }

        public static Color IngredientColor(string id)
        {
            var ing = GameData.Ingredient(id);
            return ing.scraps ? Muted : MethodColor(ing.method);
        }
    }

    /// <summary>
    /// 한글 글꼴. Resources/Fonts/Jua-Regular(배달의민족 주아체, OFL)를 쓰고,
    /// 없으면 내장 글꼴로 대신한다(OS 대체 글꼴로 한글이 나온다).
    /// </summary>
    public static class GameFonts
    {
        static Font main;

        public static Font Main
        {
            get
            {
                if (main == null)
                {
                    main = Resources.Load<Font>("Fonts/Jua-Regular");
                    if (main == null)
                    {
#if UNITY_2022_2_OR_NEWER
                        main = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                        main = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                    }
                }
                return main;
            }
        }
    }
}
