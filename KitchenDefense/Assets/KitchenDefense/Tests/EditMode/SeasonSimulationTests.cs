using System.Collections.Generic;
using System.Text;
using KitchenDefense.Core;
using NUnit.Framework;

namespace KitchenDefense.Tests
{
    /// <summary>
    /// 봇이 시즌 전체(5일)를 자동으로 플레이한다. 루프가 끝까지 도는지, 결정론적인지,
    /// 그리고 대략적인 난이도(아무것도 안 하면 지고, 적당히 하면 버티는지)를 확인한다.
    /// </summary>
    public class SeasonSimulationTests
    {
        public struct BotResult
        {
            public bool failed;
            public int score;
            public string rank;
            public int durability;
            public int leaks;
            public int reservationsDone;
            public int reservationsFailed;
            public string log;
        }

        static readonly LayoutEntry[] Plan =
        {
            new LayoutEntry { type = ToolType.Knife, col = 3, row = 3 },
            new LayoutEntry { type = ToolType.Grill, col = 3, row = 5 },
            new LayoutEntry { type = ToolType.Knife, col = 2, row = 7 },
            new LayoutEntry { type = ToolType.Freezer, col = 2, row = 3 },
            new LayoutEntry { type = ToolType.Cauldron, col = 4, row = 8 },
            new LayoutEntry { type = ToolType.Grill, col = 3, row = 7 },
            new LayoutEntry { type = ToolType.Knife, col = 4, row = 5 },
            new LayoutEntry { type = ToolType.Freezer, col = 4, row = 3 },
            new LayoutEntry { type = ToolType.Knife, col = 2, row = 5 },
            new LayoutEntry { type = ToolType.Grill, col = 1, row = 3 },
            new LayoutEntry { type = ToolType.Knife, col = 3, row = 1 },
            new LayoutEntry { type = ToolType.Knife, col = 4, row = 7 },
        };

        static readonly string[] ShopPriority = { "firewood", "whetstone", "charcoal", "storage", "menu_slot", "pressure_lid", "coolant" };

        public static BotResult PlaySeason(int seed, bool defend, int maxTools = 99)
        {
            var s = GameSession.NewSeason(seed);
            var log = new StringBuilder();
            int guard = 0;
            while (s.Phase != GamePhase.SeasonResult && guard++ < 200)
            {
                switch (s.Phase)
                {
                    case GamePhase.Intro:
                    case GamePhase.DayClosing:
                        s.AcceptOffer();
                        s.StartNight();
                        break;
                    case GamePhase.Night:
                        PlayNight(s, defend, maxTools, log);
                        break;
                    case GamePhase.NightResult:
                        s.BeginDay();
                        break;
                    case GamePhase.DayMenu:
                        PickMenu(s);
                        var report = s.RunBusiness();
                        log.AppendFormat("  {0}일 낮: 손님 {1}/{2}, 수입 {3}G, 예약 {4}, 평판 {5}\n", s.Run.day, report.served, report.customers,
                            report.totalGold, report.hadReservation ? (report.reservationSuccess ? "성공" : "실패") : "-", s.Run.reputation);
                        break;
                    case GamePhase.DayResult:
                        s.AfterResult();
                        break;
                    case GamePhase.DayShop:
                        Shop(s);
                        s.CloseShop();
                        break;
                }
            }
            Assert.AreEqual(GamePhase.SeasonResult, s.Phase, "시즌이 끝나지 않았다");
            return new BotResult
            {
                failed = s.Run.failed,
                score = s.Score,
                rank = s.Rank,
                durability = s.Run.durability,
                leaks = s.Run.stats.leaks,
                reservationsDone = s.Run.stats.reservationsDone,
                reservationsFailed = s.Run.stats.reservationsFailed,
                log = log.ToString(),
            };
        }

        static void PlayNight(GameSession s, bool defend, int maxTools, StringBuilder log)
        {
            var sim = s.Night;
            int leaksBefore = s.Run.stats.leaks;
            while (!s.NightOver)
            {
                if (sim.IsPrep)
                {
                    if (defend) Prepare(sim, maxTools);
                    sim.StartWave();
                }
                sim.Step(TestUtil.Dt);
                sim.Events.Clear();
                if (sim.State.elapsed > 900f) Assert.Fail("밤이 끝나지 않는다");
            }
            log.AppendFormat("{0}일 밤: 웨이브 {1}, 처치 {2}, 누수 {3}, 기구 {4}, 내구도 {5}, 재료 {6}\n", sim.State.index, sim.State.waves.Count,
                sim.State.kills, s.Run.stats.leaks - leaksBefore, sim.State.towers.Count, s.Run.durability, sim.State.haul.Count);
            s.EndNight();
        }

        static void Prepare(NightSimulation sim, int maxTools)
        {
            foreach (var e in Plan)
            {
                if (sim.State.towers.Count >= maxTools) break;
                if (sim.TowerAt(e.col, e.row) != null) continue;
                if (sim.CheckBuild(e.type, e.col, e.row) == BuildResult.Ok) sim.Build(e.type, e.col, e.row);
                else break;
            }
            if (sim.State.towers.Count >= System.Math.Min(maxTools, Plan.Length))
                foreach (var t in sim.State.towers)
                    if (sim.CanUpgrade(t)) sim.Upgrade(t);
        }

        static void PickMenu(GameSession s)
        {
            var options = DayLogic.MenuOptions(s.Run);
            options.Sort((a, b) => (b.servings * b.recipe.price).CompareTo(a.servings * a.recipe.price));
            foreach (var o in options)
                if (o.servings > 0 && s.Run.dayState.menu.Count < s.Run.MenuSlots) s.ToggleMenu(o.recipe.id);
        }

        static void Shop(GameSession s)
        {
            while (s.Run.durability <= 6 && DayLogic.CanRepair(s.Run)) s.Repair();
            foreach (var id in ShopPriority)
                foreach (var offer in s.Run.dayState.offers)
                    if (offer.itemId == id && s.Run.gold >= offer.price + Balance.RepairPrice) s.Buy(offer);
        }

        [Test]
        public void Season_IsDeterministicForSameSeed()
        {
            var a = PlaySeason(42, true);
            var b = PlaySeason(42, true);
            Assert.AreEqual(a.score, b.score);
            Assert.AreEqual(a.log, b.log);
        }

        [Test]
        public void Season_WithoutDefense_IsLost()
        {
            var r = PlaySeason(3, false);
            Assert.IsTrue(r.failed, "아무것도 안 하면 가게가 무너져야 한다");
        }

        [Test]
        public void Season_WithSimpleStrategy_IsSurvivable()
        {
            int failed = 0;
            var summary = new StringBuilder();
            var scores = new List<int>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var r = PlaySeason(seed, true);
                if (r.failed) failed++;
                scores.Add(r.score);
                summary.AppendFormat("seed {0,2}: {1} {2,5}점, 내구도 {3,2}, 누수 {4,2}, 예약 {5}/{6}\n", seed, r.rank, r.score, r.durability, r.leaks,
                    r.reservationsDone, r.reservationsDone + r.reservationsFailed);
                if (seed == 1) summary.Append(r.log);
            }
            TestContext.WriteLine(summary.ToString());
            Assert.LessOrEqual(failed, 2, "단순한 봇도 대부분 시즌을 버텨야 한다\n" + summary);
        }

        /// <summary>밸런스 조정용 리포트. 기구 수 상한별 실패율/누수를 출력한다(Test Runner에서 직접 골라 실행).</summary>
        [Test, Explicit]
        public void BalanceReport()
        {
            var sb = new StringBuilder();
            foreach (int maxTools in new[] { 2, 3, 4, 5, 6, 8, 99 })
            {
                int failed = 0, leaks = 0, score = 0, res = 0;
                const int Seeds = 30;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var r = PlaySeason(seed, true, maxTools);
                    if (r.failed) failed++;
                    leaks += r.leaks;
                    score += r.score;
                    res += r.reservationsDone;
                }
                sb.AppendFormat("기구 최대 {0,2}: 실패 {1,2}/{2}, 평균 누수 {3:0.0}, 평균 점수 {4}, 평균 예약 성공 {5:0.0}\n",
                    maxTools, failed, Seeds, leaks / (float)Seeds, score / Seeds, res / (float)Seeds);
            }
            sb.Append(PlaySeason(1, true).log);
            TestContext.WriteLine(sb.ToString());
        }

        [Test]
        public void Season_WithTooFewTools_IsHard()
        {
            int failed = 0;
            for (int seed = 1; seed <= 10; seed++)
                if (PlaySeason(seed, true, 2).failed) failed++;
            Assert.GreaterOrEqual(failed, 5, "기구 2개만으로는 대부분 버티지 못해야 한다");
        }
    }
}
