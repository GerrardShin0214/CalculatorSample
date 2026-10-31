using System;

namespace KitchenDefense.Core
{
    public struct KillPrediction
    {
        /// <summary>false면 예상할 수 없음(시간 안에 끝나지 않음).</summary>
        public bool valid;
        /// <summary>true면 이대로 두면 창고까지 간다.</summary>
        public bool leak;
        public int towerId;
        public ToolType tool;
        public CookMethod method;
        /// <summary>예상 막타로 나올 재료(분열체 등은 null).</summary>
        public string ingredientId;
        public float seconds;
    }

    /// <summary>
    /// 처치 예상 아이콘: "지금 상태로 막타를 칠 기구"를 계산한다.
    /// 그 몬스터 한 마리만 남은 것처럼 앞으로 굴려 보는 근사치라서
    /// 다른 몬스터에게 빼앗기는 공격은 고려하지 않는다. 공식은 Combat을 그대로 쓴다.
    /// </summary>
    public sealed class KillPredictor
    {
        public float StepSeconds = 0.1f;
        public float HorizonSeconds = 45f;

        float[] cooldowns = new float[16];
        float[] rates = new float[16];

        public KillPrediction Predict(NightSimulation sim, MonsterState m)
        {
            var result = new KillPrediction();
            if (sim == null || m == null || !m.alive) return result;

            var run = sim.Run;
            var map = sim.Map;
            var towers = sim.State.towers;
            var def = GameData.Monster(m.type);
            int tc = towers.Count;
            if (cooldowns.Length < tc)
            {
                cooldowns = new float[tc * 2];
                rates = new float[tc * 2];
            }
            for (int i = 0; i < tc; i++)
            {
                cooldowns[i] = towers[i].cooldown;
                rates[i] = sim.TowerRate(towers[i]);
            }

            float hp = m.hp;
            float distance = m.distance;
            float slowTimer = m.slowTimer;
            float slowFactor = m.slowFactor;
            float straightTimer = m.straightTimer;
            int segment = m.segment;
            float burnTimer = m.burnTimer;
            float burnDps = m.burnDps;
            int burnTowerId = m.burnTowerId;
            float threshold = Combat.CauldronThreshold();
            float dt = StepSeconds;

            for (float time = 0f; time < HorizonSeconds; time += dt)
            {
                Vec2 p = map.PositionAt(distance);

                for (int i = 0; i < tc; i++)
                {
                    cooldowns[i] -= dt * rates[i];
                    if (cooldowns[i] > 0f) continue;
                    var t = towers[i];
                    bool fired = false;
                    switch (t.type)
                    {
                        case ToolType.Knife:
                            if (Combat.InRange(map, t, p))
                            {
                                fired = true;
                                hp -= Combat.KnifeDamage(run, t, def);
                                if (hp <= 0.0001f) return Hit(t.id, t.type, def, time);
                            }
                            break;
                        case ToolType.Grill:
                            if (Combat.InRange(map, t, p))
                            {
                                fired = true;
                                burnDps = Combat.BurnDps(run, t, def);
                                burnTimer = GameData.Tool(t.type).burnDuration;
                                burnTowerId = t.id;
                            }
                            break;
                        case ToolType.Freezer:
                            if (Combat.InRange(map, t, p))
                            {
                                fired = true;
                                slowTimer = Math.Max(slowTimer, Combat.SlowDuration(run, t));
                                slowFactor = GameData.Tool(t.type).slowFactor;
                                hp -= Combat.FreezerDamage(run, t);
                                if (hp <= 0.0001f) return Hit(t.id, t.type, def, time);
                            }
                            break;
                        case ToolType.Cauldron:
                            if (hp <= m.maxHp * threshold + 0.0001f && map.CellContains(t.col, t.row, p))
                                return Hit(t.id, t.type, def, time);
                            break;
                    }
                    cooldowns[i] = fired ? cooldowns[i] + Combat.Interval(run, t) : 0f;
                }

                if (burnTimer > 0f)
                {
                    float tick = Math.Min(dt, burnTimer);
                    burnTimer -= dt;
                    hp -= burnDps * tick;
                    if (hp <= 0.0001f) return Hit(burnTowerId, ToolType.Grill, def, time);
                }

                Combat.Advance(map, def, ref distance, ref slowTimer, ref slowFactor, ref straightTimer, ref segment, dt);
                if (distance >= map.Length)
                {
                    result.valid = true;
                    result.leak = true;
                    result.seconds = time;
                    return result;
                }
            }
            return result;
        }

        static KillPrediction Hit(int towerId, ToolType tool, MonsterDef def, float time)
        {
            var method = GameData.Tool(tool).method;
            return new KillPrediction
            {
                valid = true,
                towerId = towerId,
                tool = tool,
                method = method,
                ingredientId = GameData.IngredientFor(def, method),
                seconds = time,
            };
        }
    }
}
