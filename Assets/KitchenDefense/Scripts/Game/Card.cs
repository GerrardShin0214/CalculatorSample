using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 낮 화면의 카드 한 장: 제목(위) + 스크롤 본문 + 버튼(아래 절반, 엄지 닿는 곳).
    /// 기획서: "메뉴, 영업 결과, 상점이 한 장씩 넘어가는 카드형. 버튼은 모두 화면 아래쪽 절반에 둔다."
    /// </summary>
    public sealed class Card
    {
        public readonly RectTransform Root;
        public readonly RectTransform Body;
        public readonly RectTransform Footer;
        public readonly Text Title;
        public readonly Text Subtitle;

        public const int TitleSize = 64;
        public const int BodySize = 36;
        public const int SmallSize = 30;

        public Card(RectTransform parent, string title, string subtitle, float footerHeight = 150f, bool vertical = false)
        {
            Root = UIKit.Node("Card", parent);
            UIKit.Stretch(Root);

            var header = UIKit.Node("Header", Root);
            UIKit.Top(header, 190f, 10f, 44f, 44f);
            Title = UIKit.FitLabel(header, "Title", title, TitleSize, Theme.Ink, TextAnchor.LowerLeft);
            UIKit.Top(Title.rectTransform, 100f, 10f);
            Subtitle = UIKit.FitLabel(header, "Subtitle", subtitle, 34, Theme.InkSoft, TextAnchor.UpperLeft);
            UIKit.Top(Subtitle.rectTransform, 70f, 116f);

            Footer = UIKit.Node("Footer", Root);
            UIKit.Bottom(Footer, footerHeight, 36f, 40f, 40f);
            if (vertical) UIKit.VStack(Footer.gameObject, 18f, 0, 0, TextAnchor.LowerCenter).childForceExpandHeight = true;
            else UIKit.HStack(Footer.gameObject, 22f, 0, 0);

            var area = UIKit.Node("BodyArea", Root);
            UIKit.Stretch(area, 20f, 206f, 20f, footerHeight + 56f);
            Body = UIKit.ScrollContent(area, 20f, 18);
        }

        public RectTransform Box(Color color, float spacing = 10f, int pad = 28)
        {
            var img = UIKit.Panel(Body, "Box", color, 1.2f);
            UIKit.VStack(img.gameObject, spacing, pad, pad - 4);
            return img.rectTransform;
        }

        public Text Text(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            return UIKit.Label(parent, "Text", text, size, color, anchor);
        }

        public Text BodyText(Transform parent, string text) { return Text(parent, text, BodySize, Theme.Ink); }

        public Text Heading(Transform parent, string text, Color color)
        {
            var t = Text(parent, text, 40, color);
            t.fontStyle = FontStyle.Bold;
            return t;
        }

        public Button FooterButton(string label, Color color, UnityAction onClick)
        {
            return UIKit.Button(Footer, "Button", label, 46, color, Color.white, onClick);
        }

        /// <summary>박스 안 한 줄짜리 버튼(상점 구매 등).</summary>
        public Button RowButton(Transform parent, string label, Color color, UnityAction onClick, float height = 96f)
        {
            var b = UIKit.Button(parent, "RowButton", label, 36, color, Color.white, onClick);
            UIKit.Layout(b, height);
            return b;
        }
    }
}
