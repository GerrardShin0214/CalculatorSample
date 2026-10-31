using KitchenDefense.Core;
using NUnit.Framework;

namespace KitchenDefense.Tests
{
    public class DayLogicTests
    {
        [Test]
        public void Spoilage_ThreeDaysFresh_SixDaysFrozen()
        {
            var s = TestUtil.DaySession(1, 4,
                TestUtil.Item("raw_meat", 0),
                TestUtil.Item("raw_meat", 1),
                TestUtil.Item("frozen_meat", 0));
            var spoiled = DayLogic.RemoveSpoiled(s.Run);
            CollectionAssert.AreEqual(new[] { "raw_meat" }, spoiled);
            CollectionAssert.AreEqual(new[] { "raw_meat", "frozen_meat" }, TestUtil.Ids(s.Run.storage));
            Assert.AreEqual(1, DayLogic.DaysLeft(4, s.Run.storage[0]), "오늘이 마지막 날");
            Assert.AreEqual(3, DayLogic.DaysLeft(4, s.Run.storage[1]));
        }

        [Test]
        public void FrozenRaw_SubstitutesSameMonster_AtThirtyPercentOff()
        {
            var s = TestUtil.DaySession(1, 1, TestUtil.Item("frozen_meat", 0), TestUtil.Item("grilled_mushroom", 0));
            var bbq = GameData.Recipe("bbq_platter");
            var taken = new bool[s.Run.storage.Count];
            bool frozen;
            Assert.IsTrue(DayLogic.TryAllocate(s.Run.storage, taken, s.Run.day, bbq, null, out frozen));
            Assert.IsTrue(frozen);
            Assert.AreEqual(32, DayLogic.DishPrice(s.Run, bbq, true), "45G의 70% = 31.5 → 32");
            Assert.AreEqual(45, DayLogic.DishPrice(s.Run, bbq, false));
        }

        [Test]
        public void FrozenRaw_CannotReplaceOtherMonster_OrAnExactFrozenIngredient()
        {
            var s = TestUtil.DaySession(1, 1, TestUtil.Item("frozen_crab", 0), TestUtil.Item("grilled_jelly", 0));
            var taken = new bool[s.Run.storage.Count];
            bool frozen;
            Assert.IsFalse(DayLogic.TryAllocate(s.Run.storage, taken, 1, GameData.Recipe("bbq_platter"), null, out frozen));
            Assert.IsFalse(DayLogic.TryAllocate(s.Run.storage, taken, 1, GameData.Recipe("slime_sherbet"), null, out frozen),
                "셔벗은 얼린 젤리 그 자체가 필요하다");
            CollectionAssert.AreEqual(new[] { false, false }, taken, "실패하면 아무것도 쓰지 않는다");
        }

        [Test]
        public void Reservation_Success_PaysRewardAndReputation()
        {
            var s = TestUtil.DaySession(1, 2, TestUtil.Item("grilled_ribs", 1), TestUtil.Item("grilled_mushroom", 1), TestUtil.Item(GameData.Scraps, 1));
            s.Run.reservation = new Reservation { active = true, day = 2, party = "기사단", recipeId = "bbq_platter", servings = 1, rewardGold = 100, rewardRep = 1 };
            int gold = s.Run.gold;
            var report = s.RunBusiness();
            Assert.IsTrue(report.reservationSuccess);
            Assert.AreEqual(100, report.reservationGold);
            Assert.AreEqual(1, s.Run.reputation);
            Assert.AreEqual(1, report.scrapsSold, "잡재료는 오늘의 백반으로");
            Assert.AreEqual(gold + 100 + Balance.ScrapsMealPrice, s.Run.gold);
            Assert.AreEqual(0, s.Run.storage.Count);
            Assert.IsFalse(s.Run.reservation.active);
            CollectionAssert.Contains(s.Run.stats.discovered, "bbq_platter");
        }

        [Test]
        public void Reservation_WithFrozenSubstitute_PaysThirtyPercentLess()
        {
            var s = TestUtil.DaySession(1, 2, TestUtil.Item("frozen_meat", 1), TestUtil.Item("grilled_mushroom", 1));
            s.Run.reservation = new Reservation { active = true, day = 2, recipeId = "bbq_platter", servings = 1, rewardGold = 100, rewardRep = 1 };
            var report = s.RunBusiness();
            Assert.IsTrue(report.reservationSuccess);
            Assert.IsTrue(report.reservationUsedFrozen);
            Assert.AreEqual(70, report.reservationGold);
        }

        [Test]
        public void Reservation_Failure_CostsReputation_AndKeepsIngredients()
        {
            var s = TestUtil.DaySession(1, 2, TestUtil.Item("grilled_ribs", 1));
            s.Run.reputation = 2;
            s.Run.reservation = new Reservation { active = true, day = 2, recipeId = "bbq_platter", servings = 1, rewardGold = 100, rewardRep = 1 };
            var report = s.RunBusiness();
            Assert.IsFalse(report.reservationSuccess);
            Assert.AreEqual(1, s.Run.reputation);
            Assert.AreEqual(1, s.Run.stats.reservationsFailed);
            CollectionAssert.AreEqual(new[] { "grilled_ribs" }, TestUtil.Ids(s.Run.storage));
        }

        [Test]
        public void Business_SellsMenuDishes_UntilIngredientsRunOut()
        {
            var s = TestUtil.DaySession(3, 1,
                TestUtil.Item("grilled_mushroom", 0), TestUtil.Item("grilled_mushroom", 0),
                TestUtil.Item("grilled_mushroom", 0), TestUtil.Item("grilled_mushroom", 0),
                TestUtil.Item("grilled_jelly", 0), TestUtil.Item("grilled_jelly", 0));
            Assert.IsTrue(s.ToggleMenu("mushroom_skewer"));
            Assert.IsTrue(s.ToggleMenu("caramel_jelly"));
            var report = s.RunBusiness();
            Assert.AreEqual(4, report.customers);
            Assert.AreEqual(3, report.served);
            Assert.AreEqual(2 * 20 + 25, report.dishGold);
            Assert.AreEqual(0, report.tips, "손님을 다 받지 못하면 팁 없음");
            Assert.AreEqual(0, s.Run.storage.Count);
        }

        [Test]
        public void Business_ServingEveryone_WithTwoDishes_GivesTipsAndReputation()
        {
            var s = TestUtil.DaySession(5, 1,
                TestUtil.Item("grilled_mushroom", 0), TestUtil.Item("grilled_mushroom", 0),
                TestUtil.Item("grilled_mushroom", 0), TestUtil.Item("grilled_mushroom", 0),
                TestUtil.Item("grilled_jelly", 0), TestUtil.Item("grilled_jelly", 0),
                TestUtil.Item("grilled_jelly", 0), TestUtil.Item("grilled_jelly", 0));
            s.ToggleMenu("mushroom_skewer");
            s.ToggleMenu("caramel_jelly");
            var report = s.RunBusiness();
            Assert.AreEqual(4, report.served);
            Assert.AreEqual(90, report.dishGold);
            Assert.AreEqual(9, report.tips);
            Assert.AreEqual(1, report.satisfactionRep);
            Assert.AreEqual(1, s.Run.reputation);
        }

        [Test]
        public void Menu_RespectsSlotLimit()
        {
            var s = TestUtil.DaySession(1, 1);
            Assert.IsTrue(s.ToggleMenu("slime_sherbet"));
            Assert.IsTrue(s.ToggleMenu("mushroom_skewer"));
            Assert.IsTrue(s.ToggleMenu("caramel_jelly"));
            Assert.IsFalse(s.ToggleMenu("boar_yukhoe"), "기본 슬롯 3개");
            Assert.IsTrue(s.ToggleMenu("caramel_jelly"), "내리기");
            Assert.IsTrue(s.ToggleMenu("boar_yukhoe"));
        }

        [Test]
        public void MenuOptions_ReserveTodaysReservationFirst()
        {
            var s = TestUtil.DaySession(1, 2, TestUtil.Item("raw_meat", 1), TestUtil.Item("raw_meat", 1), TestUtil.Item("raw_meat", 1), TestUtil.Item("raw_meat", 1));
            s.Run.reservation = new Reservation { active = true, day = 2, recipeId = "boar_yukhoe", servings = 1, rewardGold = 80, rewardRep = 1 };
            foreach (var o in DayLogic.MenuOptions(s.Run))
                if (o.recipe.id == "boar_yukhoe") Assert.AreEqual(1, o.servings, "4개 중 2개는 예약분");
            Assert.AreEqual(ReservationReadiness.Ready, DayLogic.TodayReservationReadiness(s.Run));
        }

        [Test]
        public void Closing_DumpsCheapestOverflowIntoSetMeal()
        {
            var s = TestUtil.DaySession(1, 1);
            for (int i = 0; i < 8; i++) s.Run.storage.Add(TestUtil.Item("grilled_ribs", 0));
            s.Run.storage.Add(TestUtil.Item(GameData.Scraps, 0));
            s.Run.storage.Add(TestUtil.Item("grilled_jelly", 0));
            int gold = s.Run.gold;
            DayLogic.ResolveOverflow(s.Run);
            Assert.AreEqual(8, s.Run.storage.Count);
            CollectionAssert.AreEquivalent(new[] { GameData.Scraps, "grilled_jelly" }, s.Run.dayState.dumped);
            Assert.AreEqual(gold + 2 * Balance.ScrapsMealPrice, s.Run.gold);
        }

        [Test]
        public void Shop_UpgradesStackUpToMaxLevel()
        {
            var s = TestUtil.DaySession(1, 1);
            s.Run.gold = 10000;
            var item = GameData.ShopItem("storage");
            for (int i = 0; i < item.maxLevel; i++)
            {
                var offer = new ShopOffer { itemId = item.id, price = DayLogic.Price(s.Run, item) };
                Assert.IsTrue(DayLogic.Buy(s.Run, offer));
                Assert.IsTrue(offer.sold);
                Assert.IsFalse(DayLogic.Buy(s.Run, offer), "같은 상품은 하루 한 번");
            }
            Assert.AreEqual(Balance.BaseStorage + item.maxLevel * Balance.StoragePerUpgrade, s.Run.StorageCapacity);
            Assert.IsFalse(DayLogic.CanBuy(s.Run, new ShopOffer { itemId = item.id, price = 1 }), "최대 단계");
        }

        [Test]
        public void Shop_RollsFourDistinctOffers()
        {
            var s = TestUtil.DaySession(9, 1);
            var offers = DayLogic.RollShop(s.Run);
            Assert.AreEqual(Balance.ShopOffersPerDay, offers.Count);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var o in offers) Assert.IsTrue(seen.Add(o.itemId));
        }

        [Test]
        public void Repair_RestoresDurability()
        {
            var s = TestUtil.DaySession(1, 1);
            s.Run.durability = 5;
            s.Run.gold = 100;
            Assert.IsTrue(DayLogic.Repair(s.Run));
            Assert.AreEqual(5 + Balance.RepairAmount, s.Run.durability);
            Assert.AreEqual(100 - Balance.RepairPrice, s.Run.gold);
        }

        [Test]
        public void Reservations_PayAtLeastDoubleTheMenuPrice()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var s = TestUtil.DaySession(seed, 1);
                for (int day = 1; day <= Balance.SeasonDays; day++)
                {
                    var r = DayLogic.RollReservation(s.Run, day);
                    var recipe = GameData.Recipe(r.recipeId);
                    Assert.IsTrue(r.active);
                    Assert.GreaterOrEqual(r.servings, 1);
                    Assert.LessOrEqual(r.servings * recipe.ingredients.Length, s.Run.StorageCapacity);
                    Assert.GreaterOrEqual(r.rewardGold, 2 * recipe.price * r.servings, recipe.name);
                    if (day == 1) Assert.LessOrEqual(recipe.price, 25, "첫 예약은 쉬운 요리");
                }
            }
        }
    }
}

namespace KitchenDefense.Tests
{
    public class KoreanTextTests
    {
        [Test]
        public void Particles_FollowFinalConsonant()
        {
            Assert.AreEqual("뿔멧돼지를", KoreanText.Eul("뿔멧돼지"));
            Assert.AreEqual("걷는 버섯을", KoreanText.Eul("걷는 버섯"));
            Assert.AreEqual("그릴로", KoreanText.Euro("그릴"), "ㄹ 받침은 '로'");
            Assert.AreEqual("가마솥으로", KoreanText.Euro("가마솥"));
            Assert.AreEqual("냉동고로", KoreanText.Euro("냉동고"));
            Assert.AreEqual("뿔멧돼지를 그릴로 막타", GameData.HowToGet("grilled_ribs"));
        }
    }
}
