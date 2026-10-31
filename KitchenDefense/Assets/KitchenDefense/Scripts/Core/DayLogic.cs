using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    public struct MenuOption
    {
        public RecipeDef recipe;
        /// <summary>오늘 만들 수 있는 최대 인분(예약분을 먼저 떼어 둔 뒤).</summary>
        public int servings;
        /// <summary>그중 얼린 원재료로 대체해야 하는 인분(판매가 -30%).</summary>
        public int frozenServings;
        public bool selected;
    }

    public struct IngredientNeed
    {
        public string ingredientId;
        public int needed;
        /// <summary>예약 날짜까지 상하지 않는, 정확히 같은 재료 수.</summary>
        public int have;
        /// <summary>대신 쓸 수 있는 얼린 원재료 수(같은 몬스터).</summary>
        public int frozenSubstitutes;
    }

    public enum ReservationReadiness { None, Ready, ReadyWithFrozen, Missing }

    /// <summary>
    /// 낮 — 경영 시스템. 결정 세 번(메뉴 짜기 → 영업 → 정비·마감)으로 1분 안에 끝난다.
    /// </summary>
    public static class DayLogic
    {
        // ───────────────────────── 보관 ─────────────────────────

        public static int ShelfLife(string id)
        {
            return GameData.Ingredient(id).frozen ? Balance.FrozenShelfLife : Balance.ShelfLife;
        }

        /// <summary>오늘을 포함해 며칠 더 쓸 수 있는지. 1이면 오늘이 마지막.</summary>
        public static int DaysLeft(int today, StoredIngredient s)
        {
            return ShelfLife(s.id) - (today - s.night) + 1;
        }

        public static bool IsFreshOn(int day, StoredIngredient s)
        {
            return day - s.night <= ShelfLife(s.id);
        }

        /// <summary>아침에 상한 재료를 버린다. 재료는 3일, 냉동 재료는 6일 간다.</summary>
        public static List<string> RemoveSpoiled(RunState run)
        {
            var spoiled = new List<string>();
            for (int i = run.storage.Count - 1; i >= 0; i--)
            {
                if (IsFreshOn(run.day, run.storage[i])) continue;
                spoiled.Add(run.storage[i].id);
                run.storage.RemoveAt(i);
            }
            spoiled.Reverse();
            return spoiled;
        }

        // ───────────────────────── 재료 배정 ─────────────────────────

        static int FindBest(List<StoredIngredient> storage, bool[] taken, int today, string exactId, MonsterType frozenOf, bool exact)
        {
            int best = -1;
            int bestDays = int.MaxValue;
            for (int i = 0; i < storage.Count; i++)
            {
                if (taken[i]) continue;
                var s = storage[i];
                if (exact)
                {
                    if (s.id != exactId) continue;
                }
                else
                {
                    var d = GameData.Ingredient(s.id);
                    if (!d.frozen || d.monster != frozenOf) continue;
                }
                int days = DaysLeft(today, s);
                if (days < bestDays)
                {
                    bestDays = days;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 레시피 1인분에 쓸 재료를 고른다. 같은 재료를 먼저 쓰고(상하기 직전 것부터),
        /// 없으면 같은 몬스터의 얼린 원재료로 대체한다. 실패하면 아무것도 건드리지 않는다.
        /// </summary>
        public static bool TryAllocate(List<StoredIngredient> storage, bool[] taken, int today, RecipeDef recipe, List<int> picked, out bool usedFrozen)
        {
            usedFrozen = false;
            var need = recipe.ingredients;
            var chosen = new int[need.Length];
            for (int k = 0; k < need.Length; k++)
            {
                chosen[k] = FindBest(storage, taken, today, need[k], MonsterType.Slime, true);
                if (chosen[k] >= 0) taken[chosen[k]] = true;
            }
            for (int k = 0; k < need.Length; k++)
            {
                if (chosen[k] >= 0) continue;
                var want = GameData.Ingredient(need[k]);
                int idx = want.frozen || want.scraps ? -1 : FindBest(storage, taken, today, null, want.monster, false);
                if (idx < 0)
                {
                    for (int j = 0; j < need.Length; j++)
                        if (chosen[j] >= 0) taken[chosen[j]] = false;
                    usedFrozen = false;
                    return false;
                }
                chosen[k] = idx;
                taken[idx] = true;
                usedFrozen = true;
            }
            if (picked != null) picked.AddRange(chosen);
            return true;
        }

        public static int CountServings(List<StoredIngredient> storage, bool[] takenBase, int today, RecipeDef recipe, out int frozenServings)
        {
            var taken = (bool[])takenBase.Clone();
            int servings = 0;
            frozenServings = 0;
            bool usedFrozen;
            while (TryAllocate(storage, taken, today, recipe, null, out usedFrozen))
            {
                servings++;
                if (usedFrozen) frozenServings++;
                if (servings > 99) break;
            }
            return servings;
        }

        /// <summary>오늘 예약분을 먼저 떼어 둔 재료 표시. 예약을 못 채우면 아무것도 떼지 않는다.</summary>
        public static bool[] ReserveForToday(RunState run, out bool canFulfill, out bool needsFrozen)
        {
            var taken = new bool[run.storage.Count];
            canFulfill = false;
            needsFrozen = false;
            var res = run.reservation;
            if (!res.active || res.day != run.day) return taken;
            var recipe = GameData.Recipe(res.recipeId);
            if (recipe == null) return taken;
            var trial = (bool[])taken.Clone();
            for (int s = 0; s < res.servings; s++)
            {
                bool f;
                if (!TryAllocate(run.storage, trial, run.day, recipe, null, out f)) return taken;
                needsFrozen |= f;
            }
            canFulfill = true;
            return trial;
        }

        public static ReservationReadiness TodayReservationReadiness(RunState run)
        {
            var res = run.reservation;
            if (!res.active || res.day != run.day) return ReservationReadiness.None;
            bool ok, frozen;
            ReserveForToday(run, out ok, out frozen);
            if (!ok) return ReservationReadiness.Missing;
            return frozen ? ReservationReadiness.ReadyWithFrozen : ReservationReadiness.Ready;
        }

        public static List<MenuOption> MenuOptions(RunState run)
        {
            bool ok, frozen;
            var taken = ReserveForToday(run, out ok, out frozen);
            var list = new List<MenuOption>();
            foreach (var r in GameData.Recipes)
            {
                int f;
                int servings = CountServings(run.storage, taken, run.day, r, out f);
                list.Add(new MenuOption { recipe = r, servings = servings, frozenServings = f, selected = run.dayState.menu.Contains(r.id) });
            }
            return list;
        }

        /// <summary>밤 화면의 '내일 예약' 목표: 재료별 필요/보유 수.</summary>
        public static List<IngredientNeed> ReservationNeeds(RunState run, Reservation res)
        {
            var list = new List<IngredientNeed>();
            if (res == null || !res.active) return list;
            var recipe = GameData.Recipe(res.recipeId);
            if (recipe == null) return list;
            var order = new List<string>();
            var needed = new Dictionary<string, int>();
            for (int s = 0; s < res.servings; s++)
                foreach (var id in recipe.ingredients)
                {
                    int c;
                    if (!needed.TryGetValue(id, out c)) order.Add(id);
                    needed[id] = c + 1;
                }
            foreach (var id in order)
            {
                var want = GameData.Ingredient(id);
                int have = 0, frozen = 0;
                foreach (var s in run.storage)
                {
                    if (!IsFreshOn(res.day, s)) continue;
                    if (s.id == id) have++;
                    else
                    {
                        var d = GameData.Ingredient(s.id);
                        if (!want.frozen && d.frozen && d.monster == want.monster) frozen++;
                    }
                }
                list.Add(new IngredientNeed { ingredientId = id, needed = needed[id], have = have, frozenSubstitutes = frozen });
            }
            return list;
        }

        /// <summary>이 재료가 아직 예약에 모자라는가(처치 예상 아이콘 강조용).</summary>
        public static bool IsStillNeeded(List<IngredientNeed> needs, string ingredientId)
        {
            if (needs == null || ingredientId == null) return false;
            for (int i = 0; i < needs.Count; i++)
                if (needs[i].ingredientId == ingredientId) return needs[i].have < needs[i].needed;
            return false;
        }

        // ───────────────────────── 영업 ─────────────────────────

        public static int CustomerCount(RunState run)
        {
            return Balance.BaseCustomers + Balance.CustomersPerReputation * run.reputation + Math.Max(0, run.day - 1);
        }

        public static float PriceMultiplier(RunState run)
        {
            return 1f + Balance.ReputationPriceBonus * run.reputation;
        }

        public static int DishPrice(RunState run, RecipeDef recipe, bool frozen)
        {
            float p = recipe.price * PriceMultiplier(run);
            if (frozen) p *= Balance.FrozenSubstitutePriceMultiplier;
            return MathUtil.Round(p);
        }

        /// <summary>
        /// 자동 영업(빨리 감기). 예약 → 일반 손님(메뉴판에서 랜덤 주문) → 잡재료는 오늘의 백반으로 판매.
        /// </summary>
        public static BusinessReport RunBusiness(RunState run)
        {
            var ds = run.dayState;
            var report = new BusinessReport();
            int startRep = run.reputation;
            report.repBefore = startRep;
            var storage = run.storage;
            var taken = new bool[storage.Count];

            // 1) 예약 손님
            var res = run.reservation;
            if (res.active && res.day == run.day)
            {
                var recipe = GameData.Recipe(res.recipeId);
                report.hadReservation = true;
                report.reservationParty = res.party;
                report.reservationRecipeId = res.recipeId;
                report.reservationServings = res.servings;
                bool ok;
                bool frozen;
                var reserved = ReserveForToday(run, out ok, out frozen);
                if (ok && recipe != null)
                {
                    taken = reserved;
                    int gold = frozen ? MathUtil.Round(res.rewardGold * Balance.FrozenSubstitutePriceMultiplier) : res.rewardGold;
                    report.reservationSuccess = true;
                    report.reservationUsedFrozen = frozen;
                    report.reservationGold = gold;
                    report.reservationRep = res.rewardRep;
                    run.reputation += res.rewardRep;
                    run.stats.reservationsDone++;
                    run.stats.dishesSold += res.servings;
                    Discover(run, report, recipe.id);
                }
                else
                {
                    report.reservationRep = -1;
                    run.reputation = Math.Max(0, run.reputation - 1);
                    run.stats.reservationsFailed++;
                }
                res.active = false;
            }

            // 2) 일반 손님
            int customers = Balance.BaseCustomers + Balance.CustomersPerReputation * startRep + Math.Max(0, run.day - 1);
            float priceMult = 1f + Balance.ReputationPriceBonus * startRep;
            report.customers = customers;
            var menu = new List<RecipeDef>();
            foreach (var id in ds.menu)
            {
                var r = GameData.Recipe(id);
                if (r != null) menu.Add(r);
            }
            var lines = new SaleLine[menu.Count];
            for (int i = 0; i < menu.Count; i++) lines[i] = new SaleLine { recipeId = menu[i].id };
            var order = new List<int>();
            for (int i = 0; i < menu.Count; i++) order.Add(i);
            for (int c = 0; c < customers; c++)
            {
                run.rng.Shuffle(order);
                for (int k = 0; k < order.Count; k++)
                {
                    int idx = order[k];
                    bool usedFrozen;
                    if (!TryAllocate(storage, taken, run.day, menu[idx], null, out usedFrozen)) continue;
                    float p = menu[idx].price * priceMult;
                    if (usedFrozen) p *= Balance.FrozenSubstitutePriceMultiplier;
                    int price = MathUtil.Round(p);
                    lines[idx].count++;
                    lines[idx].gold += price;
                    if (usedFrozen) lines[idx].frozenCount++;
                    report.served++;
                    break;
                }
            }
            int distinct = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].count <= 0) continue;
                distinct++;
                report.sales.Add(lines[i]);
                report.dishGold += lines[i].gold;
                run.stats.dishesSold += lines[i].count;
                Discover(run, report, lines[i].recipeId);
            }

            // 3) 오늘의 백반: 잡재료는 동네 주민에게 헐값에 판다.
            for (int i = 0; i < storage.Count; i++)
            {
                if (taken[i] || storage[i].id != GameData.Scraps) continue;
                taken[i] = true;
                report.scrapsSold++;
            }
            report.scrapsGold = report.scrapsSold * Balance.ScrapsMealPrice;

            // 4) 팁과 만족도
            if (customers > 0 && report.served >= customers && report.dishGold > 0)
            {
                report.tips = MathUtil.Round(report.dishGold * Balance.TipRate);
                if (distinct >= 2)
                {
                    report.satisfactionRep = 1;
                    run.reputation += 1;
                }
            }

            // 5) 쓴 재료 빼기
            for (int i = storage.Count - 1; i >= 0; i--)
                if (taken[i]) storage.RemoveAt(i);

            report.totalGold = report.reservationGold + report.dishGold + report.scrapsGold + report.tips;
            run.gold += report.totalGold;
            run.stats.totalGold += report.totalGold;
            report.repAfter = run.reputation;
            ds.report = report;
            ds.businessDone = true;
            return report;
        }

        static void Discover(RunState run, BusinessReport report, string recipeId)
        {
            if (run.stats.discovered.Contains(recipeId)) return;
            run.stats.discovered.Add(recipeId);
            report.newDishes.Add(recipeId);
        }

        // ───────────────────────── 마감 ─────────────────────────

        /// <summary>창고 칸을 넘는 재료는 싼 것(같으면 먼저 상할 것)부터 오늘의 백반으로 턴다.</summary>
        public static void ResolveOverflow(RunState run)
        {
            var ds = run.dayState;
            ds.dumped.Clear();
            ds.dumpedGold = 0;
            int cap = run.StorageCapacity;
            while (run.storage.Count > cap)
            {
                int idx = 0;
                for (int i = 1; i < run.storage.Count; i++)
                {
                    var a = run.storage[i];
                    var b = run.storage[idx];
                    int va = GameData.Ingredient(a.id).value, vb = GameData.Ingredient(b.id).value;
                    if (va < vb || (va == vb && DaysLeft(run.day, a) < DaysLeft(run.day, b))) idx = i;
                }
                ds.dumped.Add(run.storage[idx].id);
                run.storage.RemoveAt(idx);
                ds.dumpedGold += Balance.ScrapsMealPrice;
            }
            run.gold += ds.dumpedGold;
            run.stats.totalGold += ds.dumpedGold;
        }

        public static int OverflowCount(RunState run)
        {
            return Math.Max(0, run.storage.Count - run.StorageCapacity);
        }

        // ───────────────────────── 상점 ─────────────────────────

        public static int Level(RunState run, ShopEffect effect)
        {
            var u = run.upgrades;
            switch (effect)
            {
                case ShopEffect.MenuSlot: return u.menuSlot;
                case ShopEffect.Storage: return u.storage;
                case ShopEffect.Knife: return u.knife;
                case ShopEffect.Grill: return u.grill;
                case ShopEffect.Freezer: return u.freezer;
                case ShopEffect.Cauldron: return u.cauldron;
                case ShopEffect.Firepower: return u.firepower;
                default: return 0;
            }
        }

        static void AddLevel(RunState run, ShopEffect effect)
        {
            var u = run.upgrades;
            switch (effect)
            {
                case ShopEffect.MenuSlot: u.menuSlot++; break;
                case ShopEffect.Storage: u.storage++; break;
                case ShopEffect.Knife: u.knife++; break;
                case ShopEffect.Grill: u.grill++; break;
                case ShopEffect.Freezer: u.freezer++; break;
                case ShopEffect.Cauldron: u.cauldron++; break;
                case ShopEffect.Firepower: u.firepower++; break;
                case ShopEffect.Repair: run.durability = Math.Min(Balance.MaxDurability, run.durability + Balance.RepairAmount); break;
            }
        }

        public static int Price(RunState run, ShopItemDef item)
        {
            return item.price + item.priceStep * Level(run, item.effect);
        }

        /// <summary>상점에는 매일 무작위 상품 4개가 나온다(수리는 항상 따로 있다).</summary>
        public static List<ShopOffer> RollShop(RunState run)
        {
            var pool = new List<ShopItemDef>();
            foreach (var item in GameData.ShopItems)
                if (Level(run, item.effect) < item.maxLevel) pool.Add(item);
            run.rng.Shuffle(pool);
            var offers = new List<ShopOffer>();
            for (int i = 0; i < pool.Count && offers.Count < Balance.ShopOffersPerDay; i++)
                offers.Add(new ShopOffer { itemId = pool[i].id, price = Price(run, pool[i]) });
            return offers;
        }

        public static bool CanBuy(RunState run, ShopOffer offer)
        {
            if (offer == null || offer.sold) return false;
            var item = GameData.ShopItem(offer.itemId);
            if (item == null || run.gold < offer.price) return false;
            return Level(run, item.effect) < item.maxLevel;
        }

        public static bool Buy(RunState run, ShopOffer offer)
        {
            if (!CanBuy(run, offer)) return false;
            var item = GameData.ShopItem(offer.itemId);
            run.gold -= offer.price;
            AddLevel(run, item.effect);
            offer.sold = true;
            return true;
        }

        public static bool CanRepair(RunState run)
        {
            return run.durability < Balance.MaxDurability && run.gold >= Balance.RepairPrice;
        }

        public static bool Repair(RunState run)
        {
            if (!CanRepair(run)) return false;
            run.gold -= Balance.RepairPrice;
            AddLevel(run, ShopEffect.Repair);
            return true;
        }

        // ───────────────────────── 예약 ─────────────────────────

        /// <summary>마감 때 공개되는 내일 예약. 보상은 일반 판매의 2배 이상, 못 지키면 평판 -1.</summary>
        public static Reservation RollReservation(RunState run, int forDay)
        {
            var rng = run.rng;
            int maxPrice = forDay <= 1 ? 25 : (forDay == 2 ? 45 : 999);
            var candidates = new List<RecipeDef>();
            foreach (var r in GameData.Recipes)
                if (r.price <= maxPrice) candidates.Add(r);
            var recipe = candidates[rng.Range(0, candidates.Count)];

            int servings;
            if (forDay <= 1) servings = 1;
            else if (forDay <= 3) servings = rng.Range(1, 3);
            else servings = rng.Range(2, 4);
            while (servings > 1 && servings * recipe.ingredients.Length > run.StorageCapacity) servings--;

            int reward = MathUtil.RoundTo(recipe.price * servings * Balance.ReservationRewardMultiplier + 10 * forDay, 10);
            string party = recipe.parties != null && recipe.parties.Length > 0 ? recipe.parties[rng.Range(0, recipe.parties.Length)] : "단골 손님";
            return new Reservation
            {
                active = true,
                day = forDay,
                party = party,
                recipeId = recipe.id,
                servings = servings,
                rewardGold = reward,
                rewardRep = 1,
            };
        }

        // ───────────────────────── 시즌 점수 ─────────────────────────

        public static int SeasonScore(RunState run)
        {
            return run.stats.totalGold + run.reputation * 100 + run.durability * 20 + run.stats.reservationsDone * 50;
        }

        public static string Rank(RunState run)
        {
            if (run.failed) return "F";
            int score = SeasonScore(run);
            if (score >= 2600) return "S";
            if (score >= 1900) return "A";
            if (score >= 1200) return "B";
            return "C";
        }
    }
}
