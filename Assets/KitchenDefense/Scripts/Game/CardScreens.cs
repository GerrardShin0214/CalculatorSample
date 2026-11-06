using System.Collections.Generic;
using System.Text;
using KitchenDefense.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KitchenDefense.Game
{
    /// <summary>타이틀 · 개업 전야 · 밤 결과 · 낮(메뉴/영업 결과/상점/마감) · 시즌 결과 · 일시정지 · 게임 방법.</summary>
    public sealed class CardScreens
    {
        readonly GameController ctl;
        readonly RectTransform root;
        readonly RectTransform overlayRoot;
        readonly Image backdrop;
        Card current;
        GameObject overlay;

        // 메뉴 카드 부분 갱신용
        sealed class DishRow
        {
            public string recipeId;
            public Image bg;
            public Image indicator;
            public Text status;
            public int servings;
        }

        readonly List<DishRow> dishRows = new List<DishRow>();
        Text menuHeader;

        // 상점 카드 부분 갱신용(구매해도 목록이 맨 위로 튀지 않게 다시 만들지 않는다)
        sealed class ShopRow
        {
            public ShopOffer offer;
            public Text info;
            public Button button;
        }

        readonly List<ShopRow> shopRows = new List<ShopRow>();
        Text repairInfo;
        Button repairButton;
        Text shopHint;

        UnityAction helpBack;

        // 이야기 장면(대사 한 줄씩, 탭해서 넘김)
        GameObject storyGo;
        StoryBeat storyBeat;
        int storyIndex;
        bool storyOnGoal;
        UnityAction storyDone;
        string storyDoneLabel;
        RectTransform portraitRoot;
        Image portrait;
        Text portraitGlyph;
        Text storySpeaker;
        Text storyText;
        Button storyNext;
        string typeFull = "";
        float typeTime;
        bool typing;
        const float TypeSpeed = 40f;

        public bool StoryOpen { get { return storyGo != null; } }

        /// <summary>'게임 방법' 카드가 떠 있는가(뒤로 버튼이 여기서 돌아가게).</summary>
        public bool HelpOpen { get { return helpBack != null; } }

        public void BackFromHelp()
        {
            var back = helpBack;
            if (back != null) back();
        }

        public CardScreens(GameController ctl, RectTransform root, RectTransform overlayRoot, Image backdrop)
        {
            this.ctl = ctl;
            this.root = root;
            this.overlayRoot = overlayRoot;
            this.backdrop = backdrop;
        }

        public bool HasOverlay { get { return overlay != null; } }

        public void Hide()
        {
            if (current != null) Object.Destroy(current.Root.gameObject);
            current = null;
            dishRows.Clear();
            menuHeader = null;
            shopRows.Clear();
            repairInfo = null;
            repairButton = null;
            shopHint = null;
            helpBack = null;
            if (storyGo != null) Object.Destroy(storyGo);
            storyGo = null;
            storyBeat = null;
            storyDone = null;
            typing = false;
            backdrop.color = Theme.Paper;
            if (backdrop.gameObject.activeSelf) backdrop.gameObject.SetActive(false);
        }

        public void HideOverlay()
        {
            if (overlay != null) Object.Destroy(overlay);
            overlay = null;
        }

        Card New(string title, string subtitle, float footerHeight = 150f, bool vertical = false)
        {
            Hide();
            backdrop.gameObject.SetActive(true);
            current = new Card(root, title, subtitle, footerHeight, vertical);
            return current;
        }

        RunState Run { get { return ctl.Session.Run; } }

        string StatusLine()
        {
            var r = Run;
            return Theme.Colorize(Texts.Heart + " 위생 " + r.durability + "/" + Balance.MaxDurability, Theme.Bad) + "    "
                + Theme.Colorize("골드 " + r.gold + "G", Theme.Gold) + "    "
                + Theme.Colorize(Texts.Star + " 평판 " + r.reputation, Theme.Hex(0xC79100));
        }

        // ───────────── 이야기 장면 ─────────────

        /// <summary>
        /// 스테이지(밤) 사이에 보여 주는 이야기. 대사를 한 줄씩 타자 치듯 보여 주고, 아무 데나 탭하면 넘어간다.
        /// 마지막엔 "오늘의 목표"를 보여 주고 doneLabel 버튼으로 onDone을 부른다.
        /// </summary>
        public void ShowStory(StoryBeat beat, string chapter, string countdown, string doneLabel, UnityAction onDone)
        {
            Hide();
            if (beat == null || beat.lines == null || beat.lines.Length == 0)
            {
                if (onDone != null) onDone();
                return;
            }
            storyBeat = beat;
            storyIndex = 0;
            storyOnGoal = false;
            storyDone = onDone;
            storyDoneLabel = doneLabel;
            backdrop.color = Theme.StoryNight;
            backdrop.gameObject.SetActive(true);

            var rootRt = UIKit.Node("Story", root);
            UIKit.Stretch(rootRt);
            storyGo = rootRt.gameObject;

            // 화면 아무 데나 탭 = 다음
            var tap = UIKit.Image(rootRt, "Tap", new Color(0f, 0f, 0f, 0f));
            UIKit.Stretch(tap.rectTransform);
            var tapButton = tap.gameObject.AddComponent<Button>();
            tapButton.transition = Selectable.Transition.None;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            tapButton.navigation = nav;
            tapButton.onClick.AddListener(AdvanceStory);

            var chapterText = UIKit.FitLabel(rootRt, "Chapter", chapter, 36, Theme.HudSoft, TextAnchor.MiddleLeft);
            UIKit.Top(chapterText.rectTransform, 50f, 40f, 48f, 280f);
            var titleText = UIKit.FitLabel(rootRt, "Title", beat.title, 76, Theme.StoryText, TextAnchor.MiddleLeft);
            UIKit.Top(titleText.rectTransform, 104f, 92f, 48f, 48f);
            var countdownText = UIKit.FitLabel(rootRt, "Countdown", countdown, 34, Theme.Star, TextAnchor.MiddleLeft);
            UIKit.Top(countdownText.rectTransform, 50f, 200f, 48f, 48f);

            var skip = UIKit.Button(rootRt, "Skip", "건너뛰기", 34, Theme.StoryPanel, Theme.HudSoft, SkipStory);
            var srt = (RectTransform)skip.transform;
            srt.anchorMin = new Vector2(1f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.sizeDelta = new Vector2(220f, 80f);
            srt.anchoredPosition = new Vector2(-36f, -36f);

            // 말하는 식구의 얼굴(동그란 몸 + 눈 + 한 글자)
            portraitRoot = UIKit.Node("Portrait", rootRt);
            portraitRoot.anchorMin = new Vector2(0.5f, 0.6f);
            portraitRoot.anchorMax = new Vector2(0.5f, 0.6f);
            portraitRoot.pivot = new Vector2(0.5f, 0.5f);
            portraitRoot.sizeDelta = new Vector2(340f, 340f);
            portrait = UIKit.Image(portraitRoot, "Face", Color.white, SpriteFactory.Circle);
            UIKit.Stretch(portrait.rectTransform);
            portrait.raycastTarget = false;
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -70f : 70f;
                var eye = UIKit.Image(portraitRoot, "Eye", Color.white, SpriteFactory.Circle);
                eye.raycastTarget = false;
                var ert = eye.rectTransform;
                ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f);
                ert.sizeDelta = new Vector2(82f, 82f);
                ert.anchoredPosition = new Vector2(x, 50f);
                var pupil = UIKit.Image(portraitRoot, "Pupil", Theme.Hex(0x1B1411), SpriteFactory.Circle);
                pupil.raycastTarget = false;
                var prt = pupil.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(38f, 38f);
                prt.anchoredPosition = new Vector2(x + 6f, 42f);
            }
            portraitGlyph = UIKit.Label(portraitRoot, "Glyph", "", 110, Theme.Ink, TextAnchor.MiddleCenter);
            var grt = portraitGlyph.rectTransform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(300f, 160f);
            grt.anchoredPosition = new Vector2(0f, -70f);

            // 대사 상자(엄지 닿는 아래쪽)
            var box = UIKit.Panel(rootRt, "Speech", Theme.StoryPanel, 1f);
            UIKit.Bottom(box.rectTransform, 580f, 40f, 32f, 32f);
            box.raycastTarget = false;
            storySpeaker = UIKit.FitLabel(box.transform, "Speaker", "", 44, Theme.Star, TextAnchor.MiddleLeft);
            UIKit.Top(storySpeaker.rectTransform, 72f, 26f, 44f, 44f);
            storyText = UIKit.Label(box.transform, "Text", "", 44, Theme.StoryText, TextAnchor.UpperLeft);
            UIKit.Stretch(storyText.rectTransform, 44f, 112f, 44f, 160f);
            storyNext = UIKit.Button(box.transform, "Next", "다음", 44, Theme.Accent, Color.white, AdvanceStory);
            UIKit.Bottom((RectTransform)storyNext.transform, 112f, 30f, 44f, 44f);

            ShowStoryLine();
        }

        void ShowStoryLine()
        {
            var line = storyBeat.lines[storyIndex];
            var speaker = Story.Speaker(line.speaker);
            bool face = speaker != null;
            if (portraitRoot.gameObject.activeSelf != face) portraitRoot.gameObject.SetActive(face);
            if (face)
            {
                portrait.color = Theme.Hex(speaker.color);
                portraitGlyph.text = speaker.glyph;
                storySpeaker.text = speaker.name;
                storySpeaker.color = Color.Lerp(Theme.Hex(speaker.color), Color.white, 0.35f);
            }
            else storySpeaker.text = "";
            storyText.color = face ? Theme.StoryText : Theme.HudSoft;
            typeFull = line.text;
            typeTime = 0f;
            typing = true;
            storyText.text = "";
            UIKit.SetLabel(storyNext, "다음");
        }

        void ShowStoryGoal()
        {
            storyOnGoal = true;
            typing = false;
            portraitRoot.gameObject.SetActive(false);
            storySpeaker.text = storyBeat.id.StartsWith("night") ? "오늘의 목표" : "한 줄 정리";
            storySpeaker.color = Theme.Fire;
            storyText.color = Theme.StoryText;
            storyText.text = storyBeat.goal;
            UIKit.SetLabel(storyNext, storyDoneLabel);
        }

        void AdvanceStory()
        {
            if (storyGo == null) return;
            if (typing)
            {
                typing = false;
                storyText.text = typeFull;
                return;
            }
            if (storyOnGoal)
            {
                FinishStory();
                return;
            }
            storyIndex++;
            if (storyIndex < storyBeat.lines.Length) ShowStoryLine();
            else ShowStoryGoal();
        }

        /// <summary>건너뛰기: 목표 화면으로 바로 가고, 이미 목표 화면이면 끝낸다.</summary>
        public void SkipStory()
        {
            if (storyGo == null) return;
            if (storyOnGoal) FinishStory();
            else ShowStoryGoal();
        }

        void FinishStory()
        {
            var done = storyDone;
            Hide();
            if (done != null) done();
        }

        /// <summary>매 프레임: 대사를 타자 치듯 한 글자씩 보여 준다.</summary>
        public void Update(float dt)
        {
            if (!typing || storyText == null) return;
            typeTime += dt;
            int n = Mathf.FloorToInt(typeTime * TypeSpeed);
            if (n >= typeFull.Length)
            {
                typing = false;
                storyText.text = typeFull;
            }
            else storyText.text = typeFull.Substring(0, n);
        }

        // ───────────── 타이틀 ─────────────

        public void ShowTitle()
        {
            var c = New("", "", 380f, true);
            UIKit.SetText(c.Title, "");
            var hero = c.Box(Theme.Paper, 6f, 10);
            var big = c.Text(hero, "막타 키친", 128, Theme.Accent, TextAnchor.MiddleCenter);
            big.fontStyle = FontStyle.Bold;
            c.Text(hero, "한밤의 주방 수비대", 56, Theme.Ink, TextAnchor.MiddleCenter);
            c.Text(hero, "사장님이 잠든 사이, 주방 식구들이 깨어난다", 34, Theme.InkSoft, TextAnchor.MiddleCenter);

            var how = c.Box(Theme.Card);
            c.Heading(how, "핵심 규칙", Theme.Accent);
            c.BodyText(how, "주방 식구(조리기구)가 곧 타워입니다. 도둑 동물을 쫓아낸 " + Theme.Colorize("막타", Theme.Accent)
                + " 식구가 동물이 떨어뜨린 음식을 제 방식대로 손질하고, 그 재료가 내일의 메뉴가 됩니다.");
            c.Text(how, "예) 멧돼지를 불판 아저씨로 막타 = 군고구마, 칼 삼형제로 막타 = 고구마 채", Card.SmallSize, Theme.InkSoft);

            int best = SaveSystem.BestScore;
            c.Text(c.Body, "최고 점수 " + best + "점  |  프로토타입 v0.1 (기획서 첫 프로토타입 범위)", 28, Theme.InkSoft, TextAnchor.MiddleCenter);

            var saved = SaveSystem.HasSave() ? SaveSystem.Load() : null;
            if (saved != null)
                c.FooterButton("이어하기 (" + PhaseLabel(saved) + ")", Theme.Good, ctl.ContinueSeason);
            c.FooterButton("새 시즌 시작", Theme.Accent, ctl.NewSeason);
            c.FooterButton("게임 방법", Theme.Hex(0x8D7B6D), () => ShowHelp(ShowTitle));
        }

        static string PhaseLabel(RunState r)
        {
            switch (r.phase)
            {
                case GamePhase.Intro: return "개업 전야";
                case GamePhase.Night:
                case GamePhase.NightResult: return r.day + "일 밤";
                case GamePhase.SeasonResult: return "시즌 결과";
                default: return r.day + "일차 낮";
            }
        }

        // ───────────── 개업 전야 ─────────────

        public void ShowIntro()
        {
            var c = New("개업 전야", "평론가 방문까지 " + Story.DaysUntilCritic(0) + "일  |  0일 밤부터 시작합니다");
            var story = c.Box(Theme.Card);
            c.BodyText(story, "찍찍단이 조리대의 음식을 낚아채 창고로 달아난다. 놈들을 쫓아내고, 떨어뜨린 음식은 그 자리에서 손질해 두자. 아침에 사장님이 그 재료로 영업한다.");
            c.Text(story, "창고가 털릴 때마다 위생이 1 깎이고, 0이 되면 영업 정지. 닷새 뒤 미식 평론가가 온다!", Card.SmallSize, Theme.InkSoft);

            var rule = c.Box(Theme.Card);
            c.Heading(rule, "막타 = 조리법", Theme.Accent);
            foreach (var def in GameData.Tools)
                c.BodyText(rule, Theme.Colorize(def.glyph, Color.Lerp(Theme.ToolColor(def.type), Theme.Ink, 0.4f)) + "  " + def.name + ": " + def.description);
            c.Text(rule, "표에 없는 조합(예: 치즈를 솥할매가 삶기)은 잡재료가 되어 오늘의 백반으로 헐값에 팔립니다.", Card.SmallSize, Theme.InkSoft);

            var ctrl = c.Box(Theme.Card);
            c.Heading(ctrl, "조작", Theme.Accent);
            c.BodyText(ctrl, "- 길 옆 빈 칸을 탭하고 아래 바에서 식구 고르기 (솥할매는 길 위)");
            c.BodyText(ctrl, "- 배치한 식구를 탭: 약불(피해 절반) / 강화 / 이동 / 회수");
            c.BodyText(ctrl, "- 동물 옆 아이콘: 지금 상태로 막타를 칠 식구 예상");
            c.BodyText(ctrl, "- 웨이브 사이 준비 시간에는 이동과 회수가 무료");

            AddOfferBox(c, "사장님의 예약 메모 (1일차)");
            c.FooterButton("거절하고 시작", Theme.Hex(0x8D7B6D), () => ctl.ResolveOfferAndStartNight(false));
            c.FooterButton("수락하고 시작", Theme.Accent, () => ctl.ResolveOfferAndStartNight(true));
        }

        void AddOfferBox(Card c, string heading)
        {
            var offer = Run.offer;
            if (offer == null || !offer.active) return;
            var box = c.Box(Theme.AccentSoft);
            c.Heading(box, heading, Theme.Accent);
            c.BodyText(box, Texts.ReservationLine(offer));
            c.Text(box, "필요한 재료:\n" + Texts.ReservationNeedsHowTo(offer), Card.SmallSize, Theme.Ink);
            c.Text(box, "받으면 오늘 밤의 두 번째 목표가 됩니다. 받고 못 지키면 평판 -1.", Card.SmallSize, Theme.InkSoft);
        }

        // ───────────── 밤 결과 ─────────────

        public void ShowNightResult()
        {
            var n = Run.night;
            var c = New(n.index + "일 밤 방어 완료", StatusLine());
            var box = c.Box(Theme.Card);
            c.Heading(box, "쫓아냄 " + n.kills + "  |  창고 털림 " + n.leaks, n.leaks > 0 ? Theme.Bad : Theme.Good);
            if (n.eaten.Count > 0) c.BodyText(box, Theme.Colorize("창고에서 털린 재료: " + Texts.Group(n.eaten), Theme.Bad));

            var haul = c.Box(Theme.Card);
            c.Heading(haul, "오늘 밤 손질한 재료 " + n.haul.Count + "개", Theme.Accent);
            c.BodyText(haul, n.haul.Count > 0 ? Texts.Group(n.haul, "\n", true) : "없음");

            var res = ctl.Session.UpcomingReservation;
            if (res != null)
            {
                var rb = c.Box(Theme.AccentSoft);
                c.Heading(rb, "내일 예약: " + Texts.RecipeName(res.recipeId) + " x" + res.servings, Theme.Accent);
                var needs = ctl.Session.UpcomingNeeds();
                bool ready = true;
                bool frozenOk = true;
                foreach (var nd in needs)
                {
                    if (nd.have < nd.needed) ready = false;
                    if (nd.have + nd.frozenSubstitutes < nd.needed) frozenOk = false;
                }
                c.BodyText(rb, ready ? Theme.Colorize("재료 준비 완료!", Theme.Good)
                    : frozenOk ? Theme.Colorize("얼린 재료로 대체하면 납품 가능 (보상 -30%)", Theme.Blue)
                    : Theme.Colorize("재료가 모자랍니다. 이대로면 예약 실패(평판 -1)", Theme.Bad));
                foreach (var nd in needs)
                    c.Text(rb, "- " + Texts.IngredientName(nd.ingredientId) + " " + Mathf.Min(nd.have, nd.needed) + "/" + nd.needed, Card.SmallSize, Theme.Ink);
            }
            c.FooterButton("낮 영업 준비", Theme.Accent, ctl.BeginDay);
        }

        // ───────────── 낮 1: 메뉴 짜기 ─────────────

        public void ShowDayMenu()
        {
            var run = Run;
            var c = New(run.day + "일차 낮  |  메뉴 짜기" + (run.IsFinalDay ? " (마지막 날)" : ""), StatusLine());

            if (run.dayState.spoiled.Count > 0)
            {
                var sp = c.Box(Theme.Card);
                c.BodyText(sp, Theme.Colorize("아침에 상한 재료: " + Texts.Group(run.dayState.spoiled), Theme.Bad));
            }

            var res = run.reservation;
            if (res.active && res.day == run.day)
            {
                var rb = c.Box(Theme.AccentSoft);
                c.Heading(rb, "오늘 예약  |  " + res.party, Theme.Accent);
                c.BodyText(rb, Texts.RecipeName(res.recipeId) + " x" + res.servings + "  |  보상 " + res.rewardGold + "G, 평판 +" + res.rewardRep);
                switch (DayLogic.TodayReservationReadiness(run))
                {
                    case ReservationReadiness.Ready:
                        c.BodyText(rb, Theme.Colorize("재료 준비 완료! 영업을 시작하면 먼저 납품합니다.", Theme.Good));
                        break;
                    case ReservationReadiness.ReadyWithFrozen:
                        c.BodyText(rb, Theme.Colorize("얼린 재료로 대체해 납품합니다 (보상 -30%).", Theme.Blue));
                        break;
                    default:
                        c.BodyText(rb, Theme.Colorize("재료 부족: 이대로면 예약 실패, 평판 -1", Theme.Bad));
                        break;
                }
            }

            // 창고
            var sb = c.Box(Theme.Card);
            c.Heading(sb, "창고 " + run.storage.Count + "/" + run.StorageCapacity, Theme.Ink);
            if (run.storage.Count == 0) c.BodyText(sb, "창고가 비었습니다.");
            else
            {
                var grid = UIKit.Node("Chips", sb);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(440f, 64f);
                g.spacing = new Vector2(14f, 12f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = 2;
                foreach (var chip in StorageChips(run))
                {
                    var img = UIKit.Panel(grid, "Chip", Color.Lerp(Theme.IngredientColor(chip.id), Color.white, 0.55f), 2f);
                    var t = UIKit.FitLabel(img.transform, "Text", chip.label, 30, Theme.Ink, TextAnchor.MiddleCenter);
                    UIKit.Stretch(t.rectTransform, 10f, 4f, 10f, 4f);
                }
            }

            // 메뉴판
            var mb = c.Box(Theme.Card, 12f);
            menuHeader = c.Heading(mb, "", Theme.Ink);
            dishRows.Clear();
            foreach (var opt in DayLogic.MenuOptions(run)) dishRows.Add(AddDishRow(mb, opt));

            int scraps = 0;
            foreach (var s in run.storage) if (s.id == GameData.Scraps) scraps++;
            c.Text(mb, "오늘의 백반: 잡재료 " + scraps + "개는 동네 주민에게 자동으로 팝니다 (" + Balance.ScrapsMealPrice + "G씩).", Card.SmallSize, Theme.InkSoft);
            c.Text(mb, "얼린 재료는 같은 음식의 다른 재료로 대체할 수 있어요 (예: 얼린 고구마로 군고구마, 판매가 -30%).", Card.SmallSize, Theme.InkSoft);

            RefreshDishRows();
            c.FooterButton("영업 시작 (손님 " + DayLogic.CustomerCount(run) + "명)", Theme.Accent, ctl.RunBusiness);
        }

        struct Chip
        {
            public string id;
            public string label;
        }

        static List<Chip> StorageChips(RunState run)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            var minDays = new Dictionary<string, int>();
            foreach (var s in run.storage)
            {
                int c;
                if (!counts.TryGetValue(s.id, out c))
                {
                    order.Add(s.id);
                    minDays[s.id] = int.MaxValue;
                }
                counts[s.id] = c + 1;
                minDays[s.id] = Mathf.Min(minDays[s.id], DayLogic.DaysLeft(run.day, s));
            }
            var list = new List<Chip>();
            foreach (var id in order)
            {
                string label = Texts.IngredientName(id) + (counts[id] > 1 ? " x" + counts[id] : "");
                if (id != GameData.Scraps) label += "  (" + Texts.ShelfLabel(minDays[id]) + ")";
                list.Add(new Chip { id = id, label = label });
            }
            return list;
        }

        DishRow AddDishRow(RectTransform parent, MenuOption opt)
        {
            var recipe = opt.recipe;
            var row = new DishRow { recipeId = recipe.id, servings = opt.servings };
            var btn = UIKit.Button(parent, "Dish " + recipe.id, null, 30, Theme.CardAlt, Theme.Ink, () => OnDishTapped(recipe.id));
            UIKit.Layout(btn, 118f);
            row.bg = (Image)btn.targetGraphic;

            row.indicator = UIKit.Panel(btn.transform, "Indicator", Theme.Muted, 3f);
            var irt = row.indicator.rectTransform;
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(16f, 80f);
            irt.anchoredPosition = new Vector2(18f, 0f);
            row.indicator.raycastTarget = false;

            var name = UIKit.FitLabel(btn.transform, "Name", recipe.name + "  " + DayLogic.DishPrice(Run, recipe, false) + "G", 36, Theme.Ink, TextAnchor.LowerLeft);
            UIKit.Anchor(name.rectTransform, 0f, 0.5f, 0.66f, 1f);
            name.rectTransform.offsetMin = new Vector2(52f, 0f);
            name.rectTransform.offsetMax = new Vector2(0f, -8f);

            var detail = UIKit.FitLabel(btn.transform, "Detail", Texts.RecipeIngredients(recipe), 28, Theme.InkSoft, TextAnchor.UpperLeft);
            UIKit.Anchor(detail.rectTransform, 0f, 0f, 0.66f, 0.5f);
            detail.rectTransform.offsetMin = new Vector2(52f, 8f);
            detail.rectTransform.offsetMax = new Vector2(0f, -2f);

            row.status = UIKit.FitLabel(btn.transform, "Status", "", 30, Theme.Ink, TextAnchor.MiddleRight);
            UIKit.Anchor(row.status.rectTransform, 0.66f, 0f, 1f, 1f);
            row.status.rectTransform.offsetMin = new Vector2(0f, 8f);
            row.status.rectTransform.offsetMax = new Vector2(-22f, -8f);
            row.status.text = opt.servings <= 0 ? "재료 없음"
                : (opt.frozenServings > 0 ? "x" + opt.servings + " 가능\n(냉동 대체 " + opt.frozenServings + ")" : "x" + opt.servings + " 가능");
            row.status.color = opt.servings > 0 ? Theme.Good : Theme.Muted;
            return row;
        }

        void OnDishTapped(string recipeId)
        {
            var session = ctl.Session;
            bool onMenu = session.IsOnMenu(recipeId);
            DishRow row = null;
            foreach (var r in dishRows) if (r.recipeId == recipeId) row = r;
            if (!onMenu && row != null && row.servings <= 0)
            {
                ctl.Toast("재료가 없는 요리예요");
                return;
            }
            if (!session.ToggleMenu(recipeId)) ctl.Toast("메뉴판이 꽉 찼어요 (상점에서 확장)");
            else ctl.MarkDirty();
            RefreshDishRows();
        }

        void RefreshDishRows()
        {
            var run = Run;
            if (menuHeader != null) menuHeader.text = "메뉴판 " + run.dayState.menu.Count + "/" + run.MenuSlots + "  (탭해서 올리기/내리기)";
            foreach (var r in dishRows)
            {
                bool on = run.dayState.menu.Contains(r.recipeId);
                r.bg.color = on ? Theme.AccentSoft : (r.servings > 0 ? Theme.CardAlt : Theme.Hex(0xEDE6DC));
                r.indicator.color = on ? Theme.Accent : Theme.Hex(0xD7CCC8);
            }
        }

        // ───────────── 낮 2: 영업 결과 ─────────────

        public void ShowDayResult()
        {
            var run = Run;
            var rep = run.dayState.report;
            var c = New(run.day + "일차 영업 결과", StatusLine());

            if (rep.hadReservation)
            {
                var rb = c.Box(rep.reservationSuccess ? Theme.Hex(0xE3F4E1) : Theme.Hex(0xFBE3DF));
                string label = rep.reservationParty + ": " + Texts.RecipeName(rep.reservationRecipeId) + " x" + rep.reservationServings;
                if (rep.reservationSuccess)
                {
                    c.Heading(rb, "예약 성공!", Theme.Good);
                    c.BodyText(rb, label + "\n+" + rep.reservationGold + "G, 평판 +" + rep.reservationRep
                        + (rep.reservationUsedFrozen ? "  (얼린 재료 대체로 -30%)" : ""));
                }
                else
                {
                    c.Heading(rb, "예약 실패...", Theme.Bad);
                    c.BodyText(rb, label + "\n재료가 모자라 손님이 실망했어요. 평판 -1");
                }
            }

            var sb = c.Box(Theme.Card);
            c.Heading(sb, "손님 " + rep.served + "/" + rep.customers + "명 응대", rep.served >= rep.customers ? Theme.Good : Theme.Ink);
            if (rep.sales.Count == 0) c.BodyText(sb, run.dayState.menu.Count == 0 ? "메뉴판이 비어 있었어요." : "만들 수 있는 요리가 없었어요.");
            foreach (var line in rep.sales)
                c.BodyText(sb, Texts.RecipeName(line.recipeId) + " x" + line.count + " = " + line.gold + "G"
                    + (line.frozenCount > 0 ? Theme.Colorize("  (냉동 대체 " + line.frozenCount + ")", Theme.Blue) : ""));
            if (rep.scrapsSold > 0) c.BodyText(sb, "오늘의 백반 x" + rep.scrapsSold + " = " + rep.scrapsGold + "G");
            if (rep.tips > 0) c.BodyText(sb, Theme.Colorize("손님을 모두 받았어요! 팁 +" + rep.tips + "G", Theme.Good));
            if (rep.satisfactionRep > 0) c.BodyText(sb, Theme.Colorize("만족도 최고! 평판 +" + rep.satisfactionRep, Theme.Good));
            if (rep.served < rep.customers) c.Text(sb, (rep.customers - rep.served) + "명은 먹을 게 없어 돌아갔어요.", Card.SmallSize, Theme.InkSoft);

            if (rep.newDishes.Count > 0)
            {
                var nb = c.Box(Theme.AccentSoft);
                var names = new List<string>();
                foreach (var id in rep.newDishes) names.Add(Texts.RecipeName(id));
                c.Heading(nb, "새 요리 발견!", Theme.Accent);
                c.BodyText(nb, string.Join(", ", names.ToArray()));
            }

            var tb = c.Box(Theme.Card);
            c.Heading(tb, "총 수입 +" + rep.totalGold + "G", Theme.Gold);
            c.BodyText(tb, "평판 " + Texts.Star + rep.repBefore + " > " + Texts.Star + rep.repAfter
                + (rep.repAfter > rep.repBefore ? "  (손님이 늘고 찍찍단도 거세집니다)" : ""));

            if (run.IsFinalDay) c.FooterButton("시즌 평가 보기", Theme.Accent, ctl.AfterResult);
            else c.FooterButton("상점으로", Theme.Accent, ctl.AfterResult);
        }

        // ───────────── 낮 3: 상점 ─────────────

        public void ShowShop()
        {
            var run = Run;
            var c = New(run.day + "일차 정비  |  상점", StatusLine());

            var rb = c.Box(Theme.Card);
            c.Heading(rb, "대청소", Theme.Ink);
            repairInfo = c.BodyText(rb, "");
            repairButton = c.RowButton(rb, "", Theme.Good, () =>
            {
                if (ctl.Session.Repair()) ctl.MarkDirty();
                RefreshShop();
            });

            var ob = c.Box(Theme.Card, 14f);
            c.Heading(ob, "오늘의 상품 (매일 바뀜)", Theme.Ink);
            foreach (var offer in run.dayState.offers)
            {
                if (GameData.ShopItem(offer.itemId) == null) continue;
                var row = new ShopRow { offer = offer };
                row.info = c.BodyText(ob, "");
                var o = offer;
                row.button = c.RowButton(ob, "", Theme.Accent, () =>
                {
                    if (ctl.Session.Buy(o)) ctl.MarkDirty();
                    RefreshShop();
                });
                shopRows.Add(row);
            }

            var hb = c.Box(Theme.Card);
            shopHint = c.BodyText(hb, "");

            c.FooterButton("마감하기", Theme.Accent, ctl.CloseShop);
            RefreshShop();
        }

        void RefreshShop()
        {
            if (current == null || repairButton == null) return;
            var run = Run;
            UIKit.SetText(current.Subtitle, StatusLine());
            UIKit.SetText(repairInfo, "위생 +" + Balance.RepairAmount + "  (지금 " + run.durability + "/" + Balance.MaxDurability + ")");
            UIKit.SetLabel(repairButton, run.durability >= Balance.MaxDurability ? "이미 반짝반짝해요" : "청소 " + Balance.RepairPrice + "G");
            repairButton.interactable = DayLogic.CanRepair(run);
            foreach (var row in shopRows)
            {
                var item = GameData.ShopItem(row.offer.itemId);
                int lv = DayLogic.Level(run, item.effect);
                UIKit.SetText(row.info, Theme.Colorize(item.name, Theme.Accent) + "  " + item.description + "  (Lv " + lv + "/" + item.maxLevel + ")");
                string label = row.offer.sold ? "구매 완료" : (lv >= item.maxLevel ? "최대 단계" : "구매 " + row.offer.price + "G");
                UIKit.SetLabel(row.button, label);
                row.button.interactable = DayLogic.CanBuy(run, row.offer);
            }
            int over = DayLogic.OverflowCount(run);
            UIKit.SetText(shopHint, "창고 " + run.storage.Count + "/" + run.StorageCapacity
                + (over > 0 ? Theme.Colorize("  마감 때 넘치는 " + over + "개는 싼 것부터 백반으로 팝니다.", Theme.Bad) : "  넘치는 재료 없음"));
        }

        // ───────────── 낮 4: 마감 ─────────────

        public void ShowClosing()
        {
            var run = Run;
            var c = New(run.day + "일차 마감", StatusLine());
            var ds = run.dayState;
            if (ds.dumped.Count > 0)
            {
                var db = c.Box(Theme.Card);
                c.BodyText(db, Theme.Colorize("창고가 넘쳐 " + ds.dumped.Count + "개를 백반으로 팔았어요 (+" + ds.dumpedGold + "G)", Theme.Bad));
                c.Text(db, Texts.Group(ds.dumped), Card.SmallSize, Theme.InkSoft);
            }
            var sb = c.Box(Theme.Card);
            c.Heading(sb, "남은 재료 " + run.storage.Count + "/" + run.StorageCapacity, Theme.Ink);
            var ids = new List<string>();
            foreach (var s in run.storage) ids.Add(s.id);
            c.BodyText(sb, Texts.Group(ids));

            AddOfferBox(c, "냉장고에 붙은 예약 메모 (" + (run.day + 1) + "일차)");
            if (run.offer.active)
            {
                c.FooterButton("거절하고 밤으로", Theme.Hex(0x8D7B6D), () => ctl.ResolveOfferAndStartNight(false));
                c.FooterButton("수락하고 밤으로", Theme.Accent, () => ctl.ResolveOfferAndStartNight(true));
            }
            else c.FooterButton("밤 준비", Theme.Accent, () => ctl.ResolveOfferAndStartNight(false));
        }

        // ───────────── 시즌 결과 ─────────────

        public void ShowSeasonResult(bool newBest)
        {
            var run = Run;
            var session = ctl.Session;
            var c = New(run.failed ? "영업 정지..." : "시즌 종료  |  미식 평론가 평가", run.failed ? run.night.index + "일 밤, 주방이 엉망이 되었습니다" : Balance.SeasonDays + "일 영업을 마쳤습니다");

            var rb = c.Box(Theme.Card, 4f);
            var rank = c.Text(rb, session.Rank, 160, run.failed ? Theme.Bad : Theme.Accent, TextAnchor.MiddleCenter);
            rank.fontStyle = FontStyle.Bold;
            c.Text(rb, session.Score + "점" + (newBest ? Theme.Colorize("  최고 기록!", Theme.Good) : ""), 56, Theme.Ink, TextAnchor.MiddleCenter);
            c.Text(rb, Texts.RankComment(session.Rank), Card.SmallSize, Theme.InkSoft, TextAnchor.MiddleCenter);

            var st = run.stats;
            var sb = c.Box(Theme.Card);
            c.Heading(sb, "시즌 기록", Theme.Ink);
            c.BodyText(sb, "번 골드 " + st.totalGold + "G  |  평판 " + Texts.Star + run.reputation + "  |  위생 " + run.durability);
            c.BodyText(sb, "예약 성공 " + st.reservationsDone + "  |  실패 " + st.reservationsFailed);
            c.BodyText(sb, "쫓아냄 " + st.kills + "  |  창고 털림 " + st.leaks + "  |  판 요리 " + st.dishesSold);
            c.Text(sb, "점수 = 번 골드 + 평판 x100 + 위생 x20 + 예약 성공 x50", Card.SmallSize, Theme.InkSoft);

            var db = c.Box(Theme.AccentSoft);
            c.Heading(db, "발견한 요리 " + st.discovered.Count + "/" + GameData.Recipes.Length, Theme.Accent);
            var names = new List<string>();
            foreach (var r in GameData.Recipes) names.Add(st.discovered.Contains(r.id) ? r.name : "???");
            c.BodyText(db, string.Join(", ", names.ToArray()));

            c.FooterButton("타이틀로", Theme.Hex(0x8D7B6D), ctl.GoToTitle);
            c.FooterButton("새 시즌", Theme.Accent, ctl.NewSeason);
        }

        // ───────────── 게임 방법 ─────────────

        public void ShowHelp(UnityAction back)
        {
            var c = New("게임 방법", "기획서 v0.1 첫 프로토타입 범위");
            var tb = c.Box(Theme.Card);
            c.Heading(tb, "주방 식구 (타워)", Theme.Accent);
            foreach (var t in GameData.Tools)
                c.BodyText(tb, t.name + " (화력 " + t.cost + ", " + GameData.MethodName(t.method) + "): " + t.description);
            c.Text(tb, "약불: 피해를 절반으로 줄여 다음 기구에 막타를 넘깁니다. 강화: 화력을 써서 오늘 밤 동안 피해 +40%.", Card.SmallSize, Theme.InkSoft);

            var mb = c.Box(Theme.Card);
            c.Heading(mb, "찍찍단(도둑 동물)과 떨어뜨리는 음식", Theme.Accent);
            foreach (var m in GameData.Monsters)
            {
                var sb = new StringBuilder();
                sb.Append(m.name).Append(": ").Append(m.trait);
                if (m.dropsIngredient)
                {
                    sb.Append("\n   ");
                    bool first = true;
                    foreach (var method in new[] { CookMethod.Prep, CookMethod.Grill, CookMethod.Freeze, CookMethod.Boil })
                    {
                        string id = GameData.IngredientFor(m, method);
                        if (id == GameData.Scraps) continue;
                        if (!first) sb.Append(", ");
                        sb.Append(GameData.MethodName(method)).Append('=').Append(Texts.IngredientName(id));
                        first = false;
                    }
                }
                c.BodyText(mb, sb.ToString());
            }

            var rb = c.Box(Theme.Card);
            c.Heading(rb, "레시피", Theme.Accent);
            foreach (var r in GameData.Recipes)
                c.BodyText(rb, r.name + " " + r.price + "G = " + Texts.RecipeIngredients(r));

            var lb = c.Box(Theme.Card);
            c.Heading(lb, "규칙", Theme.Accent);
            c.BodyText(lb, "- 재료는 3일, 얼린 재료는 6일 갑니다. 창고는 8칸.");
            c.BodyText(lb, "- 창고에 닿은 동물은 가장 비싼 재료를 하나 훔쳐 가고 주방을 어지럽혀 위생이 1 깎입니다. 위생 0이면 영업 정지.");
            c.BodyText(lb, "- 예약 보상은 일반 판매의 2배 이상. 받고 못 지키면 평판 -1.");
            c.BodyText(lb, "- 평판이 오르면 손님과 판매가가 늘지만, 냄새가 퍼져 찍찍단도 거세집니다.");

            c.FooterButton("돌아가기", Theme.Accent, back);
            helpBack = back;
        }

        // ───────────── 일시정지 ─────────────

        public void ShowPause()
        {
            HideOverlay();
            var dim = UIKit.Image(overlayRoot, "Pause", Theme.Dim);
            UIKit.Stretch(dim.rectTransform);
            overlay = dim.gameObject;
            var panel = UIKit.Panel(dim.transform, "Panel", Theme.Paper, 1f);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.08f, 0.08f);
            prt.anchorMax = new Vector2(0.92f, 0.62f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            var v = UIKit.VStack(panel.gameObject, 22f, 40, 40, TextAnchor.MiddleCenter);
            v.childForceExpandHeight = true;
            var title = UIKit.FitLabel(panel.transform, "Title", "일시정지", 64, Theme.Ink, TextAnchor.MiddleCenter);
            UIKit.Layout(title, 100f);
            UIKit.Button(panel.transform, "Resume", "계속하기", 46, Theme.Accent, Color.white, ctl.ResumeFromPause);
            UIKit.Button(panel.transform, "Help", "게임 방법", 46, Theme.Hex(0x8D7B6D), Color.white, () =>
            {
                HideOverlay();
                ShowHelp(ctl.ReturnFromHelp);
            });
            UIKit.Button(panel.transform, "Title", "저장하고 타이틀로", 46, Theme.Blue, Color.white, ctl.GoToTitle);
            Button quit = null;
            quit = UIKit.Button(panel.transform, "Abandon", "시즌 포기", 46, Theme.Bad, Color.white, () =>
            {
                var label = UIKit.LabelOf(quit);
                if (label != null && label.text == "시즌 포기")
                {
                    label.text = "정말 포기할까요? (한 번 더)";
                    return;
                }
                ctl.AbandonSeason();
            });
        }
    }
}
