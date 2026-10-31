using UnityEngine;
using UnityEngine.SceneManagement;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 진입점. Main 씬의 "Game" 오브젝트에 붙어 있다. 나머지(카메라 설정, UI, 보드)는 GameController가 코드로 만든다.
    /// 씬 없이 빈 씬에서 Play를 눌러도 게임이 뜨도록 자동 시작도 해 둔다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Ensure();
        }

        public static void Ensure()
        {
            if (GameController.Instance != null) return;
            new GameObject("GameController").AddComponent<GameController>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "Main" || string.IsNullOrEmpty(scene.path)) Ensure();
        }
    }
}
