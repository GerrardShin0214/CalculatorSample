using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    /// <summary>
    /// 시즌 진행(단계 전환)을 담당한다. Unity 쪽은 이 클래스만 호출하고, 그리는 일만 한다.
    ///
    /// 개업 전야(첫 예약 제안) → 0일 밤 → 1일 낮(메뉴 → 영업 결과 → 상점 → 마감·예약) → 1일 밤 → … → 5일 낮 → 시즌 결과
    /// </summary>
    public sealed class GameSession
    {
        public const int SaveVersion = 1;

        public RunState Run { get; private set; }
        public KitchenMap Map { get; private set; }
        public NightSimulation Night { get; private set; }

        public GameSession(RunState run)
        {
            if (run == null) throw new ArgumentNullException("run");
            Run = run;
            Map = KitchenMap.CreateDefault();
            if (run.phase == GamePhase.Night || run.phase == GamePhase.NightResult)
                Night = new NightSimulation(Map, run);
        }

        public static GameSession NewSeason(int seed)
        {
            var run = new RunState
            {
                version = SaveVersion,
                seed = seed,
                rng = new Rng(seed),
                phase = GamePhase.Intro,
                day = 0,
                gold = Balance.StartGold,
                durability = Balance.MaxDurability,
            };
            run.offer = DayLogic.RollReservation(run, 1);
            return new GameSession(run);
        }

        public GamePhase Phase { get { return Run.phase; } }

        // ───────────── 예약 제안(개업 전야 · 마감) ─────────────

        public bool HasOffer { get { return Run.offer != null && Run.offer.active; } }

        public void AcceptOffer()
        {
            if (!HasOffer) return;
            Run.reservation = Run.offer.Clone();
            Run.offer.active = false;
        }

        public void DeclineOffer()
        {
            Run.offer.active = false;
        }

        // ───────────── 밤 ─────────────

        public void StartNight()
        {
            if (Run.phase != GamePhase.Intro && Run.phase != GamePhase.DayClosing) return;
            Run.night = WaveGenerator.CreateNight(Run);
            Run.phase = GamePhase.Night;
            Night = new NightSimulation(Map, Run);
        }

        public bool NightOver
        {
            get { return Run.phase == GamePhase.Night && Night != null && (Night.State.finished || Run.failed); }
        }

        /// <summary>마지막 웨이브를 막았거나 가게가 무너졌을 때 호출한다.</summary>
        public void EndNight()
        {
            if (Run.phase != GamePhase.Night) return;
            if (Run.failed)
            {
                Run.phase = GamePhase.SeasonResult;
                return;
            }
            if (!Night.State.finished) return;
            Run.lastLayout = Night.CaptureLayout();
            Run.phase = GamePhase.NightResult;
        }

        // ───────────── 낮 ─────────────

        public void BeginDay()
        {
            if (Run.phase != GamePhase.NightResult) return;
            Run.day += 1;
            Run.dayState = new DayState();
            Run.dayState.spoiled = DayLogic.RemoveSpoiled(Run);
            Run.night = new NightState();
            Night = null;
            Run.phase = GamePhase.DayMenu;
        }

        public bool IsOnMenu(string recipeId) { return Run.dayState.menu.Contains(recipeId); }

        /// <summary>메뉴판에 올리거나 내린다. 슬롯이 꽉 찼으면 false.</summary>
        public bool ToggleMenu(string recipeId)
        {
            if (Run.phase != GamePhase.DayMenu || GameData.Recipe(recipeId) == null) return false;
            var menu = Run.dayState.menu;
            if (menu.Remove(recipeId)) return true;
            if (menu.Count >= Run.MenuSlots) return false;
            menu.Add(recipeId);
            return true;
        }

        public BusinessReport RunBusiness()
        {
            if (Run.phase != GamePhase.DayMenu) return Run.dayState.report;
            var report = DayLogic.RunBusiness(Run);
            Run.phase = GamePhase.DayResult;
            return report;
        }

        /// <summary>결과 화면 다음: 마지막 날이면 시즌 평가, 아니면 상점.</summary>
        public void AfterResult()
        {
            if (Run.phase != GamePhase.DayResult) return;
            if (Run.IsFinalDay)
            {
                Run.phase = GamePhase.SeasonResult;
                return;
            }
            Run.dayState.offers = DayLogic.RollShop(Run);
            Run.phase = GamePhase.DayShop;
        }

        public bool Buy(ShopOffer offer) { return Run.phase == GamePhase.DayShop && DayLogic.Buy(Run, offer); }
        public bool Repair() { return Run.phase == GamePhase.DayShop && DayLogic.Repair(Run); }

        public void CloseShop()
        {
            if (Run.phase != GamePhase.DayShop) return;
            DayLogic.ResolveOverflow(Run);
            Run.offer = DayLogic.RollReservation(Run, Run.day + 1);
            Run.phase = GamePhase.DayClosing;
        }

        // ───────────── 결과 ─────────────

        public int Score { get { return DayLogic.SeasonScore(Run); } }
        public string Rank { get { return DayLogic.Rank(Run); } }

        /// <summary>지금 밤의 목표가 되는 예약(내일 올 손님).</summary>
        public Reservation UpcomingReservation
        {
            get
            {
                var r = Run.reservation;
                if (r == null || !r.active) return null;
                int targetDay = Run.phase == GamePhase.Night || Run.phase == GamePhase.NightResult ? Run.day + 1 : Run.day;
                return r.day == targetDay ? r : null;
            }
        }

        public List<IngredientNeed> UpcomingNeeds()
        {
            return DayLogic.ReservationNeeds(Run, UpcomingReservation);
        }
    }
}
