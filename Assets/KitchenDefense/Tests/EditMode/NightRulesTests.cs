using System.Collections.Generic;
using KitchenDefense.Core;
using NUnit.Framework;

namespace KitchenDefense.Tests
{
    public class NightRulesTests
    {
        [Test]
        public void DefaultMap_HasSnakePath()
        {
            var map = KitchenMap.CreateDefault();
            Assert.AreEqual(7, map.Cols);
            Assert.AreEqual(10, map.Rows);
            Assert.AreEqual(23.5f, map.Length, 0.001f);
            Assert.AreEqual(24, map.PathCells.Count);
            Assert.IsTrue(map.IsReserved(1, 0), "뒷문");
            Assert.IsTrue(map.IsReserved(3, 9), "창고");
            Assert.IsTrue(map.IsPath(2, 2));
            Assert.IsFalse(map.IsPath(3, 3));
        }

        [Test]
        public void Map_CellCenter_RoundTrips()
        {
            var map = KitchenMap.CreateDefault();
            for (int r = 0; r < map.Rows; r++)
                for (int c = 0; c < map.Cols; c++)
                {
                    Cell cell;
                    Assert.IsTrue(map.TryGetCell(map.CellCenter(c, r), out cell));
                    Assert.AreEqual(new Cell(c, r), cell);
                }
        }

        [Test]
        public void Build_FollowsPathRules()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            Assert.AreEqual(BuildResult.NeedsFloor, sim.CheckBuild(ToolType.Knife, 2, 2), "경로 위는 가마솥만");
            Assert.AreEqual(BuildResult.NeedsPath, sim.CheckBuild(ToolType.Cauldron, 3, 3), "가마솥은 경로 위에만");
            Assert.AreEqual(BuildResult.Reserved, sim.CheckBuild(ToolType.Cauldron, 1, 0));
            Assert.AreEqual(BuildResult.Ok, sim.CheckBuild(ToolType.Cauldron, 2, 2));
            TestUtil.Build(sim, ToolType.Knife, 3, 3);
            Assert.AreEqual(BuildResult.Occupied, sim.CheckBuild(ToolType.Grill, 3, 3));
            sim.State.firepower = 10;
            Assert.AreEqual(BuildResult.NoFirepower, sim.CheckBuild(ToolType.Grill, 2, 3));
        }

        [Test]
        public void Build_SpendsFirepower_AndRefundDependsOnPhase()
        {
            var s = TestUtil.NightSession(firepower: 200);
            var sim = s.Night;
            var knife = sim.Build(ToolType.Knife, 3, 3);
            Assert.AreEqual(150, sim.State.firepower);
            Assert.AreEqual(50, sim.RefundValue(knife), "준비 시간 회수는 전액");
            TestUtil.SetSingleWave(sim, MonsterType.Raccoon);
            Assert.IsTrue(sim.StartWave());
            Assert.AreEqual(25, sim.RefundValue(knife), "웨이브 중 회수는 절반");
            Assert.AreEqual(BuildResult.NotNow, sim.CheckMove(knife, 2, 3), "이동은 준비 시간에만");
        }

        [Test]
        public void GrillLastHit_GivesGrilledIngredient()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Grill, 3, 3);
            TestUtil.Build(sim, ToolType.Grill, 3, 5);
            TestUtil.Build(sim, ToolType.Grill, 3, 7);
            TestUtil.SetSingleWave(sim, MonsterType.Raccoon);
            sim.StartWave();
            TestUtil.RunWave(sim);
            CollectionAssert.AreEqual(new[] { "grilled_fish" }, sim.State.haul);
            Assert.AreEqual(0, sim.State.leaks);
        }

        [Test]
        public void FreezerLastHit_GivesFrozenRaw_AndMouseGangScatters()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Freezer, 0, 0);
            TestUtil.Build(sim, ToolType.Freezer, 2, 0);
            TestUtil.Build(sim, ToolType.Freezer, 0, 1);
            TestUtil.Build(sim, ToolType.Freezer, 2, 1);
            TestUtil.Build(sim, ToolType.Freezer, 0, 2);
            TestUtil.SetSingleWave(sim, MonsterType.MouseGang);
            sim.StartWave();
            TestUtil.RunWave(sim);
            CollectionAssert.AreEqual(new[] { "frozen_cheese" }, sim.State.haul, "꼬마 생쥐는 빈손이다");
            Assert.AreEqual(3, sim.State.kills, "생쥐 떼 1 + 흩어진 꼬마 생쥐 2");
        }

        [Test]
        public void KnifeOnMouseGang_GivesCheeseSlice_AndTwoMiceScatter()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Knife, 2, 1);
            TestUtil.SetSingleWave(sim, MonsterType.MouseGang);
            sim.StartWave();
            for (int i = 0; i < 3000 && sim.State.kills == 0; i++)
            {
                sim.Step(TestUtil.Dt);
                sim.Events.Clear();
            }
            CollectionAssert.AreEqual(new[] { "cheese_slice" }, sim.State.haul);
            Assert.AreEqual(2, sim.State.monsters.Count);
            foreach (var m in sim.State.monsters) Assert.AreEqual(MonsterType.Mouse, m.type);
        }

        [Test]
        public void BoilOnCheese_IsNotInTable_GivesScraps()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Cauldron, 2, 2);
            TestUtil.SetSingleWave(sim, MonsterType.MouseGang);
            sim.StartWave();
            var gang = TestUtil.StepUntilSpawn(sim);
            gang.hp = gang.maxHp * 0.2f;
            for (int i = 0; i < 3000 && sim.State.kills == 0; i++)
            {
                sim.Step(TestUtil.Dt);
                sim.Events.Clear();
            }
            CollectionAssert.AreEqual(new[] { GameData.Scraps }, sim.State.haul, "삶은 치즈는 표에 없다(잡재료)");
        }

        [Test]
        public void Cauldron_BoilsOnlyAtOrBelowQuarterHp()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Cauldron, 2, 2);
            TestUtil.SetSingleWave(sim, MonsterType.Boar);
            sim.StartWave();
            var boar = TestUtil.StepUntilSpawn(sim);
            boar.hp = boar.maxHp * 0.2f;
            TestUtil.RunWave(sim);
            CollectionAssert.AreEqual(new[] { "steamed_potato" }, sim.State.haul);

            var s2 = TestUtil.NightSession();
            var sim2 = s2.Night;
            TestUtil.Build(sim2, ToolType.Cauldron, 2, 2);
            TestUtil.SetSingleWave(sim2, MonsterType.Boar);
            sim2.StartWave();
            TestUtil.RunWave(sim2);
            Assert.AreEqual(0, sim2.State.haul.Count, "체력이 많으면 그냥 지나간다");
            Assert.AreEqual(1, sim2.State.leaks);
            Assert.AreEqual(Balance.MaxDurability - 1, s2.Run.durability);
        }

        [Test]
        public void Leak_EatsMostValuableIngredient()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            s.Run.storage.Add(TestUtil.Item("grilled_cheese", 0));
            s.Run.storage.Add(TestUtil.Item("grilled_fish", 0));
            s.Run.storage.Add(TestUtil.Item("potato_strips", 0));
            TestUtil.SetSingleWave(sim, MonsterType.Boar);
            sim.StartWave();
            TestUtil.RunWave(sim);
            CollectionAssert.AreEqual(new[] { "grilled_cheese", "potato_strips" }, TestUtil.Ids(s.Run.storage));
            CollectionAssert.AreEqual(new[] { "grilled_fish" }, sim.State.eaten);
        }

        [Test]
        public void DurabilityZero_EndsSeason()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            s.Run.durability = 1;
            TestUtil.SetSingleWave(sim, MonsterType.Boar);
            sim.StartWave();
            TestUtil.RunWave(sim);
            Assert.IsTrue(s.Run.failed);
            Assert.IsTrue(s.NightOver);
            s.EndNight();
            Assert.AreEqual(GamePhase.SeasonResult, s.Phase);
            Assert.AreEqual("F", s.Rank);
        }

        [Test]
        public void LowHeat_HalvesDamage_ButNotForCauldron()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            var knife = sim.Build(ToolType.Knife, 3, 3);
            var pot = sim.Build(ToolType.Cauldron, 2, 2);
            float full = Combat.KnifeDamage(s.Run, knife, GameData.Monster(MonsterType.Boar));
            Assert.IsTrue(sim.ToggleLowHeat(knife));
            Assert.AreEqual(full * 0.5f, Combat.KnifeDamage(s.Run, knife, GameData.Monster(MonsterType.Boar)), 0.0001f);
            Assert.IsFalse(sim.ToggleLowHeat(pot));
        }

        [Test]
        public void Raccoon_ResistsKnife_AndIsWeakToHeat()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            var knife = sim.Build(ToolType.Knife, 3, 3);
            var grill = sim.Build(ToolType.Grill, 2, 3);
            var raccoon = GameData.Monster(MonsterType.Raccoon);
            var boar = GameData.Monster(MonsterType.Boar);
            Assert.AreEqual(Combat.KnifeDamage(s.Run, knife, boar) * 0.5f, Combat.KnifeDamage(s.Run, knife, raccoon), 0.0001f);
            Assert.AreEqual(Combat.BurnDps(s.Run, grill, boar) * 1.5f, Combat.BurnDps(s.Run, grill, raccoon), 0.0001f);
        }

        [Test]
        public void Upgrade_CostsFirepower_AndRaisesDamage()
        {
            var s = TestUtil.NightSession(firepower: 300);
            var sim = s.Night;
            var knife = sim.Build(ToolType.Knife, 3, 3);
            float before = Combat.KnifeDamage(s.Run, knife, GameData.Monster(MonsterType.Boar));
            Assert.IsTrue(sim.Upgrade(knife));
            Assert.AreEqual(300 - 50 - Balance.ToolUpgradeCosts[0], sim.State.firepower);
            Assert.Greater(Combat.KnifeDamage(s.Run, knife, GameData.Monster(MonsterType.Boar)), before);
            Assert.IsTrue(sim.Upgrade(knife));
            Assert.IsFalse(sim.Upgrade(knife), "최대 단계");
        }

        [Test]
        public void FlySwarm_Buzz_SlowsNearbyTools()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            var knife = sim.Build(ToolType.Knife, 2, 1);
            TestUtil.SetSingleWave(sim, MonsterType.FlySwarm);
            sim.StartWave();
            var shroom = TestUtil.StepUntilSpawn(sim);
            shroom.distance = 1.5f; // (−2, 3.5): 기구와 1칸 거리
            Assert.AreEqual(0.7f, sim.TowerRate(knife), 0.0001f);
            shroom.distance = 20f;
            Assert.AreEqual(1f, sim.TowerRate(knife), 0.0001f);
        }

        [Test]
        public void Boar_ChargesOnStraights_UnlessSlowed()
        {
            var map = KitchenMap.CreateDefault();
            var boar = GameData.Monster(MonsterType.Boar);
            Assert.IsTrue(Combat.IsCharging(map, boar, 4.0f, 0f, 1f), "직선 한가운데");
            Assert.IsFalse(Combat.IsCharging(map, boar, 4.0f, 0.5f, 1f), "감속 중에는 돌진 불가");
            Assert.IsFalse(Combat.IsCharging(map, boar, 6.3f, 0f, 1f), "모퉁이 직전");
            Assert.IsFalse(Combat.IsCharging(map, boar, 4.0f, 0f, 0.1f), "직선에 막 들어섬");
        }

        [Test]
        public void RestoreLayout_RebuildsLastNight()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Knife, 3, 3);
            TestUtil.Build(sim, ToolType.Cauldron, 2, 2);
            var layout = sim.CaptureLayout();
            sim.State.towers.Clear();
            sim.State.firepower = 200;
            Assert.AreEqual(2, sim.RestoreLayout(layout));
            Assert.AreEqual(200 - 50 - 80, sim.State.firepower);
            Assert.IsNotNull(sim.TowerAt(3, 3));
            Assert.IsNotNull(sim.TowerAt(2, 2));
        }

        [Test]
        public void Predictor_NoTools_PredictsLeak()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.SetSingleWave(sim, MonsterType.MouseGang);
            sim.StartWave();
            var m = TestUtil.StepUntilSpawn(sim);
            var p = new KillPredictor().Predict(sim, m);
            Assert.IsTrue(p.valid);
            Assert.IsTrue(p.leak);
        }

        [Test]
        public void Predictor_LowHpAndCauldron_PredictsBoil()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            var pot = sim.Build(ToolType.Cauldron, 4, 8);
            TestUtil.SetSingleWave(sim, MonsterType.Boar);
            sim.StartWave();
            var m = TestUtil.StepUntilSpawn(sim);
            m.hp = m.maxHp * 0.2f;
            var p = new KillPredictor().Predict(sim, m);
            Assert.IsTrue(p.valid);
            Assert.IsFalse(p.leak);
            Assert.AreEqual(pot.id, p.towerId);
            Assert.AreEqual(CookMethod.Boil, p.method);
            Assert.AreEqual("steamed_potato", p.ingredientId);
        }

        static IEnumerable<MonsterType> PredictorCases()
        {
            yield return MonsterType.Boar;
            yield return MonsterType.Raccoon;
            yield return MonsterType.MouseGang;
        }

        /// <summary>몬스터가 한 마리뿐이면 예상은 실제 막타와 정확히 같아야 한다.</summary>
        [TestCaseSource("PredictorCases")]
        public void Predictor_MatchesSimulation_ForSingleMonster(MonsterType type)
        {
            var layouts = new[]
            {
                new[] { new LayoutEntry { type = ToolType.Knife, col = 2, row = 1 }, new LayoutEntry { type = ToolType.Grill, col = 3, row = 5 }, new LayoutEntry { type = ToolType.Cauldron, col = 4, row = 8 } },
                new[] { new LayoutEntry { type = ToolType.Freezer, col = 2, row = 3 }, new LayoutEntry { type = ToolType.Grill, col = 3, row = 7 }, new LayoutEntry { type = ToolType.Knife, col = 6, row = 8 } },
                new[] { new LayoutEntry { type = ToolType.Grill, col = 3, row = 3, lowHeat = true }, new LayoutEntry { type = ToolType.Knife, col = 2, row = 5 }, new LayoutEntry { type = ToolType.Cauldron, col = 1, row = 5 } },
            };
            foreach (var layout in layouts)
            {
                var s = TestUtil.NightSession();
                var sim = s.Night;
                Assert.AreEqual(layout.Length, sim.RestoreLayout(layout));
                TestUtil.SetSingleWave(sim, type);
                sim.StartWave();
                var m = TestUtil.StepUntilSpawn(sim);
                int id = m.id;
                var predictor = new KillPredictor { StepSeconds = TestUtil.Dt, HorizonSeconds = 120f };
                var p = predictor.Predict(sim, m);
                Assert.IsTrue(p.valid, "예상 불가");

                int actualTower = -2;
                bool leaked = false;
                for (int i = 0; i < 20000 && sim.State.waveActive && actualTower == -2 && !leaked; i++)
                {
                    sim.Step(TestUtil.Dt);
                    foreach (var e in sim.Events)
                    {
                        if (e.monsterId != id) continue;
                        if (e.type == SimEventType.Killed) actualTower = e.towerId;
                        if (e.type == SimEventType.Leaked) leaked = true;
                    }
                    sim.Events.Clear();
                }
                Assert.AreEqual(p.leak, leaked, type + " 누수 예상");
                if (!leaked) Assert.AreEqual(p.towerId, actualTower, type + " 막타 기구 예상");
            }
        }

        [Test]
        public void Night_EndsAfterLastWave()
        {
            var s = TestUtil.NightSession();
            var sim = s.Night;
            TestUtil.Build(sim, ToolType.Knife, 2, 1);
            TestUtil.Build(sim, ToolType.Knife, 3, 3);
            TestUtil.Build(sim, ToolType.Grill, 3, 5);
            TestUtil.Build(sim, ToolType.Knife, 3, 7);
            int waves = sim.State.waves.Count;
            for (int w = 0; w < waves; w++)
            {
                Assert.IsTrue(sim.IsPrep);
                Assert.IsTrue(sim.StartWave());
                TestUtil.RunWave(sim);
            }
            Assert.IsTrue(sim.State.finished);
            Assert.IsTrue(s.NightOver);
            s.EndNight();
            Assert.AreEqual(GamePhase.NightResult, s.Phase);
            Assert.AreEqual(4, s.Run.lastLayout.Count);
        }
    }
}
