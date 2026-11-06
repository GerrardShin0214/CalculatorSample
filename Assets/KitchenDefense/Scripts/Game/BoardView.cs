using System.Collections.Generic;
using KitchenDefense.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 주방 격자와 그 위의 기구·몬스터를 그린다. 상태는 전부 Core(NightSimulation)에 있고,
    /// 여기서는 매 프레임 그 상태를 그대로 옮겨 그리기만 한다.
    /// </summary>
    public sealed class BoardView
    {
        public readonly Transform Root;
        readonly KitchenMap map;
        readonly Transform unitRoot;
        readonly SpriteRenderer[] highlights;
        readonly SpriteRenderer selection;
        readonly SpriteRenderer rangeFill;
        readonly SpriteRenderer rangeRing;
        readonly SpriteRenderer storageFlash;
        float storageFlashTime;

        readonly Dictionary<int, TowerView> towers = new Dictionary<int, TowerView>();
        readonly Dictionary<int, MonsterView> monsters = new Dictionary<int, MonsterView>();
        readonly Stack<MonsterView> monsterPool = new Stack<MonsterView>();
        readonly List<int> scratch = new List<int>();
        readonly HashSet<int> seen = new HashSet<int>();

        public BoardView(KitchenMap map)
        {
            this.map = map;
            Root = new GameObject("Board").transform;

            var frame = Sprite("Frame", Root, SpriteFactory.Rounded, Theme.BoardFrame, -10);
            frame.transform.localScale = new Vector3(map.Cols + 0.36f, map.Rows + 0.36f, 1f);

            highlights = new SpriteRenderer[map.Cols * map.Rows];
            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    Color color;
                    if (c == map.Entrance.col && r == map.Entrance.row) color = Theme.Entrance;
                    else if (c == map.Storage.col && r == map.Storage.row) color = Theme.Storage;
                    else if (map.IsPath(c, r)) color = Theme.Path;
                    else color = (c + r) % 2 == 0 ? Theme.Floor : Theme.FloorAlt;
                    var cell = Sprite("Cell " + c + "," + r, Root, SpriteFactory.Rounded, color, -9);
                    cell.transform.localPosition = CellPosition(c, r);
                    cell.transform.localScale = Vector3.one * 0.93f;

                    var hl = Sprite("Highlight", cell.transform, SpriteFactory.Rounded, Theme.Valid, -7);
                    hl.transform.localScale = Vector3.one * 0.86f;
                    hl.gameObject.SetActive(false);
                    highlights[r * map.Cols + c] = hl;
                }
            }

            for (float d = 0.45f; d < map.Length - 0.4f; d += 0.5f)
            {
                var dot = Sprite("PathDot", Root, SpriteFactory.Circle, Theme.PathDot, -8);
                var p = map.PositionAt(d);
                dot.transform.localPosition = new Vector3(p.x, p.y, 0f);
                dot.transform.localScale = Vector3.one * 0.09f;
            }

            WorldText.Create(Root, "EntranceLabel", "뒷문", 0.26f, Theme.HudText, -6).transform.localPosition = CellPosition(map.Entrance.col, map.Entrance.row);
            WorldText.Create(Root, "StorageLabel", "창고", 0.26f, Theme.HudText, -6).transform.localPosition = CellPosition(map.Storage.col, map.Storage.row);

            storageFlash = Sprite("StorageFlash", Root, SpriteFactory.Rounded, new Color(1f, 0.2f, 0.15f, 0f), -5);
            storageFlash.transform.localPosition = CellPosition(map.Storage.col, map.Storage.row);
            storageFlash.transform.localScale = Vector3.one * 0.93f;

            selection = Sprite("Selection", Root, SpriteFactory.RoundedOutline, Theme.Selection, 30);
            selection.transform.localScale = Vector3.one * 1.02f;
            selection.gameObject.SetActive(false);

            rangeFill = Sprite("RangeFill", Root, SpriteFactory.Circle, new Color(1f, 1f, 1f, 0.08f), 2);
            rangeRing = Sprite("RangeRing", Root, SpriteFactory.Ring, new Color(1f, 1f, 1f, 0.45f), 3);
            HideRange();

            unitRoot = new GameObject("Units").transform;
            unitRoot.SetParent(Root, false);
        }

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public Vector3 CellPosition(int col, int row)
        {
            var p = map.CellCenter(col, row);
            return new Vector3(p.x, p.y, 0f);
        }

        public static Vector3 ToWorld(Vec2 p) { return new Vector3(p.x, p.y, 0f); }

        public void SetVisible(bool visible)
        {
            if (Root.gameObject.activeSelf != visible) Root.gameObject.SetActive(visible);
        }

        // ───────────── 표시 보조 ─────────────

        public void SetHighlights(System.Func<int, int, bool> valid)
        {
            for (int r = 0; r < map.Rows; r++)
                for (int c = 0; c < map.Cols; c++)
                {
                    bool on = valid != null && valid(c, r);
                    var hl = highlights[r * map.Cols + c];
                    if (hl.gameObject.activeSelf != on) hl.gameObject.SetActive(on);
                }
        }

        public void SetSelection(bool visible, int col, int row)
        {
            if (selection.gameObject.activeSelf != visible) selection.gameObject.SetActive(visible);
            if (visible) selection.transform.localPosition = CellPosition(col, row);
        }

        public void ShowRange(Vector3 center, float radius, Color color)
        {
            rangeFill.gameObject.SetActive(true);
            rangeRing.gameObject.SetActive(true);
            rangeFill.transform.localPosition = center;
            rangeRing.transform.localPosition = center;
            rangeFill.transform.localScale = Vector3.one * radius * 2f;
            rangeRing.transform.localScale = Vector3.one * radius * 2f;
            rangeFill.color = new Color(color.r, color.g, color.b, 0.1f);
            rangeRing.color = new Color(color.r, color.g, color.b, 0.6f);
        }

        public void HideRange()
        {
            rangeFill.gameObject.SetActive(false);
            rangeRing.gameObject.SetActive(false);
        }

        public void FlashStorage() { storageFlashTime = 0.6f; }

        public void PunchTower(int towerId)
        {
            TowerView v;
            if (towers.TryGetValue(towerId, out v)) v.Punch();
        }

        public Vector3 TowerPosition(int towerId, Vector3 fallback)
        {
            TowerView v;
            return towers.TryGetValue(towerId, out v) ? v.Go.transform.localPosition : fallback;
        }

        /// <summary>새 밤이 시작되면 id가 다시 1부터 시작하므로 이전 밤의 그림을 모두 지운다.</summary>
        public void ClearUnits()
        {
            foreach (var v in towers.Values) Object.Destroy(v.Go);
            towers.Clear();
            foreach (var v in monsters.Values) Object.Destroy(v.Go);
            monsters.Clear();
            while (monsterPool.Count > 0) Object.Destroy(monsterPool.Pop().Go);
            SetHighlights(null);
            SetSelection(false, 0, 0);
            HideRange();
        }

        // ───────────── 동기화 ─────────────

        public struct MonsterHint
        {
            public bool hasPrediction;
            public KillPrediction prediction;
            public bool isTarget;
            public bool predictedNeeded;
        }

        public void Sync(NightSimulation sim, System.Func<MonsterState, MonsterHint> hint, int selectedTowerId, float time, float dt)
        {
            if (storageFlashTime > 0f)
            {
                storageFlashTime -= dt;
                storageFlash.color = new Color(1f, 0.25f, 0.2f, Mathf.Clamp01(storageFlashTime / 0.6f) * 0.7f);
            }

            // 기구
            seen.Clear();
            var towerStates = sim.State.towers;
            for (int i = 0; i < towerStates.Count; i++)
            {
                var t = towerStates[i];
                seen.Add(t.id);
                TowerView v;
                if (towers.TryGetValue(t.id, out v) && v.Type != t.type)
                {
                    Object.Destroy(v.Go);
                    towers.Remove(t.id);
                    v = null;
                }
                if (v == null)
                {
                    v = new TowerView(unitRoot, t.type);
                    towers.Add(t.id, v);
                }
                var tp = CellPosition(t.col, t.row);
                Vector3 target;
                bool hasTarget = NearestIntruder(sim, tp, out target);
                v.Sync(sim, t, tp, t.id == selectedTowerId, hasTarget, target, time, dt);
            }
            scratch.Clear();
            foreach (var id in towers.Keys) if (!seen.Contains(id)) scratch.Add(id);
            foreach (var id in scratch)
            {
                Object.Destroy(towers[id].Go);
                towers.Remove(id);
            }

            // 몬스터
            seen.Clear();
            var list = sim.State.monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.alive) continue;
                seen.Add(m.id);
                MonsterView v;
                if (monsters.TryGetValue(m.id, out v) && v.Type != m.type)
                {
                    Release(v);
                    monsters.Remove(m.id);
                    v = null;
                }
                if (v == null)
                {
                    v = monsterPool.Count > 0 ? monsterPool.Pop() : new MonsterView(unitRoot);
                    v.Setup(m.type);
                    monsters.Add(m.id, v);
                }
                v.Sync(map, m, sim.IsCharging(m), hint != null ? hint(m) : new MonsterHint(), time);
            }
            scratch.Clear();
            foreach (var id in monsters.Keys) if (!seen.Contains(id)) scratch.Add(id);
            foreach (var id in scratch)
            {
                Release(monsters[id]);
                monsters.Remove(id);
            }
        }

        bool NearestIntruder(NightSimulation sim, Vector3 from, out Vector3 nearest)
        {
            nearest = from;
            float best = float.MaxValue;
            var list = sim.State.monsters;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].alive) continue;
                var p = map.PositionAt(list[i].distance);
                float dx = p.x - from.x, dy = p.y - from.y;
                float d2 = dx * dx + dy * dy;
                if (d2 < best)
                {
                    best = d2;
                    nearest = new Vector3(p.x, p.y, 0f);
                }
            }
            return best < float.MaxValue;
        }

        void Release(MonsterView v)
        {
            v.Go.SetActive(false);
            monsterPool.Push(v);
        }
    }

    /// <summary>주방 식구(의인화된 조리기구) 하나의 그림. 눈이 달려 있어서 가장 가까운 도둑을 쳐다본다.</summary>
    public sealed class TowerView
    {
        public readonly GameObject Go;
        public readonly ToolType Type;
        readonly SpriteRenderer shadow;
        readonly SpriteRenderer body;
        readonly SpriteRenderer inner;
        readonly SpriteRenderer eyeL, eyeR, pupilL, pupilR;
        readonly SpriteRenderer[] pips = new SpriteRenderer[2];
        readonly SpriteRenderer lowBadge;
        readonly Vector3 eyeOffsetL, eyeOffsetR;
        float punch;

        public TowerView(Transform parent, ToolType type)
        {
            Type = type;
            Go = new GameObject("Tool " + type);
            Go.transform.SetParent(parent, false);
            var t = Go.transform;
            Color color = Theme.ToolColor(type);
            bool pot = type == ToolType.Cauldron;

            shadow = BoardView.Sprite("Shadow", t, pot ? SpriteFactory.Circle : SpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.35f), 4);
            shadow.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            shadow.transform.localScale = Vector3.one * (pot ? 0.8f : 0.86f);

            body = BoardView.Sprite("Body", t, pot ? SpriteFactory.Circle : SpriteFactory.Rounded, pot ? Theme.Hex(0x37302C) : color, 5);
            body.transform.localScale = Vector3.one * (pot ? 0.78f : 0.82f);

            inner = BoardView.Sprite("Inner", t, pot ? SpriteFactory.Circle : SpriteFactory.Rounded, pot ? color : Color.Lerp(color, Color.white, 0.35f), 6);
            inner.transform.localScale = Vector3.one * (pot ? 0.56f : 0.6f);

            var glyph = WorldText.Create(t, "Glyph", GameData.Tool(type).glyph, 0.3f, pot ? Color.white : Theme.Ink, 7);
            glyph.transform.localPosition = new Vector3(0f, -0.13f, 0f);

            // 의인화: 동그란 눈 두 개
            float eyeY = pot ? 0.1f : 0.12f;
            eyeOffsetL = new Vector3(-0.13f, eyeY, 0f);
            eyeOffsetR = new Vector3(0.13f, eyeY, 0f);
            eyeL = BoardView.Sprite("EyeL", t, SpriteFactory.Circle, Color.white, 8);
            eyeR = BoardView.Sprite("EyeR", t, SpriteFactory.Circle, Color.white, 8);
            pupilL = BoardView.Sprite("PupilL", t, SpriteFactory.Circle, Theme.Hex(0x1B1411), 9);
            pupilR = BoardView.Sprite("PupilR", t, SpriteFactory.Circle, Theme.Hex(0x1B1411), 9);
            eyeL.transform.localPosition = eyeOffsetL;
            eyeR.transform.localPosition = eyeOffsetR;
            pupilL.transform.localScale = Vector3.one * 0.075f;
            pupilR.transform.localScale = Vector3.one * 0.075f;

            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = BoardView.Sprite("Pip", t, SpriteFactory.Circle, Theme.Star, 8);
                pips[i].transform.localPosition = new Vector3(-0.1f + 0.2f * i, -0.33f, 0f);
                pips[i].transform.localScale = Vector3.one * 0.12f;
            }

            lowBadge = BoardView.Sprite("LowHeat", t, SpriteFactory.Circle, Theme.Blue, 10);
            lowBadge.transform.localPosition = new Vector3(0.3f, 0.3f, 0f);
            lowBadge.transform.localScale = Vector3.one * 0.3f;
            WorldText.Create(lowBadge.transform, "Text", "약", 0.62f, Color.white, 11);
        }

        public void Punch() { punch = 1f; }

        public void Sync(NightSimulation sim, TowerState t, Vector3 position, bool selected, bool hasTarget, Vector3 target, float time, float dt)
        {
            if (!Go.activeSelf) Go.SetActive(true);
            Go.transform.localPosition = position;
            punch = Mathf.Max(0f, punch - dt * 6f);
            float s = 1f + 0.14f * punch + (selected ? 0.04f * Mathf.Sin(time * 6f) : 0f);
            Go.transform.localScale = new Vector3(s, s, 1f);

            // 가끔 깜빡이고, 가장 가까운 도둑 쪽으로 눈동자를 굴린다.
            float blink = Mathf.Repeat(time + t.id * 0.77f, 3.6f) < 0.12f ? 0.15f : 1f;
            eyeL.transform.localScale = new Vector3(0.17f, 0.17f * blink, 1f);
            eyeR.transform.localScale = new Vector3(0.17f, 0.17f * blink, 1f);
            Vector3 look = Vector3.zero;
            if (hasTarget)
            {
                var d = target - position;
                float len = Mathf.Sqrt(d.x * d.x + d.y * d.y);
                if (len > 0.001f) look = new Vector3(d.x / len, d.y / len, 0f) * 0.04f;
            }
            pupilL.transform.localPosition = eyeOffsetL + look;
            pupilR.transform.localPosition = eyeOffsetR + look;
            bool open = blink > 0.5f;
            if (pupilL.gameObject.activeSelf != open)
            {
                pupilL.gameObject.SetActive(open);
                pupilR.gameObject.SetActive(open);
            }

            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < t.level;
                if (pips[i].gameObject.activeSelf != on) pips[i].gameObject.SetActive(on);
            }
            if (lowBadge.gameObject.activeSelf != t.lowHeat) lowBadge.gameObject.SetActive(t.lowHeat);

            if (Type == ToolType.Cauldron)
            {
                // 재사용 대기 중이면 솥이 식어 보인다.
                bool ready = t.cooldown <= 0f;
                inner.color = ready ? Theme.ToolColor(Type) : Theme.Hex(0x6E7B63);
                float bubble = ready ? 0.03f * Mathf.Sin(time * 8f + t.id) : 0f;
                inner.transform.localScale = Vector3.one * (0.56f + bubble);
            }
            else
            {
                // 파리 떼의 윙윙 소리에 시달리면 보랏빛으로 질린다.
                float rate = sim.TowerRate(t);
                Color baseColor = Theme.ToolColor(Type);
                body.color = rate < 1f ? Color.Lerp(baseColor, Theme.Hex(0xB39DDB), 0.45f) : baseColor;
            }
        }
    }

    /// <summary>도둑 동물 하나의 그림: 몸통 + 귀·주둥이 등 + 물고 온 음식 + 체력바 + 막타 예상 아이콘.</summary>
    public sealed class MonsterView
    {
        public readonly GameObject Go;
        public MonsterType Type { get; private set; }

        readonly SortingGroup group;
        readonly Transform bodyRoot;
        readonly SpriteRenderer outline;
        readonly SpriteRenderer body;
        readonly SpriteRenderer[] extras = new SpriteRenderer[8];
        readonly SpriteRenderer eyeL, eyeR, pupilL, pupilR;
        readonly SpriteRenderer loot;
        readonly SpriteRenderer burn;
        readonly SpriteRenderer status;
        readonly SpriteRenderer hpBack, hpFill, hpTick;
        readonly SpriteRenderer icon, iconRing;
        readonly TextMesh iconText;
        float radius;
        Color baseColor;
        bool buzzing;

        const float HpWidth = 0.6f;
        const float HpHeight = 0.09f;

        public MonsterView(Transform parent)
        {
            Go = new GameObject("Animal");
            Go.transform.SetParent(parent, false);
            group = Go.AddComponent<SortingGroup>();
            group.sortingOrder = 10;

            bodyRoot = new GameObject("BodyRoot").transform;
            bodyRoot.SetParent(Go.transform, false);
            outline = BoardView.Sprite("Outline", bodyRoot, SpriteFactory.Circle, Color.black, 1);
            body = BoardView.Sprite("Body", bodyRoot, SpriteFactory.Circle, Color.white, 2);
            for (int i = 0; i < extras.Length; i++) extras[i] = BoardView.Sprite("Part", bodyRoot, SpriteFactory.Circle, Color.white, 3);
            eyeL = BoardView.Sprite("EyeL", bodyRoot, SpriteFactory.Circle, Color.white, 6);
            eyeR = BoardView.Sprite("EyeR", bodyRoot, SpriteFactory.Circle, Color.white, 6);
            pupilL = BoardView.Sprite("PupilL", bodyRoot, SpriteFactory.Circle, Theme.Hex(0x1B1411), 7);
            pupilR = BoardView.Sprite("PupilR", bodyRoot, SpriteFactory.Circle, Theme.Hex(0x1B1411), 7);
            loot = BoardView.Sprite("Loot", bodyRoot, SpriteFactory.Circle, Color.white, 8);
            burn = BoardView.Sprite("Burn", bodyRoot, SpriteFactory.Circle, Theme.Hex(0xFF7043, 0.85f), 9);
            // pixelfx 상태 효과(화상/빙결) 겹쳐 그리기
            status = BoardView.Sprite("StatusFx", Go.transform, null, Color.white, 10);
            status.gameObject.SetActive(false);

            hpBack = BoardView.Sprite("HpBack", Go.transform, SpriteFactory.Square, new Color(0f, 0f, 0f, 0.7f), 12);
            hpFill = BoardView.Sprite("HpFill", Go.transform, SpriteFactory.Square, Theme.Good, 13);
            hpTick = BoardView.Sprite("HpTick", Go.transform, SpriteFactory.Square, new Color(0f, 0f, 0f, 0.8f), 14);
            iconRing = BoardView.Sprite("IconRing", Go.transform, SpriteFactory.Circle, Color.white, 15);
            icon = BoardView.Sprite("Icon", Go.transform, SpriteFactory.Circle, Color.white, 16);
            iconText = WorldText.Create(icon.transform, "IconText", "", 0.7f, Theme.Ink, 17);
        }

        public void Setup(MonsterType type)
        {
            Type = type;
            var def = GameData.Monster(type);
            radius = def.radius;
            baseColor = Theme.MonsterColor(type);
            Go.name = "Animal " + type;
            Go.SetActive(true);
            buzzing = type == MonsterType.FlySwarm;

            float r = radius;
            float d = r * 2f;
            Color dark = Color.Lerp(baseColor, Color.black, 0.45f);
            Color pink = Theme.Hex(0xF4A9B8);
            outline.transform.localScale = Vector3.one * d * 1.12f;
            outline.color = Color.Lerp(baseColor, Color.black, 0.6f);
            body.transform.localScale = Vector3.one * d;
            body.color = baseColor;
            for (int i = 0; i < extras.Length; i++) extras[i].gameObject.SetActive(false);

            float eyeSize = d * 0.28f;
            Vector3 eyePosL = new Vector3(-r * 0.32f, r * 0.08f, 0f);
            Vector3 eyePosR = new Vector3(r * 0.32f, r * 0.08f, 0f);
            Color eyeWhite = Color.white;
            Sprite lootSprite = null;
            Vector2 lootSize = Vector2.zero;
            Color lootColor = Color.white;

            switch (type)
            {
                case MonsterType.MouseGang:
                case MonsterType.Mouse:
                {
                    bool gang = type == MonsterType.MouseGang;
                    Part(0, SpriteFactory.Circle, new Vector2(-r * 0.62f, r * 0.72f), new Vector2(d * 0.44f, d * 0.44f), baseColor, 0);
                    Part(1, SpriteFactory.Circle, new Vector2(r * 0.62f, r * 0.72f), new Vector2(d * 0.44f, d * 0.44f), baseColor, 0);
                    Part(2, SpriteFactory.Circle, new Vector2(-r * 0.62f, r * 0.72f), new Vector2(d * 0.24f, d * 0.24f), pink, 3);
                    Part(3, SpriteFactory.Circle, new Vector2(r * 0.62f, r * 0.72f), new Vector2(d * 0.24f, d * 0.24f), pink, 3);
                    Part(4, SpriteFactory.Circle, new Vector2(0f, -r * 0.36f), new Vector2(d * 0.16f, d * 0.14f), pink, 4);
                    if (gang)
                    {
                        // 떼: 뒤에 따라오는 생쥐 두 마리
                        Part(5, SpriteFactory.Circle, new Vector2(-r * 0.95f, -r * 0.55f), new Vector2(d * 0.5f, d * 0.5f), Color.Lerp(baseColor, Color.white, 0.2f), 0);
                        Part(6, SpriteFactory.Circle, new Vector2(r * 0.95f, -r * 0.55f), new Vector2(d * 0.5f, d * 0.5f), Color.Lerp(baseColor, Color.white, 0.2f), 0);
                        lootSprite = SpriteFactory.Rounded;
                        lootSize = new Vector2(0.24f, 0.19f);
                        lootColor = Theme.Hex(0xFFD54F);
                    }
                    break;
                }
                case MonsterType.FlySwarm:
                {
                    // 파리 세 마리 + 반투명 날개
                    body.transform.localScale = Vector3.one * d * 0.7f;
                    outline.transform.localScale = Vector3.one * d * 0.78f;
                    Part(0, SpriteFactory.Circle, new Vector2(-r * 0.75f, r * 0.35f), new Vector2(d * 0.45f, d * 0.45f), baseColor, 1);
                    Part(1, SpriteFactory.Circle, new Vector2(r * 0.7f, -r * 0.4f), new Vector2(d * 0.45f, d * 0.45f), baseColor, 1);
                    Color wing = new Color(0.88f, 0.95f, 1f, 0.65f);
                    Part(2, SpriteFactory.Circle, new Vector2(-r * 0.3f, r * 0.55f), new Vector2(d * 0.36f, d * 0.26f), wing, 5);
                    Part(3, SpriteFactory.Circle, new Vector2(r * 0.3f, r * 0.55f), new Vector2(d * 0.36f, d * 0.26f), wing, 5);
                    Part(4, SpriteFactory.Circle, new Vector2(-r * 0.75f, r * 0.7f), new Vector2(d * 0.26f, d * 0.2f), wing, 5);
                    Part(5, SpriteFactory.Circle, new Vector2(r * 0.7f, -r * 0.05f), new Vector2(d * 0.26f, d * 0.2f), wing, 5);
                    eyeWhite = Theme.Hex(0xE53935);
                    eyeSize = d * 0.2f;
                    eyePosL = new Vector3(-r * 0.15f, r * 0.05f, 0f);
                    eyePosR = new Vector3(r * 0.15f, r * 0.05f, 0f);
                    break;
                }
                case MonsterType.Boar:
                {
                    Part(0, SpriteFactory.Circle, new Vector2(-r * 0.62f, r * 0.72f), new Vector2(d * 0.3f, d * 0.3f), dark, 0);
                    Part(1, SpriteFactory.Circle, new Vector2(r * 0.62f, r * 0.72f), new Vector2(d * 0.3f, d * 0.3f), dark, 0);
                    Part(2, SpriteFactory.Circle, new Vector2(0f, -r * 0.34f), new Vector2(d * 0.42f, d * 0.32f), Theme.Hex(0xE8A598), 3);
                    Part(3, SpriteFactory.Circle, new Vector2(-r * 0.09f, -r * 0.34f), new Vector2(d * 0.07f, d * 0.07f), dark, 4);
                    Part(4, SpriteFactory.Circle, new Vector2(r * 0.09f, -r * 0.34f), new Vector2(d * 0.07f, d * 0.07f), dark, 4);
                    Part(5, SpriteFactory.Circle, new Vector2(-r * 0.36f, -r * 0.58f), new Vector2(d * 0.1f, d * 0.14f), Theme.Hex(0xFFF8E1), 4);
                    Part(6, SpriteFactory.Circle, new Vector2(r * 0.36f, -r * 0.58f), new Vector2(d * 0.1f, d * 0.14f), Theme.Hex(0xFFF8E1), 4);
                    lootSprite = SpriteFactory.Circle;
                    lootSize = new Vector2(0.34f, 0.2f);
                    lootColor = Theme.Hex(0x9C5FB5);
                    break;
                }
                case MonsterType.Raccoon:
                {
                    Part(0, SpriteFactory.Circle, new Vector2(-r * 0.62f, r * 0.74f), new Vector2(d * 0.32f, d * 0.32f), dark, 0);
                    Part(1, SpriteFactory.Circle, new Vector2(r * 0.62f, r * 0.74f), new Vector2(d * 0.32f, d * 0.32f), dark, 0);
                    // 너구리 가면 무늬
                    Part(2, SpriteFactory.Rounded, new Vector2(0f, r * 0.1f), new Vector2(d * 0.92f, d * 0.3f), Theme.Hex(0x3E3A37), 3);
                    Part(3, SpriteFactory.Circle, new Vector2(0f, -r * 0.36f), new Vector2(d * 0.4f, d * 0.3f), Theme.Hex(0xEDE7E1), 4);
                    Part(4, SpriteFactory.Circle, new Vector2(0f, -r * 0.27f), new Vector2(d * 0.12f, d * 0.09f), Theme.Hex(0x2B2522), 5);
                    lootSprite = SpriteFactory.Circle;
                    lootSize = new Vector2(0.42f, 0.17f);
                    lootColor = Theme.Hex(0x90A4AE);
                    break;
                }
            }

            eyeL.transform.localPosition = eyePosL;
            eyeR.transform.localPosition = eyePosR;
            eyeL.transform.localScale = Vector3.one * eyeSize;
            eyeR.transform.localScale = Vector3.one * eyeSize;
            eyeL.color = eyeWhite;
            eyeR.color = eyeWhite;
            pupilL.transform.localScale = Vector3.one * eyeSize * 0.5f;
            pupilR.transform.localScale = Vector3.one * eyeSize * 0.5f;

            bool hasLoot = lootSprite != null;
            loot.gameObject.SetActive(hasLoot);
            if (hasLoot)
            {
                loot.sprite = lootSprite;
                loot.color = lootColor;
                loot.transform.localScale = new Vector3(lootSize.x, lootSize.y, 1f);
            }

            burn.transform.localPosition = new Vector3(r * 0.55f, r * 0.75f, 0f);
            burn.transform.localScale = Vector3.one * d * 0.28f;

            float barY = r + 0.15f;
            hpBack.transform.localPosition = new Vector3(0f, barY, 0f);
            hpBack.transform.localScale = new Vector3(HpWidth + 0.04f, HpHeight + 0.04f, 1f);
            hpTick.transform.localPosition = new Vector3(-HpWidth * 0.5f + HpWidth * Combat.CauldronThreshold(), barY, 0f);
            hpTick.transform.localScale = new Vector3(0.025f, HpHeight + 0.04f, 1f);
        }

        void Part(int i, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order)
        {
            var e = extras[i];
            e.gameObject.SetActive(true);
            e.sprite = sprite;
            e.sortingOrder = order;
            e.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            e.transform.localScale = new Vector3(size.x, size.y, 1f);
            e.color = color;
        }

        public void Sync(KitchenMap map, MonsterState m, bool charging, BoardView.MonsterHint hint, float time)
        {
            if (!Go.activeSelf) Go.SetActive(true);
            Vec2 p = map.PositionAt(m.distance);
            Vec2 dir = map.DirectionAt(m.distance);
            Vec2 side = new Vec2(-dir.y, dir.x);
            Go.transform.localPosition = new Vector3(p.x + side.x * m.lateral, p.y + side.y * m.lateral, -0.001f * (m.id % 97));

            if (buzzing)
            {
                // 파리 떼는 제자리에서 윙윙 떨린다.
                float jx = Mathf.Sin(time * 31f + m.id) * 0.035f;
                float jy = Mathf.Sin(time * 27f + m.id * 1.7f) * 0.035f;
                bodyRoot.localPosition = new Vector3(jx, jy, 0f);
                bodyRoot.localScale = Vector3.one;
            }
            else
            {
                // 통통 튀는 걸음
                float bob = Mathf.Abs(Mathf.Sin(time * (charging ? 16f : 9f) + m.id)) * 0.05f;
                bodyRoot.localPosition = new Vector3(0f, bob, 0f);
                float squash = 1f + 0.05f * Mathf.Sin(time * 7f + m.id);
                bodyRoot.localScale = new Vector3(squash, 2f - squash, 1f);
            }

            Color c = baseColor;
            if (m.slowTimer > 0f) c = Color.Lerp(c, Theme.Hex(0xB3E5FC), 0.5f);
            if (charging) c = Color.Lerp(c, Theme.Hex(0xFF5252), 0.25f + 0.15f * Mathf.Sin(time * 20f));
            body.color = c;

            // 물고 온 음식은 가는 방향 앞쪽에
            if (loot.gameObject.activeSelf)
            {
                loot.transform.localPosition = new Vector3(dir.x, dir.y, 0f) * radius * 0.95f + new Vector3(0f, -radius * 0.2f, 0f);
                loot.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }

            // 화상(불판)·감속(냉동고)은 pixelfx 시트가 있으면 몸 위에 불꽃/서리 애니메이션으로, 없으면 작은 불씨 점으로.
            PixelFxClip overlay = null;
            if (m.burnTimer > 0f) overlay = PixelFxLibrary.Get(PixelFxLibrary.Burning);
            else if (m.slowTimer > 0f) overlay = PixelFxLibrary.Get(PixelFxLibrary.Frozen);
            bool showOverlay = overlay != null;
            if (status.gameObject.activeSelf != showOverlay) status.gameObject.SetActive(showOverlay);
            if (showOverlay)
            {
                status.sprite = overlay.FrameAt(time + m.id * 0.37f);
                float os = radius / 0.33f * 0.7f;
                status.transform.localScale = new Vector3(os, os, 1f);
                status.transform.localPosition = new Vector3(0f, -radius * 0.9f, 0f);
            }
            bool burning = m.burnTimer > 0f && !showOverlay;
            if (burn.gameObject.activeSelf != burning) burn.gameObject.SetActive(burning);
            if (burning) burn.transform.localScale = Vector3.one * radius * 2f * (0.24f + 0.06f * Mathf.Sin(time * 25f));

            Vector3 look = new Vector3(dir.x, dir.y, 0f) * radius * 0.08f;
            pupilL.transform.localPosition = eyeL.transform.localPosition + look;
            pupilR.transform.localPosition = eyeR.transform.localPosition + look;

            float ratio = m.maxHp > 0f ? Mathf.Clamp01(m.hp / m.maxHp) : 0f;
            float barY = radius + 0.15f;
            hpFill.transform.localScale = new Vector3(HpWidth * ratio, HpHeight, 1f);
            hpFill.transform.localPosition = new Vector3(-HpWidth * 0.5f + HpWidth * ratio * 0.5f, barY, 0f);
            // 25% 이하(빨강) = 솥할매가 확정으로 삶을 수 있는 구간
            hpFill.color = ratio <= Combat.CauldronThreshold() + 0.0001f ? Theme.Bad : (ratio <= 0.5f ? Theme.Gold : Theme.Good);

            SyncIcon(hint, barY);
        }

        void SyncIcon(BoardView.MonsterHint hint, float barY)
        {
            bool show = hint.hasPrediction && hint.prediction.valid;
            if (icon.gameObject.activeSelf != show) icon.gameObject.SetActive(show);
            bool ring = show && hint.isTarget;
            if (iconRing.gameObject.activeSelf != ring) iconRing.gameObject.SetActive(ring);
            if (!show) return;

            var pred = hint.prediction;
            // 기획서: 처치 예상 아이콘은 예약 대상에만 크게 띄운다.
            float size = hint.isTarget ? 0.42f : 0.26f;
            var pos = new Vector3(HpWidth * 0.5f + size * 0.5f + 0.04f, barY, 0f);
            icon.transform.localPosition = pos;
            icon.transform.localScale = Vector3.one * size;
            string text = pred.leak ? "!" : GameData.Tool(pred.tool).glyph;
            icon.color = pred.leak ? Theme.Bad : Theme.ToolColor(pred.tool);
            if (iconText.text != text)
            {
                iconText.text = text;
                iconText.color = pred.leak ? Color.white : Theme.Ink;
            }
            if (ring)
            {
                iconRing.transform.localPosition = pos;
                iconRing.transform.localScale = Vector3.one * (size + 0.1f);
                iconRing.color = hint.predictedNeeded ? Theme.Good : Theme.Bad;
            }
        }
    }
}
