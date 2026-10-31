using System;

namespace KitchenDefense.Core
{
    /// <summary>
    /// 전투 공식 모음. 실제 시뮬레이션(NightSimulation)과 처치 예상(KillPredictor)이
    /// 같은 공식을 쓰도록 여기 한곳에 둔다.
    /// </summary>
    public static class Combat
    {
        public static int StartFirepower(RunState run)
        {
            return Balance.NightBaseFirepower + Balance.FirepowerPerNight * run.day + Balance.FirepowerPerUpgrade * run.upgrades.firepower;
        }

        public static float MonsterMaxHp(RunState run, MonsterDef def, int night)
        {
            return def.hp * (1f + Balance.NightHpGrowth * night + Balance.ReputationHpGrowth * run.reputation);
        }

        /// <summary>기구 피해 배율: 임시 강화 × 상점 영구 강화 × 약불.</summary>
        public static float DamageMultiplier(RunState run, TowerState t)
        {
            float m = 1f + Balance.ToolUpgradeDamageBonus * t.level;
            switch (t.type)
            {
                case ToolType.Knife: m *= 1f + Balance.ShopDamageBonus * run.upgrades.knife; break;
                case ToolType.Grill: m *= 1f + Balance.ShopDamageBonus * run.upgrades.grill; break;
                case ToolType.Freezer: m *= 1f + Balance.ShopDamageBonus * run.upgrades.freezer; break;
            }
            if (t.lowHeat && t.type != ToolType.Cauldron) m *= Balance.LowHeatMultiplier;
            return m;
        }

        public static float Interval(RunState run, TowerState t)
        {
            var def = GameData.Tool(t.type);
            if (t.type != ToolType.Cauldron) return def.interval;
            return def.interval
                * MathUtil.Pow(Balance.ShopCauldronCooldownMultiplier, run.upgrades.cauldron)
                * MathUtil.Pow(Balance.CauldronUpgradeCooldownMultiplier, t.level);
        }

        public static float Range(TowerState t) { return GameData.Tool(t.type).range; }

        public static float KnifeDamage(RunState run, TowerState t, MonsterDef target)
        {
            return GameData.Tool(t.type).damage * DamageMultiplier(run, t) * target.knifeMultiplier;
        }

        public static float FreezerDamage(RunState run, TowerState t)
        {
            return GameData.Tool(t.type).damage * DamageMultiplier(run, t);
        }

        public static float BurnDps(RunState run, TowerState t, MonsterDef target)
        {
            return GameData.Tool(t.type).burnDps * DamageMultiplier(run, t) * target.heatMultiplier;
        }

        public static float SlowDuration(RunState run, TowerState t)
        {
            return GameData.Tool(t.type).slowDuration + Balance.ShopFreezerSlowBonus * run.upgrades.freezer;
        }

        public static float CauldronThreshold() { return GameData.Tool(ToolType.Cauldron).killThreshold; }

        /// <summary>임시 강화 비용. 최대 단계면 -1.</summary>
        public static int UpgradeCost(TowerState t)
        {
            return t.level < Balance.MaxToolLevel ? Balance.ToolUpgradeCosts[t.level] : -1;
        }

        public static bool InRange(KitchenMap map, TowerState t, Vec2 p)
        {
            float r = Range(t);
            return Vec2.SqrDistance(map.CellCenter(t.col, t.row), p) <= r * r;
        }

        public static bool IsCharging(KitchenMap map, MonsterDef def, float distance, float slowTimer, float straightTimer)
        {
            return def.chargeMultiplier > 1f
                && slowTimer <= 0f
                && straightTimer >= def.chargeDelay
                && map.DistanceToSegmentEnd(distance) > 0.5f;
        }

        /// <summary>
        /// 몬스터 한 마리를 dt만큼 전진시킨다. 감속 중에는 멧돼지 돌진이 끊긴다
        /// (기획서: 빠른 멧돼지는 앞쪽 냉동고로 잡는다).
        /// </summary>
        public static void Advance(KitchenMap map, MonsterDef def, ref float distance, ref float slowTimer, ref float slowFactor,
            ref float straightTimer, ref int segment, float dt)
        {
            float speed = def.speed;
            bool slowed = slowTimer > 0f;
            if (slowed)
            {
                speed *= slowFactor;
                slowTimer = Math.Max(0f, slowTimer - dt);
                if (slowTimer <= 0f) slowFactor = 1f;
                straightTimer = 0f;
            }
            if (def.chargeMultiplier > 1f)
            {
                int seg = map.SegmentAt(distance);
                if (seg != segment)
                {
                    segment = seg;
                    straightTimer = 0f;
                }
                if (!slowed) straightTimer += dt;
                if (IsCharging(map, def, distance, slowTimer, straightTimer)) speed *= def.chargeMultiplier;
            }
            distance += speed * dt;
        }
    }
}
