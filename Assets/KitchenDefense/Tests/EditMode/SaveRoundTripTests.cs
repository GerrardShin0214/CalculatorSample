using KitchenDefense.Core;
using NUnit.Framework;

namespace KitchenDefense.Tests
{
    /// <summary>
    /// "앱을 나가면 그 순간 상태를 저장하고 그대로 이어서 한다." — 웨이브 도중 저장했다가 불러와도
    /// 원래 게임과 똑같이 이어져야 한다. Unity에서는 실제 저장에 쓰는 JsonUtility로,
    /// Unity 밖(dotnet)에서는 public 필드만 다루는 System.Text.Json으로 같은 검사를 한다.
    /// </summary>
    public class SaveRoundTripTests
    {
        static string ToJson(RunState run)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.ToJson(run);
#else
            return System.Text.Json.JsonSerializer.Serialize(run, Options);
#endif
        }

        static RunState FromJson(string json)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<RunState>(json);
#else
            return System.Text.Json.JsonSerializer.Deserialize<RunState>(json, Options);
#endif
        }

#if !UNITY_5_3_OR_NEWER
        // JsonUtility처럼 public 필드만 본다(프로퍼티는 무시).
        static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions
        {
            IncludeFields = true,
            IgnoreReadOnlyProperties = true,
        };
#endif

        [Test]
        public void MidWaveSave_ContinuesExactlyLikeTheOriginal()
        {
            var original = TestUtil.NightSession(11);
            var sim = original.Night;
            TestUtil.Build(sim, ToolType.Knife, 3, 3);
            TestUtil.Build(sim, ToolType.Grill, 3, 5);
            TestUtil.Build(sim, ToolType.Freezer, 2, 3);
            sim.ToggleLowHeat(sim.TowerAt(3, 5));
            original.Run.storage.Add(TestUtil.Item("roasted_potato", 0));
            sim.StartWave();
            for (int i = 0; i < 240; i++)
            {
                sim.Step(TestUtil.Dt);
                sim.Events.Clear();
            }
            Assert.Greater(sim.State.monsters.Count, 0, "웨이브 도중이어야 한다");

            var restored = new GameSession(FromJson(ToJson(original.Run)));
            Assert.IsNotNull(restored.Night);
            AssertSameGame(original.Run, restored.Run);

            for (int i = 0; i < 900; i++)
            {
                original.Night.Step(TestUtil.Dt);
                original.Night.Events.Clear();
                restored.Night.Step(TestUtil.Dt);
                restored.Night.Events.Clear();
            }
            AssertSameGame(original.Run, restored.Run);
        }

        /// <summary>실수 표기 차이에 흔들리지 않도록 게임 결과(정수·목록)만 비교한다.</summary>
        static void AssertSameGame(RunState a, RunState b)
        {
            Assert.AreEqual(a.phase, b.phase);
            Assert.AreEqual(a.day, b.day);
            Assert.AreEqual(a.gold, b.gold);
            Assert.AreEqual(a.durability, b.durability);
            Assert.AreEqual(a.rng.state, b.rng.state);
            CollectionAssert.AreEqual(TestUtil.Ids(a.storage), TestUtil.Ids(b.storage));
            var na = a.night;
            var nb = b.night;
            Assert.AreEqual(na.waveIndex, nb.waveIndex);
            Assert.AreEqual(na.waveActive, nb.waveActive);
            Assert.AreEqual(na.spawnCursor, nb.spawnCursor);
            Assert.AreEqual(na.firepower, nb.firepower);
            Assert.AreEqual(na.kills, nb.kills);
            Assert.AreEqual(na.leaks, nb.leaks);
            CollectionAssert.AreEqual(na.haul, nb.haul);
            CollectionAssert.AreEqual(na.eaten, nb.eaten);
            Assert.AreEqual(na.towers.Count, nb.towers.Count);
            for (int i = 0; i < na.towers.Count; i++)
            {
                Assert.AreEqual(na.towers[i].type, nb.towers[i].type);
                Assert.AreEqual(na.towers[i].col, nb.towers[i].col);
                Assert.AreEqual(na.towers[i].row, nb.towers[i].row);
                Assert.AreEqual(na.towers[i].lowHeat, nb.towers[i].lowHeat);
                Assert.AreEqual(na.towers[i].level, nb.towers[i].level);
                Assert.AreEqual(na.towers[i].kills, nb.towers[i].kills);
            }
            Assert.AreEqual(na.monsters.Count, nb.monsters.Count);
            for (int i = 0; i < na.monsters.Count; i++)
            {
                Assert.AreEqual(na.monsters[i].id, nb.monsters[i].id);
                Assert.AreEqual(na.monsters[i].type, nb.monsters[i].type);
                Assert.AreEqual(na.monsters[i].distance, nb.monsters[i].distance, 0.001f);
                Assert.AreEqual(na.monsters[i].hp, nb.monsters[i].hp, 0.001f);
            }
        }

        [Test]
        public void DaySave_KeepsMenuReservationAndRng()
        {
            var s = TestUtil.DaySession(5, 2, TestUtil.Item("grilled_cheese", 1), TestUtil.Item("grilled_cheese", 1), TestUtil.Item("potato_strips", 1));
            s.Run.reservation = new Reservation { active = true, day = 2, party = "학교 앞 학생들", recipeId = "cheese_skewer", servings = 1, rewardGold = 60, rewardRep = 1 };
            s.ToggleMenu("candied_potato");
            var restored = new GameSession(FromJson(ToJson(s.Run)));
            Assert.AreEqual(GamePhase.DayMenu, restored.Phase);
            CollectionAssert.AreEqual(s.Run.dayState.menu, restored.Run.dayState.menu);
            Assert.AreEqual("cheese_skewer", restored.Run.reservation.recipeId);
            Assert.AreEqual(s.Run.rng.state, restored.Run.rng.state);
            var a = s.RunBusiness();
            var b = restored.RunBusiness();
            Assert.AreEqual(a.totalGold, b.totalGold);
            Assert.AreEqual(a.served, b.served);
            Assert.AreEqual(s.Run.reputation, restored.Run.reputation);
            CollectionAssert.AreEqual(TestUtil.Ids(s.Run.storage), TestUtil.Ids(restored.Run.storage));
        }
    }
}
