using System.Collections.Generic;
using KitchenDefense.Core;

namespace KitchenDefense.Tests
{
    /// <summary>테스트용 도우미. Unity Test Runner와 dotnet test 양쪽에서 같이 쓴다.</summary>
    public static class TestUtil
    {
        public const float Dt = 1f / 30f;

        /// <summary>예약 없이 0일 밤 준비 상태의 세션. 화력은 넉넉하게 준다.</summary>
        public static GameSession NightSession(int seed = 7, int firepower = 9999)
        {
            var s = GameSession.NewSeason(seed);
            s.DeclineOffer();
            s.StartNight();
            s.Night.State.firepower = firepower;
            return s;
        }

        /// <summary>현재 밤을 지정한 몬스터만 나오는 단일 웨이브로 바꾼다.</summary>
        public static void SetSingleWave(NightSimulation sim, params MonsterType[] types)
        {
            var n = sim.State;
            n.waves.Clear();
            var w = new WaveDef();
            foreach (var t in types) w.spawns.Add(new SpawnEntry { type = t, delay = 0.5f });
            n.waves.Add(w);
            n.waveIndex = 0;
            n.waveActive = false;
            n.finished = false;
        }

        public static void Build(NightSimulation sim, ToolType type, int col, int row)
        {
            var t = sim.Build(type, col, row);
            if (t == null) throw new System.InvalidOperationException("설치 실패: " + type + " " + col + "," + row + " → " + sim.CheckBuild(type, col, row));
        }

        /// <summary>웨이브가 끝날 때까지(또는 시간 초과) 진행한다.</summary>
        public static float RunWave(NightSimulation sim, float maxSeconds = 180f)
        {
            float t = 0f;
            while (sim.State.waveActive && t < maxSeconds)
            {
                sim.Step(Dt);
                sim.Events.Clear();
                t += Dt;
            }
            return t;
        }

        /// <summary>첫 몬스터가 나올 때까지만 진행한다.</summary>
        public static MonsterState StepUntilSpawn(NightSimulation sim)
        {
            for (int i = 0; i < 1000 && sim.State.monsters.Count == 0; i++)
            {
                sim.Step(Dt);
                sim.Events.Clear();
            }
            return sim.State.monsters.Count > 0 ? sim.State.monsters[0] : null;
        }

        public static StoredIngredient Item(string id, int night)
        {
            return new StoredIngredient { id = id, night = night };
        }

        public static List<string> Ids(List<StoredIngredient> storage)
        {
            var list = new List<string>();
            foreach (var s in storage) list.Add(s.id);
            return list;
        }

        /// <summary>낮 메뉴 단계까지 바로 간 세션.</summary>
        public static GameSession DaySession(int seed, int day, params StoredIngredient[] storage)
        {
            var s = GameSession.NewSeason(seed);
            s.DeclineOffer();
            s.StartNight();
            s.Night.State.finished = true;
            s.EndNight();
            s.BeginDay();
            s.Run.day = day;
            s.Run.storage.Clear();
            s.Run.storage.AddRange(storage);
            return s;
        }
    }
}
