using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    /// <summary>
    /// 밤의 웨이브 구성. 0일 밤(개업 전야)은 생쥐 떼만 나오는 고정 튜토리얼이고, 이후는 예산 기반 무작위.
    /// 이야기 순서대로 동물이 하나씩 합류한다(파리 떼 1일 밤, 멧돼지 2일 밤, 너구리 3일 밤).
    /// 받은 예약에 필요한 동물은 반드시 섞어 넣는다(미끼 시스템은 다음 단계).
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
                lists.Add(new List<MonsterType> { MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang });
                lists.Add(new List<MonsterType> { MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang });
                lists.Add(new List<MonsterType> { MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang, MonsterType.MouseGang });
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

            // 예약 재료를 물고 오는 동물을 필요 수 + 1마리 섞는다.
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
            MonsterType[] all = { MonsterType.MouseGang, MonsterType.FlySwarm, MonsterType.Boar, MonsterType.Raccoon };
            float[] baseWeights = { 1f, 0.6f, 1f, 0.8f };
            var pool = new List<MonsterType>();
            var weights = new List<float>();
            for (int i = 0; i < all.Length; i++)
            {
                int intro = GameData.Monster(all[i]).introNight;
                if (intro > night) continue;
                pool.Add(all[i]);
                // 그날 밤 이야기에서 처음 소개되는 동물은 더 자주 나온다.
                weights.Add(baseWeights[i] * (intro == night ? 1.6f : 1f));
            }
            int guard = 0;
            while (budget >= 3 && guard++ < 64)
            {
                float total = 0f;
                for (int i = 0; i < pool.Count; i++)
                    if (GameData.Monster(pool[i]).cost <= budget) total += weights[i];
                if (total <= 0f) break;
                float roll = rng.NextFloat() * total;
                int pick = -1;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (GameData.Monster(pool[i]).cost > budget) continue;
                    pick = i;
                    roll -= weights[i];
                    if (roll <= 0f) break;
                }
                if (pick < 0) break;
                list.Add(pool[pick]);
                budget -= GameData.Monster(pool[pick]).cost;
            }
            return list;
        }

        static float BaseDelay(MonsterType type)
        {
            switch (type)
            {
                case MonsterType.MouseGang: return 1.3f;
                case MonsterType.FlySwarm: return 1.1f;
                case MonsterType.Boar: return 1.0f;
                case MonsterType.Raccoon: return 1.5f;
                default: return 1f;
            }
        }
    }
}
