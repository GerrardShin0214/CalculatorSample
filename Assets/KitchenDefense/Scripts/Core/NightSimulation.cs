using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    public enum SimEventType
    {
        Spawned,
        Attack,
        Killed,
        Split,
        Leaked,
        WaveStarted,
        WaveCleared,
        NightCleared,
        SeasonFailed,
    }

    /// <summary>화면 연출용 이벤트. 시뮬레이션은 이벤트만 쌓고, 표시는 Unity 쪽이 맡는다.</summary>
    public struct SimEvent
    {
        public SimEventType type;
        public int monsterId;
        public MonsterType monsterType;
        public int towerId;
        public ToolType toolType;
        public CookMethod method;
        public string ingredientId;
        public string eatenId;
        public Vec2 position;
        public int amount;
    }

    public enum BuildResult
    {
        Ok,
        NotNow,
        OutOfBounds,
        Reserved,
        Occupied,
        NeedsPath,
        NeedsFloor,
        NoFirepower,
    }

    /// <summary>
    /// 밤 — 디펜스 시뮬레이션. 엔진과 무관한 순수 로직이라 테스트와 저장/복원이 쉽다.
    /// 막타 규칙: 몬스터가 죽는 순간 마지막 피해를 준 기구의 조리법이 재료를 정한다.
    /// </summary>
    public sealed class NightSimulation
    {
        public readonly KitchenMap Map;
        public readonly RunState Run;
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public NightSimulation(KitchenMap map, RunState run)
        {
            Map = map;
            Run = run;
        }

        public NightState State { get { return Run.night; } }

        /// <summary>웨이브 사이 준비 시간. 재배치가 무료다.</summary>
        public bool IsPrep { get { return !State.waveActive && !State.finished && !Run.failed; } }
        public bool HasNextWave { get { return State.waveIndex < State.waves.Count; } }
        public WaveDef NextWave { get { return HasNextWave ? State.waves[State.waveIndex] : null; } }

        public int AliveCount
        {
            get
            {
                int c = 0;
                var list = State.monsters;
                for (int i = 0; i < list.Count; i++) if (list[i].alive) c++;
                return c;
            }
        }

        /// <summary>현재 웨이브에서 아직 나오지 않은 몬스터 수.</summary>
        public int PendingSpawns
        {
            get
            {
                var n = State;
                if (!n.waveActive || n.waveIndex >= n.waves.Count) return 0;
                return Math.Max(0, n.waves[n.waveIndex].spawns.Count - n.spawnCursor);
            }
        }

        public TowerState TowerAt(int col, int row)
        {
            var towers = State.towers;
            for (int i = 0; i < towers.Count; i++)
                if (towers[i].col == col && towers[i].row == row) return towers[i];
            return null;
        }

        public TowerState TowerById(int id)
        {
            var towers = State.towers;
            for (int i = 0; i < towers.Count; i++)
                if (towers[i].id == id) return towers[i];
            return null;
        }

        public MonsterState MonsterById(int id)
        {
            var list = State.monsters;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i];
            return null;
        }

        // ───────────────────────── 배치 ─────────────────────────

        BuildResult CheckCell(ToolType type, int col, int row, TowerState ignore)
        {
            if (!Map.InBounds(col, row)) return BuildResult.OutOfBounds;
            if (Map.IsReserved(col, row)) return BuildResult.Reserved;
            var occupant = TowerAt(col, row);
            if (occupant != null && occupant != ignore) return BuildResult.Occupied;
            bool path = Map.IsPath(col, row);
            var def = GameData.Tool(type);
            if (def.onPath && !path) return BuildResult.NeedsPath;
            if (!def.onPath && path) return BuildResult.NeedsFloor;
            return BuildResult.Ok;
        }

        /// <summary>칸 모양만 보고 이 기구를 놓을 수 있는지(화력은 보지 않음).</summary>
        public bool CellFits(ToolType type, int col, int row)
        {
            return CheckCell(type, col, row, null) == BuildResult.Ok;
        }

        public BuildResult CheckBuild(ToolType type, int col, int row)
        {
            if (State.finished || Run.failed) return BuildResult.NotNow;
            var r = CheckCell(type, col, row, null);
            if (r != BuildResult.Ok) return r;
            if (State.firepower < GameData.Tool(type).cost) return BuildResult.NoFirepower;
            return BuildResult.Ok;
        }

        public TowerState Build(ToolType type, int col, int row)
        {
            if (CheckBuild(type, col, row) != BuildResult.Ok) return null;
            var def = GameData.Tool(type);
            var n = State;
            n.firepower -= def.cost;
            var t = new TowerState
            {
                id = n.nextTowerId++,
                type = type,
                col = col,
                row = row,
                cooldown = 0.2f,
                invested = def.cost,
            };
            n.towers.Add(t);
            return t;
        }

        public BuildResult CheckMove(TowerState t, int col, int row)
        {
            if (t == null) return BuildResult.NotNow;
            if (!IsPrep) return BuildResult.NotNow;
            return CheckCell(t.type, col, row, t);
        }

        public bool Move(TowerState t, int col, int row)
        {
            if (CheckMove(t, col, row) != BuildResult.Ok) return false;
            t.col = col;
            t.row = row;
            return true;
        }

        /// <summary>준비 시간에는 전액, 웨이브 중에는 절반을 돌려준다.</summary>
        public int RefundValue(TowerState t)
        {
            if (t == null) return 0;
            return IsPrep ? t.invested : (int)(t.invested * Balance.RefundRateDuringWave);
        }

        public bool Remove(TowerState t)
        {
            if (t == null || State.finished || Run.failed) return false;
            if (!State.towers.Remove(t)) return false;
            State.firepower += RefundValue(t);
            return true;
        }

        public int UpgradeCost(TowerState t) { return Combat.UpgradeCost(t); }

        public bool CanUpgrade(TowerState t)
        {
            if (t == null || State.finished || Run.failed) return false;
            int cost = Combat.UpgradeCost(t);
            return cost > 0 && State.firepower >= cost;
        }

        /// <summary>밤 중 임시 강화. 화력을 쓰고, 밤이 끝나면 사라진다.</summary>
        public bool Upgrade(TowerState t)
        {
            if (!CanUpgrade(t)) return false;
            int cost = Combat.UpgradeCost(t);
            State.firepower -= cost;
            t.invested += cost;
            t.level++;
            return true;
        }

        /// <summary>약불: 피해를 절반으로 줄여 다음 기구에 막타를 넘긴다. 가마솥은 해당 없음.</summary>
        public bool ToggleLowHeat(TowerState t)
        {
            if (t == null || t.type == ToolType.Cauldron) return false;
            t.lowHeat = !t.lowHeat;
            return true;
        }

        public List<LayoutEntry> CaptureLayout()
        {
            var list = new List<LayoutEntry>();
            foreach (var t in State.towers)
                list.Add(new LayoutEntry { type = t.type, col = t.col, row = t.row, lowHeat = t.lowHeat });
            return list;
        }

        /// <summary>지난밤 배치를 화력이 허락하는 만큼 순서대로 되살린다.</summary>
        public int RestoreLayout(IList<LayoutEntry> layout)
        {
            if (!IsPrep || layout == null) return 0;
            int placed = 0;
            for (int i = 0; i < layout.Count; i++)
            {
                var e = layout[i];
                if (CheckBuild(e.type, e.col, e.row) != BuildResult.Ok) continue;
                var t = Build(e.type, e.col, e.row);
                if (t == null) continue;
                t.lowHeat = e.lowHeat && e.type != ToolType.Cauldron;
                placed++;
            }
            return placed;
        }

        // ───────────────────────── 진행 ─────────────────────────

        public bool StartWave()
        {
            var n = State;
            if (!IsPrep || !HasNextWave) return false;
            n.waveActive = true;
            n.spawnCursor = 0;
            var w = n.waves[n.waveIndex];
            n.spawnTimer = w.spawns.Count > 0 ? w.spawns[0].delay : 0f;
            Emit(new SimEvent { type = SimEventType.WaveStarted, amount = n.waveIndex });
            return true;
        }

        /// <summary>현재 포자에 걸린 공격 속도 배율(1 = 정상).</summary>
        public float TowerRate(TowerState t)
        {
            float rate = 1f;
            Vec2 tp = Map.CellCenter(t.col, t.row);
            var list = State.monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.alive) continue;
                var def = GameData.Monster(m.type);
                if (def.sporeRadius <= 0f) continue;
                if (Vec2.SqrDistance(tp, Map.PositionAt(m.distance)) <= def.sporeRadius * def.sporeRadius)
                    rate = Math.Min(rate, def.sporeRateMultiplier);
            }
            return rate;
        }

        public bool IsCharging(MonsterState m)
        {
            return Combat.IsCharging(Map, GameData.Monster(m.type), m.distance, m.slowTimer, m.straightTimer);
        }

        public void Step(float dt)
        {
            var n = State;
            if (!n.waveActive || n.finished || Run.failed || dt <= 0f) return;
            n.elapsed += dt;
            var wave = n.waves[n.waveIndex];

            // 1) 등장
            if (n.spawnCursor < wave.spawns.Count)
            {
                n.spawnTimer -= dt;
                while (n.spawnCursor < wave.spawns.Count && n.spawnTimer <= 0f)
                {
                    if (AliveCount >= Balance.MaxMonstersAlive)
                    {
                        n.spawnTimer = 0f;
                        break;
                    }
                    Spawn(wave.spawns[n.spawnCursor].type, 0f);
                    n.spawnCursor++;
                    if (n.spawnCursor < wave.spawns.Count) n.spawnTimer += wave.spawns[n.spawnCursor].delay;
                }
            }

            // 2) 기구 공격
            var towers = n.towers;
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                t.cooldown -= dt * TowerRate(t);
                if (t.cooldown > 0f) continue;
                if (Fire(t)) t.cooldown += Combat.Interval(Run, t);
                else t.cooldown = 0f;
            }

            // 3) 화상(지속 피해)
            var monsters = n.monsters;
            int count = monsters.Count;
            for (int i = 0; i < count; i++)
            {
                var m = monsters[i];
                if (!m.alive || m.burnTimer <= 0f) continue;
                float tick = Math.Min(dt, m.burnTimer);
                m.burnTimer -= dt;
                Damage(m, m.burnDps * tick, m.burnTowerId, CookMethod.Grill);
            }

            // 4) 이동과 누수
            count = monsters.Count;
            for (int i = 0; i < count; i++)
            {
                var m = monsters[i];
                if (!m.alive) continue;
                var def = GameData.Monster(m.type);
                Combat.Advance(Map, def, ref m.distance, ref m.slowTimer, ref m.slowFactor, ref m.straightTimer, ref m.segment, dt);
                if (m.distance >= Map.Length)
                {
                    Leak(m);
                    if (Run.failed) break;
                }
            }
            monsters.RemoveAll(GonePredicate);
            if (Run.failed) return;

            // 5) 웨이브 종료
            if (n.spawnCursor >= wave.spawns.Count && AliveCount == 0)
            {
                n.waveActive = false;
                n.waveIndex++;
                for (int i = 0; i < towers.Count; i++) towers[i].cooldown = 0f;
                Emit(new SimEvent { type = SimEventType.WaveCleared, amount = n.waveIndex });
                if (n.waveIndex >= n.waves.Count)
                {
                    n.finished = true;
                    Emit(new SimEvent { type = SimEventType.NightCleared });
                }
            }
        }

        static readonly Predicate<MonsterState> GonePredicate = IsGone;
        static bool IsGone(MonsterState m) { return !m.alive; }

        bool Fire(TowerState t)
        {
            var def = GameData.Tool(t.type);
            var monsters = State.monsters;
            int count = monsters.Count;
            switch (t.type)
            {
                case ToolType.Knife:
                {
                    MonsterState target = FirstInRange(t);
                    if (target == null) return false;
                    Emit(new SimEvent { type = SimEventType.Attack, towerId = t.id, toolType = t.type, monsterId = target.id, position = Map.PositionAt(target.distance) });
                    Damage(target, Combat.KnifeDamage(Run, t, GameData.Monster(target.type)), t.id, def.method);
                    return true;
                }
                case ToolType.Grill:
                {
                    bool any = false;
                    for (int i = 0; i < count; i++)
                    {
                        var m = monsters[i];
                        if (!m.alive || !Combat.InRange(Map, t, Map.PositionAt(m.distance))) continue;
                        m.burnDps = Combat.BurnDps(Run, t, GameData.Monster(m.type));
                        m.burnTimer = def.burnDuration;
                        m.burnTowerId = t.id;
                        any = true;
                    }
                    if (any) Emit(new SimEvent { type = SimEventType.Attack, towerId = t.id, toolType = t.type, position = Map.CellCenter(t.col, t.row) });
                    return any;
                }
                case ToolType.Freezer:
                {
                    bool any = false;
                    float slow = Combat.SlowDuration(Run, t);
                    float dmg = Combat.FreezerDamage(Run, t);
                    for (int i = 0; i < count; i++)
                    {
                        var m = monsters[i];
                        if (!m.alive || !Combat.InRange(Map, t, Map.PositionAt(m.distance))) continue;
                        if (!any) Emit(new SimEvent { type = SimEventType.Attack, towerId = t.id, toolType = t.type, position = Map.CellCenter(t.col, t.row) });
                        any = true;
                        m.slowTimer = Math.Max(m.slowTimer, slow);
                        m.slowFactor = def.slowFactor;
                        Damage(m, dmg, t.id, def.method);
                    }
                    return any;
                }
                case ToolType.Cauldron:
                {
                    MonsterState target = null;
                    float best = -1f;
                    float threshold = Combat.CauldronThreshold();
                    for (int i = 0; i < count; i++)
                    {
                        var m = monsters[i];
                        if (!m.alive) continue;
                        if (m.hp > m.maxHp * threshold + 0.0001f) continue;
                        if (!Map.CellContains(t.col, t.row, Map.PositionAt(m.distance))) continue;
                        if (m.distance > best)
                        {
                            best = m.distance;
                            target = m;
                        }
                    }
                    if (target == null) return false;
                    Emit(new SimEvent { type = SimEventType.Attack, towerId = t.id, toolType = t.type, monsterId = target.id, position = Map.PositionAt(target.distance) });
                    target.lastHitTowerId = t.id;
                    target.lastHitMethod = def.method;
                    Kill(target);
                    return true;
                }
            }
            return false;
        }

        /// <summary>사거리 안에서 창고에 가장 가까운(가장 많이 전진한) 몬스터.</summary>
        MonsterState FirstInRange(TowerState t)
        {
            MonsterState target = null;
            float best = -1f;
            var monsters = State.monsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.alive || m.distance <= best) continue;
                if (!Combat.InRange(Map, t, Map.PositionAt(m.distance))) continue;
                best = m.distance;
                target = m;
            }
            return target;
        }

        void Damage(MonsterState m, float amount, int towerId, CookMethod method)
        {
            if (!m.alive || amount <= 0f) return;
            m.hp -= amount;
            m.lastHitTowerId = towerId;
            m.lastHitMethod = method;
            if (m.hp <= 0.0001f) Kill(m);
        }

        void Kill(MonsterState m)
        {
            if (!m.alive) return;
            m.alive = false;
            m.hp = 0f;
            var n = State;
            var def = GameData.Monster(m.type);
            n.kills++;
            Run.stats.kills++;
            n.firepower += def.reward;

            var tower = TowerById(m.lastHitTowerId);
            if (tower != null) tower.kills++;

            string ingredient = GameData.IngredientFor(def, m.lastHitMethod);
            if (ingredient != null)
            {
                Run.storage.Add(new StoredIngredient { id = ingredient, night = n.index });
                n.haul.Add(ingredient);
            }

            Vec2 pos = Map.PositionAt(m.distance);
            Emit(new SimEvent
            {
                type = SimEventType.Killed,
                monsterId = m.id,
                monsterType = m.type,
                towerId = m.lastHitTowerId,
                toolType = tower != null ? tower.type : GameData.ToolForMethod(m.lastHitMethod),
                method = m.lastHitMethod,
                ingredientId = ingredient,
                position = pos,
                amount = def.reward,
            });

            if (def.splitCount > 0)
            {
                for (int i = 0; i < def.splitCount; i++)
                {
                    float offset = def.splitCount > 1 ? -0.15f + 0.3f * i / (def.splitCount - 1) : 0f;
                    Spawn(def.splitInto, Math.Max(0f, m.distance + offset));
                }
                Emit(new SimEvent { type = SimEventType.Split, monsterId = m.id, monsterType = m.type, position = pos });
            }
        }

        void Leak(MonsterState m)
        {
            m.alive = false;
            var n = State;
            n.leaks++;
            Run.stats.leaks++;
            Run.durability = Math.Max(0, Run.durability - 1);

            // 창고 재료를 비싼 것부터 하나 먹는다.
            int idx = -1;
            int bestValue = -1;
            var storage = Run.storage;
            for (int i = 0; i < storage.Count; i++)
            {
                int v = GameData.Ingredient(storage[i].id).value;
                if (v > bestValue)
                {
                    bestValue = v;
                    idx = i;
                }
            }
            string eaten = null;
            if (idx >= 0)
            {
                eaten = storage[idx].id;
                storage.RemoveAt(idx);
                n.eaten.Add(eaten);
            }

            Emit(new SimEvent
            {
                type = SimEventType.Leaked,
                monsterId = m.id,
                monsterType = m.type,
                eatenId = eaten,
                position = Map.PositionAt(Map.Length),
            });

            if (Run.durability <= 0)
            {
                Run.failed = true;
                n.waveActive = false;
                Emit(new SimEvent { type = SimEventType.SeasonFailed });
            }
        }

        void Spawn(MonsterType type, float distance)
        {
            var n = State;
            var def = GameData.Monster(type);
            float hp = Combat.MonsterMaxHp(Run, def, n.index);
            int id = n.nextMonsterId++;
            var m = new MonsterState
            {
                id = id,
                type = type,
                alive = true,
                hp = hp,
                maxHp = hp,
                distance = distance,
                lateral = ((id * 37) % 9 - 4) * 0.04f,
                slowFactor = 1f,
                burnTowerId = -1,
                lastHitTowerId = -1,
                segment = Map.SegmentAt(distance),
            };
            n.monsters.Add(m);
            Emit(new SimEvent { type = SimEventType.Spawned, monsterId = id, monsterType = type, position = Map.PositionAt(distance) });
        }

        void Emit(SimEvent e) { Events.Add(e); }
    }
}
