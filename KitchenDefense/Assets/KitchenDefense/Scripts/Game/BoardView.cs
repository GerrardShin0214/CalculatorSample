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
                v.Sync(sim, t, CellPosition(t.col, t.row), t.id == selectedTowerId, time, dt);
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

        void Release(MonsterView v)
        {
            v.Go.SetActive(false);
            monsterPool.Push(v);
        }
    }

    /// <summary>조리기구 하나의 그림.</summary>
    public sealed class TowerView
    {
        public readonly GameObject Go;
        public readonly ToolType Type;
        readonly SpriteRenderer shadow;
        readonly SpriteRenderer body;
        readonly SpriteRenderer inner;
        readonly TextMesh glyph;
        readonly SpriteRenderer[] pips = new SpriteRenderer[2];
        readonly SpriteRenderer lowBadge;
        readonly TextMesh lowText;
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

            glyph = WorldText.Create(t, "Glyph", GameData.Tool(type).glyph, 0.4f, pot ? Color.white : Theme.Ink, 7);

            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = BoardView.Sprite("Pip", t, SpriteFactory.Circle, Theme.Star, 8);
                pips[i].transform.localPosition = new Vector3(-0.1f + 0.2f * i, -0.32f, 0f);
                pips[i].transform.localScale = Vector3.one * 0.13f;
            }

            lowBadge = BoardView.Sprite("LowHeat", t, SpriteFactory.Circle, Theme.Blue, 8);
            lowBadge.transform.localPosition = new Vector3(0.3f, 0.3f, 0f);
            lowBadge.transform.localScale = Vector3.one * 0.3f;
            lowText = WorldText.Create(lowBadge.transform, "Text", "약", 0.62f, Color.white, 9);
        }

        public void Punch() { punch = 1f; }

        public void Sync(NightSimulation sim, TowerState t, Vector3 position, bool selected, float time, float dt)
        {
            if (!Go.activeSelf) Go.SetActive(true);
            Go.transform.localPosition = position;
            punch = Mathf.Max(0f, punch - dt * 6f);
            float s = 1f + 0.14f * punch + (selected ? 0.04f * Mathf.Sin(time * 6f) : 0f);
            Go.transform.localScale = new Vector3(s, s, 1f);

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
                float rate = sim.TowerRate(t);
                Color baseColor = Theme.ToolColor(Type);
                body.color = rate < 1f ? Color.Lerp(baseColor, Theme.Hex(0xB39DDB), 0.45f) : baseColor;
            }
        }
    }

    /// <summary>몬스터 하나의 그림: 몸통 + 눈 + 체력바 + 처치 예상 아이콘.</summary>
    public sealed class MonsterView
    {
        public readonly GameObject Go;
        public MonsterType Type { get; private set; }

        readonly SortingGroup group;
        readonly Transform bodyRoot;
        readonly SpriteRenderer outline;
        readonly SpriteRenderer body;
        readonly SpriteRenderer shine;
        readonly SpriteRenderer[] extras = new SpriteRenderer[3];
        readonly SpriteRenderer eyeL, eyeR, pupilL, pupilR;
        readonly SpriteRenderer burn;
        readonly SpriteRenderer hpBack, hpFill, hpTick;
        readonly SpriteRenderer icon, iconRing;
        readonly TextMesh iconText;
        float radius;
        Color baseColor;

        const float HpWidth = 0.6f;
        const float HpHeight = 0.09f;

        public MonsterView(Transform parent)
        {
            Go = new GameObject("Monster");
            Go.transform.SetParent(parent, false);
            group = Go.AddComponent<SortingGroup>();
            group.sortingOrder = 10;

            bodyRoot = new GameObject("BodyRoot").transform;
            bodyRoot.SetParent(Go.transform, false);
            outline = BoardView.Sprite("Outline", bodyRoot, SpriteFactory.Circle, Color.black, 0);
            body = BoardView.Sprite("Body", bodyRoot, SpriteFactory.Circle, Color.white, 1);
            for (int i = 0; i < extras.Length; i++) extras[i] = BoardView.Sprite("Extra", bodyRoot, SpriteFactory.Circle, Color.white, 2);
            shine = BoardView.Sprite("Shine", bodyRoot, SpriteFactory.Circle, new Color(1f, 1f, 1f, 0.45f), 3);
            eyeL = BoardView.Sprite("EyeL", bodyRoot, SpriteFactory.Circle, Color.white, 4);
            eyeR = BoardView.Sprite("EyeR", bodyRoot, SpriteFactory.Circle, Color.white, 4);
            pupilL = BoardView.Sprite("PupilL", bodyRoot, SpriteFactory.Circle, Theme.Hex(0x1B1411), 5);
            pupilR = BoardView.Sprite("PupilR", bodyRoot, SpriteFactory.Circle, Theme.Hex(0x1B1411), 5);
            burn = BoardView.Sprite("Burn", bodyRoot, SpriteFactory.Circle, Theme.Hex(0xFF7043, 0.85f), 6);

            hpBack = BoardView.Sprite("HpBack", Go.transform, SpriteFactory.Square, new Color(0f, 0f, 0f, 0.7f), 10);
            hpFill = BoardView.Sprite("HpFill", Go.transform, SpriteFactory.Square, Theme.Good, 11);
            hpTick = BoardView.Sprite("HpTick", Go.transform, SpriteFactory.Square, new Color(0f, 0f, 0f, 0.8f), 12);
            iconRing = BoardView.Sprite("IconRing", Go.transform, SpriteFactory.Circle, Color.white, 13);
            icon = BoardView.Sprite("Icon", Go.transform, SpriteFactory.Circle, Color.white, 14);
            iconText = WorldText.Create(icon.transform, "IconText", "", 0.7f, Theme.Ink, 15);
        }

        public void Setup(MonsterType type)
        {
            Type = type;
            var def = GameData.Monster(type);
            radius = def.radius;
            baseColor = Theme.MonsterColor(type);
            Go.name = "Monster " + type;
            Go.SetActive(true);

            float d = radius * 2f;
            outline.transform.localScale = Vector3.one * d * 1.14f;
            outline.color = Color.Lerp(baseColor, Color.black, 0.6f);
            body.transform.localScale = Vector3.one * d;
            body.color = baseColor;
            shine.transform.localPosition = new Vector3(-radius * 0.38f, radius * 0.42f, 0f);
            shine.transform.localScale = Vector3.one * d * 0.24f;

            for (int i = 0; i < extras.Length; i++) extras[i].gameObject.SetActive(false);
            switch (type)
            {
                case MonsterType.Mushroom:
                    // 버섯갓의 흰 점
                    SetExtra(0, new Vector3(-radius * 0.45f, radius * 0.45f, 0f), d * 0.22f, Color.white);
                    SetExtra(1, new Vector3(radius * 0.5f, radius * 0.35f, 0f), d * 0.18f, Color.white);
                    SetExtra(2, new Vector3(radius * 0.05f, radius * 0.72f, 0f), d * 0.16f, Color.white);
                    shine.gameObject.SetActive(false);
                    break;
                case MonsterType.Boar:
                    // 뿔과 코
                    SetExtra(0, new Vector3(-radius * 0.62f, radius * 0.78f, 0f), d * 0.24f, Theme.Hex(0xF5EBDD));
                    SetExtra(1, new Vector3(radius * 0.62f, radius * 0.78f, 0f), d * 0.24f, Theme.Hex(0xF5EBDD));
                    SetExtra(2, new Vector3(0f, -radius * 0.38f, 0f), d * 0.36f, Theme.Hex(0xE8A598));
                    shine.gameObject.SetActive(false);
                    break;
                case MonsterType.Crab:
                    // 집게
                    SetExtra(0, new Vector3(-radius * 1.05f, radius * 0.25f, 0f), d * 0.42f, Color.Lerp(baseColor, Color.red, 0.3f));
                    SetExtra(1, new Vector3(radius * 1.05f, radius * 0.25f, 0f), d * 0.42f, Color.Lerp(baseColor, Color.red, 0.3f));
                    shine.gameObject.SetActive(true);
                    break;
                default:
                    shine.gameObject.SetActive(true);
                    break;
            }

            float eye = d * 0.3f;
            eyeL.transform.localPosition = new Vector3(-radius * 0.34f, radius * 0.05f, 0f);
            eyeR.transform.localPosition = new Vector3(radius * 0.34f, radius * 0.05f, 0f);
            eyeL.transform.localScale = Vector3.one * eye;
            eyeR.transform.localScale = Vector3.one * eye;
            pupilL.transform.localScale = Vector3.one * eye * 0.5f;
            pupilR.transform.localScale = Vector3.one * eye * 0.5f;
            burn.transform.localPosition = new Vector3(radius * 0.55f, radius * 0.75f, 0f);
            burn.transform.localScale = Vector3.one * d * 0.28f;

            float barY = radius + 0.15f;
            hpBack.transform.localPosition = new Vector3(0f, barY, 0f);
            hpBack.transform.localScale = new Vector3(HpWidth + 0.04f, HpHeight + 0.04f, 1f);
            hpTick.transform.localPosition = new Vector3(-HpWidth * 0.5f + HpWidth * Combat.CauldronThreshold(), barY, 0f);
            hpTick.transform.localScale = new Vector3(0.025f, HpHeight + 0.04f, 1f);
        }

        void SetExtra(int i, Vector3 pos, float size, Color color)
        {
            var e = extras[i];
            e.gameObject.SetActive(true);
            e.transform.localPosition = pos;
            e.transform.localScale = Vector3.one * size;
            e.color = color;
        }

        public void Sync(KitchenMap map, MonsterState m, bool charging, BoardView.MonsterHint hint, float time)
        {
            if (!Go.activeSelf) Go.SetActive(true);
            Vec2 p = map.PositionAt(m.distance);
            Vec2 dir = map.DirectionAt(m.distance);
            Vec2 side = new Vec2(-dir.y, dir.x);
            Go.transform.localPosition = new Vector3(p.x + side.x * m.lateral, p.y + side.y * m.lateral, -0.001f * (m.id % 97));

            // 통통 튀는 걸음
            float bob = Mathf.Abs(Mathf.Sin(time * (charging ? 16f : 9f) + m.id)) * 0.05f;
            bodyRoot.localPosition = new Vector3(0f, bob, 0f);
            float squash = Type == MonsterType.Slime || Type == MonsterType.SlimeSmall ? 1f + 0.06f * Mathf.Sin(time * 7f + m.id) : 1f;
            bodyRoot.localScale = new Vector3(squash, 2f - squash, 1f);

            Color c = baseColor;
            if (m.slowTimer > 0f) c = Color.Lerp(c, Theme.Hex(0xB3E5FC), 0.5f);
            if (charging) c = Color.Lerp(c, Theme.Hex(0xFF5252), 0.25f + 0.15f * Mathf.Sin(time * 20f));
            body.color = c;
            bool burning = m.burnTimer > 0f;
            if (burn.gameObject.activeSelf != burning) burn.gameObject.SetActive(burning);
            if (burning) burn.transform.localScale = Vector3.one * radius * 2f * (0.24f + 0.06f * Mathf.Sin(time * 25f));

            Vector3 look = new Vector3(dir.x, dir.y, 0f) * radius * 0.08f;
            pupilL.transform.localPosition = eyeL.transform.localPosition + look;
            pupilR.transform.localPosition = eyeR.transform.localPosition + look;

            float ratio = m.maxHp > 0f ? Mathf.Clamp01(m.hp / m.maxHp) : 0f;
            float barY = radius + 0.15f;
            hpFill.transform.localScale = new Vector3(HpWidth * ratio, HpHeight, 1f);
            hpFill.transform.localPosition = new Vector3(-HpWidth * 0.5f + HpWidth * ratio * 0.5f, barY, 0f);
            // 25% 이하(빨강) = 가마솥 확정 처치 구간
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
            // 기획서: 처치 예상 아이콘은 예약 대상 몬스터에만 크게 띄운다.
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
