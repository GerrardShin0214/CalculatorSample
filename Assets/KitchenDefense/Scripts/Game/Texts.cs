using System.Collections.Generic;
using System.Text;
using KitchenDefense.Core;
using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 화면 문구 모음. 주아체에 없는 기호(×, ·, ▶, →, …)는 쓰지 않는다.
    /// 쓸 수 있는 기호: ♥(내구도) ★(평판) “ ” 와 ASCII.
    /// </summary>
    public static class Texts
    {
        public const string Heart = "♥";
        public const string Star = "★";

        public static string IngredientName(string id) { return GameData.Ingredient(id).name; }

        public static string RecipeName(string id)
        {
            var r = GameData.Recipe(id);
            return r != null ? r.name : id;
        }

        /// <summary>["a","b","a"] → "a x2, b"</summary>
        public static string Group(IList<string> ids, string separator = ", ", bool colored = false)
        {
            if (ids == null || ids.Count == 0) return "없음";
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var id in ids)
            {
                int c;
                if (!counts.TryGetValue(id, out c)) order.Add(id);
                counts[id] = c + 1;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0) sb.Append(separator);
                string name = IngredientName(order[i]);
                if (colored) name = Theme.Colorize(name, Color.Lerp(Theme.IngredientColor(order[i]), Theme.Ink, 0.35f));
                sb.Append(name);
                if (counts[order[i]] > 1) sb.Append(" x").Append(counts[order[i]]);
            }
            return sb.ToString();
        }

        public static string RecipeIngredients(RecipeDef r)
        {
            return Group(r.ingredients, " + ");
        }

        public static string ReservationLine(Reservation r)
        {
            return r.party + " | " + RecipeName(r.recipeId) + " x" + r.servings + " | 보상 " + r.rewardGold + "G, 평판 +" + r.rewardRep;
        }

        /// <summary>예약에 필요한 재료와 얻는 법. 예: "구운 갈비 x2 (뿔멧돼지를 그릴로 막타)"</summary>
        public static string ReservationNeedsHowTo(Reservation r)
        {
            var recipe = GameData.Recipe(r.recipeId);
            if (recipe == null) return "";
            var ids = new List<string>();
            for (int s = 0; s < r.servings; s++) ids.AddRange(recipe.ingredients);
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var id in ids)
            {
                int c;
                if (!counts.TryGetValue(id, out c)) order.Add(id);
                counts[id] = c + 1;
            }
            var sb = new StringBuilder();
            foreach (var id in order)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("- ").Append(IngredientName(id)).Append(" x").Append(counts[id]).Append("  (").Append(GameData.HowToGet(id)).Append(')');
            }
            return sb.ToString();
        }

        /// <summary>밤 HUD용 한 줄: "구운 갈비 1/2, 구운 버섯 0/2"</summary>
        public static string NeedsProgress(List<IngredientNeed> needs)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < needs.Count; i++)
            {
                var n = needs[i];
                if (i > 0) sb.Append(", ");
                bool done = n.have >= n.needed;
                string s = IngredientName(n.ingredientId) + " " + Mathf.Min(n.have, n.needed) + "/" + n.needed;
                sb.Append(Theme.Colorize(s, done ? Theme.Hex(0x9CCC65) : Theme.HudText));
                if (!done && n.frozenSubstitutes > 0) sb.Append(Theme.Colorize("(냉동 대체 " + n.frozenSubstitutes + ")", Theme.Hex(0x81D4FA)));
            }
            return sb.ToString();
        }

        public static string WavePreview(WaveDef wave)
        {
            if (wave == null || wave.spawns.Count == 0) return "";
            var counts = new int[GameData.Monsters.Length];
            foreach (var s in wave.spawns) counts[(int)s.type]++;
            var sb = new StringBuilder();
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(GameData.Monsters[i].name).Append(' ').Append(counts[i]);
            }
            return sb.ToString();
        }

        public static string BuildFailReason(BuildResult r)
        {
            switch (r)
            {
                case BuildResult.Occupied: return "이미 식구가 있는 칸이에요";
                case BuildResult.NeedsPath: return "솥할매는 길 위에만 설 수 있어요";
                case BuildResult.NeedsFloor: return "길 위에는 솥할매만 설 수 있어요";
                case BuildResult.NoFirepower: return "화력이 부족해요";
                case BuildResult.Reserved: return "뒷문과 창고에는 놓을 수 없어요";
                case BuildResult.NotNow: return "이동은 웨이브 사이 준비 시간에만 돼요";
                default: return "여기에는 놓을 수 없어요";
            }
        }

        public static string ShelfLabel(int daysLeft)
        {
            if (daysLeft <= 1) return "오늘까지";
            return daysLeft + "일";
        }

        public static string RankComment(string rank)
        {
            switch (rank)
            {
                case "S": return "“이 집 막타는 예술이다.” 평론가가 별 다섯 개를 줬다!";
                case "A": return "“재료 손질이 예술이다.” 다음 시즌이 기대된다.";
                case "B": return "“맛은 있는데, 막타가 조금 아쉽다.”";
                case "C": return "“가게는 지켰지만 요리는 글쎄...”";
                default: return "주방이 엉망이 됐다. 하지만 경험은 남는다.";
            }
        }
    }
}
