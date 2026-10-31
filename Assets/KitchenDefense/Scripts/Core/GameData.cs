using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    /// <summary>조리법. 몬스터에게 막타를 친 기구의 조리법이 재료를 정한다.</summary>
    public enum CookMethod { None = 0, Prep = 1, Grill = 2, Freeze = 3, Boil = 4 }

    /// <summary>조리기구(타워). 첫 프로토타입 범위의 시작 기구 4종.</summary>
    public enum ToolType { Knife = 0, Grill = 1, Freezer = 2, Cauldron = 3 }

    /// <summary>첫 프로토타입 범위의 몬스터 4종 + 슬라임 분열체.</summary>
    public enum MonsterType { Slime = 0, SlimeSmall = 1, Mushroom = 2, Boar = 3, Crab = 4 }

    public enum ShopEffect { Repair = 0, MenuSlot = 1, Storage = 2, Knife = 3, Grill = 4, Freezer = 5, Cauldron = 6, Firepower = 7 }

    public sealed class ToolDef
    {
        public ToolType type;
        public string name;
        /// <summary>기구 위에 크게 찍히는 한 글자.</summary>
        public string glyph;
        public CookMethod method;
        /// <summary>설치 비용(화력).</summary>
        public int cost;
        /// <summary>사거리(칸). 가마솥은 자기 칸만 본다.</summary>
        public float range;
        /// <summary>한 번 공격할 때의 피해.</summary>
        public float damage;
        /// <summary>공격 간격(초). 가마솥은 재사용 대기.</summary>
        public float interval;
        /// <summary>true면 경로 위에만, false면 경로 옆 빈 칸에만 설치.</summary>
        public bool onPath;
        public uint color;
        public string role;
        public string description;
        public float burnDps;
        public float burnDuration;
        public float slowFactor;
        public float slowDuration;
        public float killThreshold;
    }

    public sealed class MonsterDef
    {
        public MonsterType type;
        public string name;
        public float hp;
        /// <summary>이동 속도(칸/초).</summary>
        public float speed;
        /// <summary>처치 시 얻는 화력.</summary>
        public int reward;
        /// <summary>웨이브 예산에서 차지하는 비용.</summary>
        public int cost;
        public float radius;
        public uint color;
        public string trait;
        public bool dropsIngredient = true;
        public MonsterType splitInto;
        public int splitCount;
        public float sporeRadius;
        /// <summary>포자 범위 안 기구의 공격 속도 배율.</summary>
        public float sporeRateMultiplier = 1f;
        public float chargeMultiplier = 1f;
        public float chargeDelay;
        public float knifeMultiplier = 1f;
        public float heatMultiplier = 1f;
        public string prepIngredient;
        public string grillIngredient;
        public string freezeIngredient;
        public string boilIngredient;
    }

    public sealed class IngredientDef
    {
        public string id;
        public string name;
        public MonsterType monster;
        public CookMethod method;
        /// <summary>재료 가치. 누수 몬스터는 비싼 것부터 먹고, 창고 초과분은 싼 것부터 백반으로 턴다.</summary>
        public int value;
        /// <summary>냉동 막타로 얻은 얼린 원재료. 같은 몬스터의 다른 재료를 대신할 수 있다(판매가 -30%).</summary>
        public bool frozen;
        public bool scraps;
    }

    public sealed class RecipeDef
    {
        public string id;
        public string name;
        public string[] ingredients;
        public int price;
        /// <summary>예약을 낼 만한 손님들(분위기용).</summary>
        public string[] parties;
    }

    public sealed class ShopItemDef
    {
        public string id;
        public string name;
        public string description;
        public ShopEffect effect;
        public int price;
        public int priceStep;
        public int maxLevel;
    }

    /// <summary>
    /// 밸런스 수치. 기획서 v0.1의 값을 그대로 쓰고, 기획서에 없는 값은 프로토타입용 초기값이다.
    /// </summary>
    public static class Balance
    {
        /// <summary>첫 프로토타입: 5일 시즌(0~4일 밤, 1~5일 낮).</summary>
        public const int SeasonDays = 5;
        public const int MaxDurability = 10;
        public const int StartGold = 60;
        public const int BaseStorage = 8;
        public const int StoragePerUpgrade = 2;
        public const int BaseMenuSlots = 3;
        public const int ShelfLife = 3;
        public const int FrozenShelfLife = 6;
        public const float FrozenSubstitutePriceMultiplier = 0.7f;
        public const int ScrapsMealPrice = 6;

        public const int NightBaseFirepower = 150;
        public const int FirepowerPerUpgrade = 25;
        /// <summary>밤이 거듭될수록 기본 화력도 오른다(몬스터가 강해지는 만큼).</summary>
        public const int FirepowerPerNight = 25;
        public const float LowHeatMultiplier = 0.5f;
        public const int MaxToolLevel = 2;
        public static readonly int[] ToolUpgradeCosts = { 50, 80 };
        public const float ToolUpgradeDamageBonus = 0.4f;
        public const float CauldronUpgradeCooldownMultiplier = 0.75f;
        public const float RefundRateDuringWave = 0.5f;

        public const float ShopDamageBonus = 0.2f;
        public const float ShopCauldronCooldownMultiplier = 0.85f;
        public const float ShopFreezerSlowBonus = 0.3f;
        public const int RepairAmount = 3;
        public const int RepairPrice = 40;
        public const int ShopOffersPerDay = 4;

        /// <summary>기획서 리스크 대응: 화면당 몬스터 수 상한.</summary>
        public const int MaxMonstersAlive = 16;
        public const float NightHpGrowth = 0.15f;
        public const float ReputationHpGrowth = 0.05f;

        public const int BaseCustomers = 4;
        public const int CustomersPerReputation = 2;
        public const float ReputationPriceBonus = 0.05f;
        public const float TipRate = 0.1f;
        /// <summary>기획서: 예약 보상은 일반 판매의 2배 이상.</summary>
        public const float ReservationRewardMultiplier = 2.2f;
    }

    public static class GameData
    {
        public const string Scraps = "scraps";

        public static readonly ToolDef[] Tools =
        {
            new ToolDef
            {
                type = ToolType.Knife, name = "식칼 거치대", glyph = "칼", method = CookMethod.Prep,
                cost = 50, range = 1.6f, damage = 8f, interval = 0.5f, color = 0xCFD8DC,
                role = "주력 딜", description = "빠른 단일 타격. 막타 시 손질 재료",
            },
            new ToolDef
            {
                type = ToolType.Grill, name = "그릴", glyph = "불", method = CookMethod.Grill,
                cost = 70, range = 1.5f, interval = 1f, burnDps = 7f, burnDuration = 2.5f, color = 0xFF7043,
                role = "광역 딜", description = "범위 화상(지속 피해). 막타 시 구이 재료",
            },
            new ToolDef
            {
                type = ToolType.Freezer, name = "냉동고", glyph = "냉", method = CookMethod.Freeze,
                cost = 60, range = 1.5f, damage = 3f, interval = 1.2f, slowFactor = 0.5f, slowDuration = 1.6f, color = 0x81D4FA,
                role = "제어, 재료 보존", description = "냉기 분사로 감속. 막타 시 얼린 원재료(보존 2배)",
            },
            new ToolDef
            {
                type = ToolType.Cauldron, name = "가마솥", glyph = "솥", method = CookMethod.Boil,
                cost = 80, range = 0.5f, interval = 5f, killThreshold = 0.25f, onPath = true, color = 0x9CCC65,
                role = "막타 확정", description = "경로 위 함정. 체력 25% 이하 몬스터를 즉시 삶는다",
            },
        };

        public static readonly MonsterDef[] Monsters =
        {
            new MonsterDef
            {
                type = MonsterType.Slime, name = "젤리 슬라임", hp = 48f, speed = 0.75f, reward = 5, cost = 3, radius = 0.3f,
                color = 0x8BE36A, trait = "느림. 죽으면 꼬마 슬라임 2마리로 분열",
                splitInto = MonsterType.SlimeSmall, splitCount = 2,
                grillIngredient = "grilled_jelly", freezeIngredient = "frozen_jelly",
            },
            new MonsterDef
            {
                type = MonsterType.SlimeSmall, name = "꼬마 슬라임", hp = 14f, speed = 1f, reward = 1, cost = 1, radius = 0.19f,
                color = 0xB9F59B, trait = "분열체. 너무 작아서 재료가 나오지 않음", dropsIngredient = false,
            },
            new MonsterDef
            {
                type = MonsterType.Mushroom, name = "걷는 버섯", hp = 62f, speed = 0.85f, reward = 6, cost = 3, radius = 0.3f,
                color = 0xE57373, trait = "포자로 주변 기구 공격 속도 감소",
                sporeRadius = 1.5f, sporeRateMultiplier = 0.7f,
                grillIngredient = "grilled_mushroom", boilIngredient = "mushroom_broth", freezeIngredient = "frozen_mushroom",
            },
            new MonsterDef
            {
                type = MonsterType.Boar, name = "뿔멧돼지", hp = 70f, speed = 1.25f, reward = 7, cost = 4, radius = 0.33f,
                color = 0xA1765A, trait = "빠름. 직선 구간에서 돌진(감속당하면 멈춤)",
                chargeMultiplier = 1.5f, chargeDelay = 0.6f,
                prepIngredient = "raw_meat", grillIngredient = "grilled_ribs", boilIngredient = "boiled_meat", freezeIngredient = "frozen_meat",
            },
            new MonsterDef
            {
                type = MonsterType.Crab, name = "갑옷게", hp = 120f, speed = 0.65f, reward = 9, cost = 5, radius = 0.33f,
                color = 0xFF9E40, trait = "칼 피해 50% 감소, 열에 약함(화상 1.5배)",
                knifeMultiplier = 0.5f, heatMultiplier = 1.5f,
                grillIngredient = "grilled_crab", boilIngredient = "boiled_crab", freezeIngredient = "frozen_crab",
            },
        };

        public static readonly IngredientDef[] Ingredients =
        {
            Ing("frozen_jelly", "얼린 젤리", MonsterType.Slime, CookMethod.Freeze, 8, true),
            Ing("grilled_jelly", "구운 젤리", MonsterType.Slime, CookMethod.Grill, 12, false),
            Ing("grilled_mushroom", "구운 버섯", MonsterType.Mushroom, CookMethod.Grill, 10, false),
            Ing("mushroom_broth", "버섯 육수", MonsterType.Mushroom, CookMethod.Boil, 16, false),
            Ing("frozen_mushroom", "얼린 버섯", MonsterType.Mushroom, CookMethod.Freeze, 7, true),
            Ing("raw_meat", "생고기", MonsterType.Boar, CookMethod.Prep, 17, false),
            Ing("grilled_ribs", "구운 갈비", MonsterType.Boar, CookMethod.Grill, 22, false),
            Ing("boiled_meat", "삶은 고기", MonsterType.Boar, CookMethod.Boil, 24, false),
            Ing("frozen_meat", "얼린 고기", MonsterType.Boar, CookMethod.Freeze, 14, true),
            Ing("boiled_crab", "삶은 게", MonsterType.Crab, CookMethod.Boil, 20, false),
            Ing("grilled_crab", "구운 게", MonsterType.Crab, CookMethod.Grill, 22, false),
            Ing("frozen_crab", "얼린 게", MonsterType.Crab, CookMethod.Freeze, 14, true),
            new IngredientDef { id = Scraps, name = "잡재료", monster = MonsterType.Slime, method = CookMethod.None, value = 3, scraps = true },
        };

        /// <summary>맨드레이크·훈연기·보스 재료가 필요 없는 레시피 8개(첫 프로토타입 범위).</summary>
        public static readonly RecipeDef[] Recipes =
        {
            Rec("slime_sherbet", "슬라임 셔벗", 15, new[] { "꼬마 손님 생일", "마을 잔치" }, "frozen_jelly"),
            Rec("mushroom_skewer", "버섯 꼬치", 20, new[] { "숲속 산책 모임", "마을 잔치" }, "grilled_mushroom", "grilled_mushroom"),
            Rec("caramel_jelly", "캐러멜 젤리", 25, new[] { "꼬마 손님 생일", "제과 길드" }, "grilled_jelly", "grilled_jelly"),
            Rec("boar_yukhoe", "멧돼지 육회", 35, new[] { "용병단", "사냥꾼 모임" }, "raw_meat", "raw_meat"),
            Rec("steamed_crab", "통게찜", 40, new[] { "어부 조합", "게 축제 위원회" }, "boiled_crab", "boiled_crab"),
            Rec("crab_salad", "게살 냉채", 45, new[] { "귀족 집사", "여름 연회" }, "boiled_crab", "frozen_jelly"),
            Rec("bbq_platter", "바비큐 플래터", 45, new[] { "기사단", "모험가 파티" }, "grilled_ribs", "grilled_mushroom"),
            Rec("mushroom_hotpot", "버섯 수육 전골", 55, new[] { "기사단", "마을 이장님" }, "boiled_meat", "mushroom_broth"),
        };

        public static readonly ShopItemDef RepairItem = new ShopItemDef
        {
            id = "repair", name = "가게 수리", description = "내구도 +" + Balance.RepairAmount,
            effect = ShopEffect.Repair, price = Balance.RepairPrice, maxLevel = 999,
        };

        public static readonly ShopItemDef[] ShopItems =
        {
            Shop("menu_slot", "메뉴판 확장", "메뉴 슬롯 +1", ShopEffect.MenuSlot, 90, 60, 2),
            Shop("storage", "창고 선반", "창고 +" + Balance.StoragePerUpgrade + "칸", ShopEffect.Storage, 60, 30, 3),
            Shop("whetstone", "숫돌", "식칼 거치대 피해 +20%", ShopEffect.Knife, 70, 40, 3),
            Shop("charcoal", "참숯", "그릴 화상 피해 +20%", ShopEffect.Grill, 70, 40, 3),
            Shop("coolant", "냉매 보충", "냉동고 피해 +20%, 감속 +0.3초", ShopEffect.Freezer, 60, 40, 3),
            Shop("pressure_lid", "압력솥 뚜껑", "가마솥 재사용 대기 -15%", ShopEffect.Cauldron, 70, 40, 3),
            Shop("firewood", "장작 비축", "밤 시작 화력 +" + Balance.FirepowerPerUpgrade, ShopEffect.Firepower, 70, 40, 3),
        };

        static readonly Dictionary<string, IngredientDef> ingredientById = new Dictionary<string, IngredientDef>();
        static readonly Dictionary<string, RecipeDef> recipeById = new Dictionary<string, RecipeDef>();
        static readonly Dictionary<string, ShopItemDef> shopById = new Dictionary<string, ShopItemDef>();

        static GameData()
        {
            for (int i = 0; i < Tools.Length; i++)
                if ((int)Tools[i].type != i) throw new InvalidOperationException("Tools 배열 순서가 ToolType과 다릅니다.");
            for (int i = 0; i < Monsters.Length; i++)
                if ((int)Monsters[i].type != i) throw new InvalidOperationException("Monsters 배열 순서가 MonsterType과 다릅니다.");
            foreach (var d in Ingredients) ingredientById.Add(d.id, d);
            foreach (var r in Recipes)
            {
                foreach (var ing in r.ingredients)
                    if (!ingredientById.ContainsKey(ing)) throw new InvalidOperationException("레시피 " + r.id + "의 재료 " + ing + "가 없습니다.");
                recipeById.Add(r.id, r);
            }
            shopById.Add(RepairItem.id, RepairItem);
            foreach (var s in ShopItems) shopById.Add(s.id, s);
        }

        public static ToolDef Tool(ToolType type) { return Tools[(int)type]; }
        public static MonsterDef Monster(MonsterType type) { return Monsters[(int)type]; }

        public static IngredientDef Ingredient(string id)
        {
            IngredientDef d;
            if (id != null && ingredientById.TryGetValue(id, out d)) return d;
            return ingredientById[Scraps];
        }

        public static bool HasIngredient(string id) { return id != null && ingredientById.ContainsKey(id); }

        public static RecipeDef Recipe(string id)
        {
            RecipeDef r;
            return id != null && recipeById.TryGetValue(id, out r) ? r : null;
        }

        public static ShopItemDef ShopItem(string id)
        {
            ShopItemDef s;
            return id != null && shopById.TryGetValue(id, out s) ? s : null;
        }

        /// <summary>재료 = 몬스터 × 조리법. 몬스터 표에 없는 조합은 잡재료가 된다.</summary>
        public static string IngredientFor(MonsterDef monster, CookMethod method)
        {
            if (!monster.dropsIngredient) return null;
            string id = null;
            switch (method)
            {
                case CookMethod.Prep: id = monster.prepIngredient; break;
                case CookMethod.Grill: id = monster.grillIngredient; break;
                case CookMethod.Freeze: id = monster.freezeIngredient; break;
                case CookMethod.Boil: id = monster.boilIngredient; break;
            }
            return string.IsNullOrEmpty(id) ? Scraps : id;
        }

        public static ToolType ToolForMethod(CookMethod method)
        {
            switch (method)
            {
                case CookMethod.Grill: return ToolType.Grill;
                case CookMethod.Freeze: return ToolType.Freezer;
                case CookMethod.Boil: return ToolType.Cauldron;
                default: return ToolType.Knife;
            }
        }

        public static string MethodName(CookMethod method)
        {
            switch (method)
            {
                case CookMethod.Prep: return "손질";
                case CookMethod.Grill: return "구이";
                case CookMethod.Freeze: return "냉동";
                case CookMethod.Boil: return "삶기";
                default: return "-";
            }
        }

        /// <summary>"구운 갈비" → "뿔멧돼지를 그릴로 막타"처럼 얻는 방법을 알려준다.</summary>
        public static string HowToGet(string ingredientId)
        {
            var ing = Ingredient(ingredientId);
            if (ing.scraps) return "표에 없는 조합으로 막타";
            return KoreanText.Eul(Monster(ing.monster).name) + " " + KoreanText.Euro(Tool(ToolForMethod(ing.method)).name) + " 막타";
        }

        static IngredientDef Ing(string id, string name, MonsterType monster, CookMethod method, int value, bool frozen)
        {
            return new IngredientDef { id = id, name = name, monster = monster, method = method, value = value, frozen = frozen };
        }

        static RecipeDef Rec(string id, string name, int price, string[] parties, params string[] ingredients)
        {
            return new RecipeDef { id = id, name = name, price = price, parties = parties, ingredients = ingredients };
        }

        static ShopItemDef Shop(string id, string name, string description, ShopEffect effect, int price, int priceStep, int maxLevel)
        {
            return new ShopItemDef { id = id, name = name, description = description, effect = effect, price = price, priceStep = priceStep, maxLevel = maxLevel };
        }
    }
}
