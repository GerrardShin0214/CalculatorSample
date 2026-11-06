using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    // 이 파일의 클래스는 전부 UnityEngine.JsonUtility로 저장된다.
    // 규칙: public 필드만, Dictionary/프로퍼티/null 클래스 금지(JsonUtility는 null 객체를 기본값 객체로 만든다).

    public enum GamePhase
    {
        Intro = 0,
        Night = 1,
        NightResult = 2,
        DayMenu = 3,
        DayResult = 4,
        DayShop = 5,
        DayClosing = 6,
        SeasonResult = 7,
    }

    [Serializable]
    public class StoredIngredient
    {
        public string id;
        /// <summary>얻은 밤의 번호. n일 밤에 얻은 재료는 n+1일 낮부터 쓸 수 있다.</summary>
        public int night;
    }

    [Serializable]
    public class Reservation
    {
        public bool active;
        /// <summary>손님이 오는 날(낮).</summary>
        public int day;
        public string party;
        public string recipeId;
        public int servings;
        public int rewardGold;
        public int rewardRep;

        public Reservation Clone() { return (Reservation)MemberwiseClone(); }
    }

    [Serializable]
    public class TowerState
    {
        public int id;
        public ToolType type;
        public int col;
        public int row;
        public bool lowHeat;
        /// <summary>밤 중 임시 강화 단계(0~2). 매일 밤 초기화.</summary>
        public int level;
        public float cooldown;
        public int kills;
        /// <summary>설치·강화에 쓴 화력(회수 시 환급 기준).</summary>
        public int invested;
    }

    [Serializable]
    public class LayoutEntry
    {
        public ToolType type;
        public int col;
        public int row;
        public bool lowHeat;
    }

    [Serializable]
    public class MonsterState
    {
        public int id;
        public MonsterType type;
        public bool alive;
        public float hp;
        public float maxHp;
        /// <summary>경로를 따라 이동한 거리(칸).</summary>
        public float distance;
        /// <summary>겹쳐 보이지 않게 경로 옆으로 살짝 비켜 그리는 값(표시 전용).</summary>
        public float lateral;
        public float slowTimer;
        public float slowFactor = 1f;
        public float burnTimer;
        public float burnDps;
        public int burnTowerId = -1;
        public int lastHitTowerId = -1;
        public CookMethod lastHitMethod;
        public int segment;
        public float straightTimer;
    }

    [Serializable]
    public class SpawnEntry
    {
        public MonsterType type;
        /// <summary>앞 몬스터가 나온 뒤 기다리는 시간(초).</summary>
        public float delay;
    }

    [Serializable]
    public class WaveDef
    {
        public List<SpawnEntry> spawns = new List<SpawnEntry>();
    }

    [Serializable]
    public class NightState
    {
        public int index;
        public List<WaveDef> waves = new List<WaveDef>();
        public int waveIndex;
        public bool waveActive;
        public bool finished;
        public int spawnCursor;
        public float spawnTimer;
        public List<MonsterState> monsters = new List<MonsterState>();
        public List<TowerState> towers = new List<TowerState>();
        public int nextMonsterId = 1;
        public int nextTowerId = 1;
        public int firepower;
        public int kills;
        public int leaks;
        public float elapsed;
        public List<string> haul = new List<string>();
        public List<string> eaten = new List<string>();
    }

    [Serializable]
    public class UpgradeLevels
    {
        public int menuSlot;
        public int storage;
        public int knife;
        public int grill;
        public int freezer;
        public int cauldron;
        public int firepower;
    }

    [Serializable]
    public class SaleLine
    {
        public string recipeId;
        public int count;
        public int frozenCount;
        public int gold;
    }

    [Serializable]
    public class BusinessReport
    {
        public bool hadReservation;
        public bool reservationSuccess;
        public bool reservationUsedFrozen;
        public string reservationParty;
        public string reservationRecipeId;
        public int reservationServings;
        public int reservationGold;
        public int reservationRep;
        public int customers;
        public int served;
        public List<SaleLine> sales = new List<SaleLine>();
        public int dishGold;
        public int scrapsSold;
        public int scrapsGold;
        public int tips;
        public int satisfactionRep;
        public int totalGold;
        public int repBefore;
        public int repAfter;
        public List<string> newDishes = new List<string>();
    }

    [Serializable]
    public class ShopOffer
    {
        public string itemId;
        public int price;
        public bool sold;
    }

    [Serializable]
    public class DayState
    {
        public List<string> menu = new List<string>();
        public List<string> spoiled = new List<string>();
        public bool businessDone;
        public BusinessReport report = new BusinessReport();
        public List<ShopOffer> offers = new List<ShopOffer>();
        public List<string> dumped = new List<string>();
        public int dumpedGold;
    }

    [Serializable]
    public class SeasonStats
    {
        public int totalGold;
        public int kills;
        public int leaks;
        public int reservationsDone;
        public int reservationsFailed;
        public int dishesSold;
        public List<string> discovered = new List<string>();
    }

    /// <summary>한 시즌의 전체 상태. 이것 하나만 저장하면 언제든 그 자리에서 이어서 할 수 있다.</summary>
    [Serializable]
    public class RunState
    {
        public int version;
        public int seed;
        public Rng rng = new Rng();
        public GamePhase phase;
        /// <summary>현재 날짜. n일 밤 다음에 n+1일 낮이 온다. 시즌 시작(개업 전야)은 0.</summary>
        public int day;
        public int gold;
        public int reputation;
        public int durability;
        public bool failed;
        public List<StoredIngredient> storage = new List<StoredIngredient>();
        public UpgradeLevels upgrades = new UpgradeLevels();
        /// <summary>수락한 예약. day가 오는 날이다.</summary>
        public Reservation reservation = new Reservation();
        /// <summary>마감 때 들어온 예약 제안(수락 전).</summary>
        public Reservation offer = new Reservation();
        public NightState night = new NightState();
        public DayState dayState = new DayState();
        public SeasonStats stats = new SeasonStats();
        /// <summary>지난밤 배치. 준비 시간에 '어제 배치'로 한 번에 되살린다.</summary>
        public List<LayoutEntry> lastLayout = new List<LayoutEntry>();
        /// <summary>이야기를 어디까지 봤는지(밤 번호). 이어하기 때 같은 장면을 다시 띄우지 않는다.</summary>
        public int storyNight = -1;

        public int StorageCapacity { get { return Balance.BaseStorage + Balance.StoragePerUpgrade * upgrades.storage; } }
        public int MenuSlots { get { return Balance.BaseMenuSlots + upgrades.menuSlot; } }
        public bool IsFinalDay { get { return day >= Balance.SeasonDays; } }
    }
}
