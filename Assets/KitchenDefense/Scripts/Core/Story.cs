namespace KitchenDefense.Core
{
    public sealed class StorySpeaker
    {
        public string id;
        public string name;
        /// <summary>초상화 원 안에 찍히는 한 글자.</summary>
        public string glyph;
        public uint color;
    }

    public struct StoryLine
    {
        /// <summary>말하는 이(Story.Speakers의 id). 빈 문자열이면 해설.</summary>
        public string speaker;
        public string text;

        public StoryLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public bool IsNarration { get { return string.IsNullOrEmpty(speaker); } }
    }

    public sealed class StoryBeat
    {
        public string id;
        public string title;
        /// <summary>장면이 끝나고 보여 주는 "오늘 밤 목표" 한 줄.</summary>
        public string goal;
        public StoryLine[] lines;
    }

    /// <summary>
    /// 세계관: 할머니가 40년 꾸려 온 골목 식당을 손주인 사장님이 다시 열었다. 사장님이 잠든 자정이면
    /// 주방 식구들(의인화된 식기와 재료)이 깨어나, 할머니의 손맛 비법 노트를 노리는 찍찍단(도둑 동물들)을 막는다.
    /// 동물들은 조리대에서 낚아챈 음식을 물고 창고로 달아나는데, 쫓아낼 때 떨어뜨린 음식은
    /// 막타를 친 식구가 그 자리에서 손질해 둔다. 아침의 사장님은 그 재료로 영업한다.
    /// 닷새 뒤 미식 평론가가 오는 날까지 가게(위생)를 지키는 것이 시즌의 목표다.
    /// 밤마다 시작 전에 한 장면씩 보여 주고, 시즌이 끝나면 등급에 따른 엔딩을 보여 준다.
    /// </summary>
    public static class Story
    {
        public const string Narrator = "";
        public const string Pot = "pot";
        public const string Knife = "knife";
        public const string Grill = "grill";
        public const string Freezer = "freezer";
        public const string Onion = "onion";
        public const string Owner = "owner";
        public const string Rat = "rat";
        public const string Critic = "critic";

        public static readonly StorySpeaker[] Speakers =
        {
            new StorySpeaker { id = Pot, name = "솥할매", glyph = "솥", color = 0x9CCC65 },
            new StorySpeaker { id = Knife, name = "칼 삼형제", glyph = "칼", color = 0xCFD8DC },
            new StorySpeaker { id = Grill, name = "불판 아저씨", glyph = "불", color = 0xFF7043 },
            new StorySpeaker { id = Freezer, name = "냉동고 언니", glyph = "냉", color = 0x81D4FA },
            new StorySpeaker { id = Onion, name = "꼬마 양파 양양이", glyph = "양", color = 0xE1BEE7 },
            new StorySpeaker { id = Owner, name = "사장님", glyph = "사", color = 0xFFCC80 },
            new StorySpeaker { id = Rat, name = "찍찍대왕", glyph = "쥐", color = 0x9E9A96 },
            new StorySpeaker { id = Critic, name = "미식 평론가", glyph = "평", color = 0xB39DDB },
        };

        static StoryLine L(string speaker, string text) { return new StoryLine(speaker, text); }

        static readonly StoryBeat[] nights =
        {
            new StoryBeat
            {
                id = "night0", title = "개업 전야",
                goal = "창고를 지켜라! 동물을 쫓아낸(막타) 식구가 떨어뜨린 음식을 손질한다.",
                lines = new[]
                {
                    L(Narrator, "할머니가 40년 동안 꾸려 온 골목 끝 작은 식당."),
                    L(Narrator, "오늘, 손주인 사장님이 그 식당 문을 다시 열었다."),
                    L(Owner, "할머니, 저 잘할 수 있겠죠? 닷새 뒤엔 미식 평론가님도 오신대요!"),
                    L(Narrator, "자정. 사장님이 불을 끄고 잠들자, 주방 식구들이 하나둘 눈을 떴다."),
                    L(Pot, "얘들아, 일어나렴. 찍찍단 녀석들이 또 냄새를 맡았구나."),
                    L(Onion, "찌, 찍찍단이요? (훌쩍) 저 오늘 시장에서 막 왔는데..."),
                    L(Pot, "쥐 두목 찍찍대왕이 할머니의 손맛 비법 노트를 노린단다."),
                    L(Pot, "놈들이 창고를 털고 주방을 어지럽히면, 사장님 가게는 문을 닫게 돼."),
                    L(Freezer, "저기 봐. 생쥐 떼가 조리대에서 치즈를 낚아채 창고 쪽으로 굴려 가고 있어!"),
                    L(Knife, "걱정 마세요! 쫓아내고, 놈들이 떨어뜨린 음식은 우리가 손질해 둘게요!"),
                    L(Pot, "그래. 막타를 친 식구가 제 방식대로 손질하는 거란다. 아침에 사장님이 그걸로 요리하실 게다."),
                },
            },
            new StoryBeat
            {
                id = "night1", title = "윙윙 정찰대",
                goal = "파리 떼는 음식을 떨어뜨리지 않는다. 근처 식구들을 느리게 하니 먼저 쫓아내자.",
                lines = new[]
                {
                    L(Narrator, "첫 영업 날 아침. 사장님은 가지런히 손질된 재료를 보고 고개를 갸웃했다."),
                    L(Owner, "어라? 누가 재료를 손질해 놨지...? 할머니가 도와주시나?"),
                    L(Grill, "흥, 우리 덕인 줄도 모르고. ...뭐, 맛있게 먹었다니 됐다."),
                    L(Freezer, "다들 조심해. 찍찍단이 오늘은 파리 떼를 데려온대."),
                    L(Pot, "파리 곁에 있으면 윙윙 소리에 정신이 없어 손이 느려진단다. 먼저 쫓아내렴."),
                    L(Onion, "파리는 아무것도 안 들고 와요? 그럼... 그냥 귀찮기만 한 거네요!"),
                },
            },
            new StoryBeat
            {
                id = "night2", title = "뒷산의 손님",
                goal = "멧돼지는 직선에서 돌진한다. 냉동고 언니로 늦추면 돌진이 멈춘다.",
                lines = new[]
                {
                    L(Narrator, "가게 앞에 손님 줄이 생겼다. 소문이 퍼지는 건 좋은 일이지만..."),
                    L(Freezer, "문제는 냄새도 같이 퍼진다는 거야. 뒷산 멧돼지가 고구마 냄새를 맡았어."),
                    L(Knife, "멧돼지라고? 쭉 뻗은 길에선 엄청 빨리 달린다던데!"),
                    L(Pot, "냉동고가 얼려 주면 돌진이 멈춘단다. 앞에서 늦추고, 길 끝에서 요리하렴."),
                    L(Grill, "군고구마는 내 전문이지. 길 끝에 날 세워라."),
                },
            },
            new StoryBeat
            {
                id = "night3", title = "털보 너구리",
                goal = "너구리는 칼에 강하고 열에 약하다. 불판으로 익히고 솥할매로 마무리하자.",
                lines = new[]
                {
                    L(Narrator, "같은 시각, 하수구 깊은 곳 찍찍단 소굴."),
                    L(Rat, "고작 냄비 몇 개한테 번번이 쫓겨나다니! 털보 너구리를 불러라!"),
                    L(Narrator, "그날 밤, 생선을 한 아름 든 너구리가 뒷문을 밀고 들어왔다."),
                    L(Knife, "으악, 털이 너무 두꺼워! 칼이 잘 안 들어가!"),
                    L(Grill, "비켜라. 너구리는 뜨거운 걸 질색하지."),
                    L(Onion, "생선은 조려도 맛있대요! 솥할매, 부탁해요!"),
                },
            },
            new StoryBeat
            {
                id = "night4", title = "찍찍대왕의 총공격",
                goal = "찍찍단이 모두 몰려온다. 오늘 밤만 버티면 평론가가 온다!",
                lines = new[]
                {
                    L(Narrator, "내일은 드디어 미식 평론가가 오는 날."),
                    L(Rat, "오늘 밤 비법 노트를 손에 넣고, 이 주방을 내 쥐구멍 식당으로 만들어 주마! 전원 출동!"),
                    L(Pot, "마지막 밤이구나. 저 노트에는 할머니의 40년 손맛이 담겨 있단다."),
                    L(Onion, "(훌쩍) 무섭지만... 이번엔 안 울 거예요. 우리가 지켜요!"),
                    L(Freezer, "침착하게. 늘 하던 대로만 하면 돼."),
                    L(Grill, "가자! 뜨거운 맛을 보여 주자!"),
                },
            },
        };

        static readonly StoryLine[] endingOpening =
        {
            L(Narrator, "닷새째 낮. 미식 평론가가 마지막 손님으로 자리에 앉았다."),
        };

        static readonly StoryBeat failure = new StoryBeat
        {
            id = "failure", title = "영업 정지",
            goal = "주방 위생이 바닥났다. 처음부터 다시 도전하자.",
            lines = new[]
            {
                L(Narrator, "아침이 밝자 주방은 발자국과 부스러기로 엉망이 되어 있었다."),
                L(Owner, "이게 다 뭐야... 이래선 영업을 할 수가 없어."),
                L(Rat, "찍찍찍! 이 주방은 이제 우리 거다!"),
                L(Pot, "얘들아, 아직 끝난 게 아니란다. 처음부터 다시 해 보자꾸나."),
            },
        };

        public static StorySpeaker Speaker(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Speakers.Length; i++)
                if (Speakers[i].id == id) return Speakers[i];
            return null;
        }

        public static int BeatCount { get { return nights.Length; } }

        /// <summary>n일 밤을 시작하기 전에 보여 줄 장면. 0일 밤 장면은 시즌 첫 화면(프롤로그)이다.</summary>
        public static StoryBeat BeforeNight(int night)
        {
            return night >= 0 && night < nights.Length ? nights[night] : null;
        }

        /// <summary>n일 밤 시점에 평론가 방문까지 남은 날.</summary>
        public static int DaysUntilCritic(int night) { return Balance.SeasonDays - night; }

        public static StoryBeat Failure() { return failure; }

        /// <summary>시즌을 마친 뒤 등급(S/A/B/C)에 따른 엔딩.</summary>
        public static StoryBeat Ending(string rank)
        {
            StoryLine[] tail;
            string title;
            switch (rank)
            {
                case "S":
                    title = "별 다섯 개";
                    tail = new[]
                    {
                        L(Critic, "이 집 손맛은 진짜군요. 별 다섯 개, 아니 여섯 개라도 드리고 싶습니다!"),
                        L(Owner, "할머니... 우리 가게, 지켜 냈어요!"),
                        L(Narrator, "그날 밤, 주방 식구들은 조용히 축배를 들었다. 찍찍단은 다시는 오지 않았다... 아마도."),
                        L(Pot, "수고했다, 얘들아. 할머니도 하늘에서 웃고 계실 게다."),
                    };
                    break;
                case "A":
                    title = "또 오겠습니다";
                    tail = new[]
                    {
                        L(Critic, "재료 손질이 놀랍군요. 누가 밤새 다듬어 놓은 것처럼요. 꼭 다시 오겠습니다."),
                        L(Owner, "(그러게요... 대체 누가 했을까?)"),
                        L(Pot, "잘했다, 얘들아. 다음엔 별 다섯 개를 받아 보자꾸나."),
                    };
                    break;
                case "B":
                    title = "조금 아쉬운 맛";
                    tail = new[]
                    {
                        L(Critic, "맛은 좋은데, 메뉴가 조금 아쉽네요. 다음엔 더 기대하겠습니다."),
                        L(Grill, "흥, 다음엔 더 뜨겁게 해 주지."),
                        L(Pot, "가게는 지켰으니 됐단다. 다음엔 막타를 더 잘 노려 보자."),
                    };
                    break;
                default:
                    title = "다음을 기약하며";
                    tail = new[]
                    {
                        L(Critic, "가게는 깨끗하군요. 음식은... 다음을 기대해 보죠."),
                        L(Onion, "(훌쩍) 재료가 모자랐어요..."),
                        L(Pot, "괜찮다. 예약 메모에 맞춰 막타를 치는 연습을 해 보자꾸나."),
                    };
                    break;
            }
            var lines = new StoryLine[endingOpening.Length + tail.Length];
            endingOpening.CopyTo(lines, 0);
            tail.CopyTo(lines, endingOpening.Length);
            string goal = rank == "S" ? "완벽한 시즌! 더 높은 점수에 도전해 보자." : "다음 시즌엔 예약 메모에 맞춰 막타를 노려 보자.";
            return new StoryBeat { id = "ending_" + rank, title = title, goal = goal, lines = lines };
        }
    }
}
