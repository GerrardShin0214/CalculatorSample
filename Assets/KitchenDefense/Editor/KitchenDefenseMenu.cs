using KitchenDefense.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KitchenDefense.EditorTools
{
    /// <summary>
    /// 상단 메뉴 "Kitchen Defense". ProjectSettings에 이미 세로 화면·IL2CPP·ARM64가 들어 있지만,
    /// 설정이 바뀌었을 때 한 번에 되돌릴 수 있게 메뉴로도 둔다.
    /// </summary>
    public static class KitchenDefenseMenu
    {
        public const string MainScenePath = "Assets/KitchenDefense/Scenes/Main.unity";
        const string BundleId = "com.gerrardshin.kitchendefense";

        [MenuItem("Kitchen Defense/메인 씬 열기", false, 0)]
        public static void OpenMainScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MainScenePath);
        }

        [MenuItem("Kitchen Defense/모바일 빌드 설정 적용 (세로, Android IL2CPP ARM64)", false, 20)]
        public static void ApplyMobileSettings()
        {
            PlayerSettings.companyName = "GerrardShin";
            PlayerSettings.productName = "막타 키친";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25;
            EnsureMainSceneInBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[KitchenDefense] 모바일 빌드 설정을 적용했습니다.");
        }

        [MenuItem("Kitchen Defense/세이브 데이터 지우기", false, 40)]
        public static void DeleteSave()
        {
            SaveSystem.Delete();
            SaveSystem.ResetBestScore();
            PlayerPrefs.DeleteKey("kd_speed");
            Debug.Log("[KitchenDefense] 세이브와 최고 점수를 지웠습니다: " + SaveSystem.FilePath);
        }

        static void EnsureMainSceneInBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == MainScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
            list.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
