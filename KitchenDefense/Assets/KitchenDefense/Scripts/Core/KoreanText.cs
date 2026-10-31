namespace KitchenDefense.Core
{
    /// <summary>받침에 따라 조사를 붙인다. 예: Eul("뿔멧돼지") = "뿔멧돼지를", Euro("가마솥") = "가마솥으로".</summary>
    public static class KoreanText
    {
        /// <summary>마지막 글자의 받침(종성) 번호. 0이면 받침 없음, 8이면 ㄹ, 한글이 아니면 -1.</summary>
        public static int FinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return -1;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return -1;
            return (c - 0xAC00) % 28;
        }

        public static bool HasBatchim(string word) { return FinalConsonant(word) > 0; }

        public static string Eul(string word) { return word + (HasBatchim(word) ? "을" : "를"); }
        public static string Eun(string word) { return word + (HasBatchim(word) ? "은" : "는"); }
        public static string Iga(string word) { return word + (HasBatchim(word) ? "이" : "가"); }
        public static string Gwa(string word) { return word + (HasBatchim(word) ? "과" : "와"); }

        /// <summary>'으로/로'. ㄹ 받침 뒤에는 '로'.</summary>
        public static string Euro(string word)
        {
            int f = FinalConsonant(word);
            return word + (f > 0 && f != 8 ? "으로" : "로");
        }
    }
}
