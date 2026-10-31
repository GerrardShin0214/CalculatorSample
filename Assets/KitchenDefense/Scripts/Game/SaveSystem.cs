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
        static string TempPath { get { return FilePath + ".tmp"; } }

        public static bool HasSave()
        {
            try { return File.Exists(FilePath) || File.Exists(TempPath); }
            catch (Exception) { return false; }
        }

        public static void Save(RunState run)
        {
            if (run == null) return;
            try
            {
                // 임시 파일에 다 쓴 다음 바꿔 끼운다. 그 사이에 앱이 죽어도 Load가 임시 파일로 살려 낸다.
                string json = JsonUtility.ToJson(run);
                File.WriteAllText(TempPath, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(TempPath, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitchenDefense] 저장 실패: " + e.Message);
            }
        }

        public static RunState Load()
        {
            // 저장 도중 앱이 죽어 임시 파일이 남았다면 그쪽이 더 최신이다(다 써진 경우에만 읽힌다).
            var run = TryRead(TempPath);
            if (run != null)
            {
                try
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    File.Move(TempPath, FilePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[KitchenDefense] 임시 세이브 복구 실패: " + e.Message);
                }
                return run;
            }
            return TryRead(FilePath);
        }

        static RunState TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var run = JsonUtility.FromJson<RunState>(File.ReadAllText(path));
                if (run == null || run.version != GameSession.SaveVersion) return null;
                return run;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitchenDefense] 불러오기 실패(" + Path.GetFileName(path) + "): " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
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
