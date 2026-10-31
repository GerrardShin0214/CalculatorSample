using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    /// <summary>
    /// 밤의 웨이브 구성. 0일 밤(개업 전야)은 고정 튜토리얼, 이후는 예산 기반 무작위.
    /// 받은 예약에 필요한 몬스터는 반드시 섞어 넣는다(미끼 시스템은 다음 단계).
    /// </summary>
    public static class WaveGenerator
    {
        public static int WaveCount(int night)
        {
            return 3 + (night >= 2 ? 1 : 0) + (night >= 4 ? 1 : 0);
        }

        public static NightState CreateNight(RunState run)
        {
            var n = new NightState
            {
                index = run.day,
                firepower = Combat.StartFirepower(run),
            };
            n.waves = Generate(run, run.day, run.reservation);
            return n;
        }

        public static List<WaveDef> Generate(RunState run, int night, Reservation goal)
        {
            var rng = run.rng;
            var lists = new List<List<MonsterType>>();
            if (night == 0)
            {
                lists.Add(new List<MonsterType> { MonsterType.Slime, MonsterType.Slime, MonsterType.Slime, MonsterType.Slime });
                lists.Add(new List<MonsterType> { MonsterType.Slime, MonsterType.Mushroom, MonsterType.Slime, MonsterType.Mushroom, MonsterType.Mushroom });
                lists.Add(new List<MonsterType> { MonsterType.Boar, MonsterType.Mushroom, MonsterType.Boar, MonsterType.Crab, MonsterType.Mushroom, MonsterType.Boar });
            }
            else
            {
                int count = WaveCount(night);
                for (int w = 0; w < count; w++)
                {
                    int budget = 10 + 4 * night + 3 * w + run.reputation;
                    if (w == count - 1) budget += 4;
                    lists.Add(Fill(rng, budget, night));
                }
            }

            // 예약 재료가 나오는 몬스터를 필요 수 + 1마리 섞는다.
            if (goal != null && goal.active && goal.day == night + 1)
            {
                var needs = MonstersNeeded(goal);
                int cursor = night == 0 ? 1 : 0;
                foreach (var pair in needs)
                {
                    for (int k = 0; k < pair.Value + 1; k++)
                    {
                        lists[cursor % lists.Count].Add(pair.Key);
                        cursor++;
                    }
                }
            }

            var waves = new List<WaveDef>();
            for (int w = 0; w < lists.Count; w++)
            {
                var list = lists[w];
                if (!(night == 0 && w == 0)) rng.Shuffle(list);
                var wave = new WaveDef();
                for (int i = 0; i < list.Count; i++)
                {
                    float delay = i == 0 ? 0.8f : BaseDelay(list[i]) + rng.Range(-0.2f, 0.25f);
                    wave.spawns.Add(new SpawnEntry { type = list[i], delay = delay });
                }
                waves.Add(wave);
            }
            return waves;
        }

        /// <summary>예약을 채우려면 어떤 몬스터가 몇 마리 필요한지.</summary>
        public static List<KeyValuePair<MonsterType, int>> MonstersNeeded(Reservation goal)
        {
            var result = new List<KeyValuePair<MonsterType, int>>();
            var recipe = GameData.Recipe(goal.recipeId);
            if (recipe == null) return result;
            var counts = new int[GameData.Monsters.Length];
            for (int s = 0; s < goal.servings; s++)
                foreach (var id in recipe.ingredients)
                    counts[(int)GameData.Ingredient(id).monster]++;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > 0) result.Add(new KeyValuePair<MonsterType, int>((MonsterType)i, counts[i]));
            return result;
        }

        static List<MonsterType> Fill(Rng rng, int budget, int night)
        {
            var list = new List<MonsterType>();
            MonsterType[] pool = { MonsterType.Slime, MonsterType.Mushroom, MonsterType.Boar, MonsterType.Crab };
            float[] weights = { 1f, 1f, night >= 1 ? 1f : 0.6f, night >= 1 ? 0.8f : 0.3f };
            int guard = 0;
            while (budget >= 3 && guard++ < 64)
            {
                float total = 0f;
                for (int i = 0; i < pool.Length; i++)
                    if (GameData.Monster(pool[i]).cost <= budget) total += weights[i];
                if (total <= 0f) break;
                float roll = rng.NextFloat() * total;
                int pick = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (GameData.Monster(pool[i]).cost > budget) continue;
                    roll -= weights[i];
                    if (roll <= 0f)
                    {
                        pick = i;
                        break;
                    }
                    pick = i;
                }
                list.Add(pool[pick]);
                budget -= GameData.Monster(pool[pick]).cost;
            }
            return list;
        }

        static float BaseDelay(MonsterType type)
        {
            switch (type)
            {
                case MonsterType.Slime: return 1.3f;
                case MonsterType.Mushroom: return 1.2f;
                case MonsterType.Boar: return 1.0f;
                case MonsterType.Crab: return 1.5f;
                default: return 1f;
            }
        }
    }
}
