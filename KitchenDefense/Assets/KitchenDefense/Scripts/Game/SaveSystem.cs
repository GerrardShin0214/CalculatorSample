using System;
using System.IO;
using KitchenDefense.Core;
using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 기획서 모바일 설계: "앱을 나가면 그 순간 상태를 저장하고 그대로 이어서 한다. 전화가 와도 손해가 없어야 한다."
    /// RunState 하나를 JSON으로 통째 저장한다(웨이브 도중의 몬스터 위치까지 포함).
    /// </summary>
    public static class SaveSystem
    {
        public const string FileName = "kitchen_defense_save.json";
        const string BestScoreKey = "kd_best_score";

        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        public static bool HasSave()
        {
            try { return File.Exists(FilePath); }
            catch (Exception) { return false; }
        }

        public static void Save(RunState run)
        {
            if (run == null) return;
            try
            {
                string json = JsonUtility.ToJson(run);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitchenDefense] 저장 실패: " + e.Message);
            }
        }

        public static RunState Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var run = JsonUtility.FromJson<RunState>(File.ReadAllText(FilePath));
                if (run == null || run.version != GameSession.SaveVersion) return null;
                return run;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitchenDefense] 불러오기 실패: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitchenDefense] 세이브 삭제 실패: " + e.Message);
            }
        }

        public static int BestScore { get { return PlayerPrefs.GetInt(BestScoreKey, 0); } }

        /// <summary>최고 점수를 갱신했으면 true.</summary>
        public static bool SubmitScore(int score)
        {
            if (score <= BestScore) return false;
            PlayerPrefs.SetInt(BestScoreKey, score);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetBestScore()
        {
            PlayerPrefs.DeleteKey(BestScoreKey);
            PlayerPrefs.Save();
        }
    }
}
