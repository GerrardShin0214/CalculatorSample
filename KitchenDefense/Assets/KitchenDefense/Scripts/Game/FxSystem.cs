using System.Collections.Generic;
using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>짧은 연출(칼질, 불꽃 고리, 냉기, 보글보글, 떠오르는 글자). 오브젝트는 풀링한다.</summary>
    public sealed class FxSystem
    {
        sealed class Item
        {
            public GameObject go;
            public SpriteRenderer sr;
            public TextMesh tm;
            public float t;
            public float life;
            public Vector3 velocity;
            public float scaleFrom;
            public float scaleTo;
            public Color color;
            public bool text;
        }

        readonly Transform root;
        readonly List<Item> active = new List<Item>();
        readonly Stack<Item> spritePool = new Stack<Item>();
        readonly Stack<Item> textPool = new Stack<Item>();

        public FxSystem(Transform parent)
        {
            root = new GameObject("Fx").transform;
            root.SetParent(parent, false);
        }

        Item TakeSprite(Sprite sprite, int order)
        {
            Item it = spritePool.Count > 0 ? spritePool.Pop() : null;
            if (it == null)
            {
                it = new Item();
                it.sr = BoardView.Sprite("Fx", root, sprite, Color.white, order);
                it.go = it.sr.gameObject;
            }
            it.sr.sprite = sprite;
            it.sr.sortingOrder = order;
            it.go.transform.localRotation = Quaternion.identity;
            it.go.SetActive(true);
            it.t = 0f;
            it.text = false;
            it.velocity = Vector3.zero;
            active.Add(it);
            return it;
        }

        /// <summary>그릴·냉동고 공격: 사거리까지 퍼지는 고리.</summary>
        public void Ring(Vector3 pos, float radius, Color color, float life = 0.35f)
        {
            var it = TakeSprite(SpriteFactory.Ring, 35);
            it.go.transform.localPosition = pos;
            it.scaleFrom = radius * 0.6f;
            it.scaleTo = radius * 2f;
            it.color = color;
            it.life = life;
        }

        /// <summary>처치·보글 연출: 퍼지며 사라지는 원.</summary>
        public void Pop(Vector3 pos, float size, Color color, float life = 0.3f)
        {
            var it = TakeSprite(SpriteFactory.Circle, 36);
            it.go.transform.localPosition = pos;
            it.scaleFrom = size * 0.4f;
            it.scaleTo = size;
            it.color = color;
            it.life = life;
        }

        /// <summary>식칼 공격: 대각선 칼자국.</summary>
        public void Slash(Vector3 pos, Color color)
        {
            var it = TakeSprite(SpriteFactory.Square, 37);
            it.go.transform.localPosition = pos + new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(-0.06f, 0.06f), 0f);
            it.go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(25f, 65f));
            it.scaleFrom = 0.5f;
            it.scaleTo = 0.62f;
            it.color = color;
            it.life = 0.16f;
            it.go.transform.localScale = new Vector3(0.5f, 0.06f, 1f);
        }

        public void Text(Vector3 pos, string text, Color color, float height = 0.28f, float life = 1.1f)
        {
            Item it = textPool.Count > 0 ? textPool.Pop() : null;
            if (it == null)
            {
                it = new Item();
                it.tm = WorldText.Create(root, "FxText", text, height, color, 45);
                it.go = it.tm.gameObject;
            }
            it.go.SetActive(true);
            WorldText.SetHeight(it.tm, height);
            it.tm.text = text;
            it.tm.color = color;
            it.go.transform.localPosition = pos;
            it.go.transform.localScale = Vector3.one;
            it.velocity = new Vector3(0f, 0.65f, 0f);
            it.color = color;
            it.life = life;
            it.t = 0f;
            it.text = true;
            active.Add(it);
        }

        public void Update(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var it = active[i];
                it.t += dt;
                float k = it.life > 0f ? Mathf.Clamp01(it.t / it.life) : 1f;
                if (it.text)
                {
                    it.go.transform.localPosition += it.velocity * dt;
                    var c = it.color;
                    c.a = it.color.a * (k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                    it.tm.color = c;
                }
                else
                {
                    var c = it.color;
                    c.a = it.color.a * (1f - k);
                    it.sr.color = c;
                    float s = Mathf.Lerp(it.scaleFrom, it.scaleTo, 1f - (1f - k) * (1f - k));
                    if (it.sr.sprite == SpriteFactory.Square) it.go.transform.localScale = new Vector3(s, 0.06f, 1f);
                    else it.go.transform.localScale = new Vector3(s, s, 1f);
                }
                if (k >= 1f)
                {
                    it.go.SetActive(false);
                    active.RemoveAt(i);
                    if (it.text) textPool.Push(it);
                    else spritePool.Push(it);
                }
            }
        }

        public void Clear()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var it = active[i];
                it.go.SetActive(false);
                if (it.text) textPool.Push(it);
                else spritePool.Push(it);
            }
            active.Clear();
        }
    }
}
