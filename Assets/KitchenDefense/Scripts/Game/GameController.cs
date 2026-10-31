using System.Collections.Generic;
using KitchenDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 게임 전체를 묶는다: 카메라·UI 생성, 단계(밤/낮/결과) 전환, 밤 시뮬레이션 고정 스텝 진행,
    /// 시뮬레이션 이벤트 → 연출, 처치 예상 계산, 저장/이어하기.
    /// 씬에는 GameBootstrap 하나만 있으면 되고 나머지는 전부 코드로 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        const float FixedStep = 1f / 30f;
        const int MaxStepsPerFrame = 8;
        const int PredictionsPerFrame = 6;
        const string SpeedKey = "kd_speed";
        const float NightOverDelay = 1.6f;

        public GameSession Session { get; private set; }
        public Camera Cam { get; private set; }
        public BoardView Board { get; private set; }
        public FxSystem Fx { get; private set; }
        public NightScreen NightUI { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; }
        public List<IngredientNeed> Needs { get { return needs; } }

        Canvas canvas;
        CanvasScaler scaler;
        CardScreens cards;
        Image backdrop;
        Image toastBg;
        Text toastText;
        float toastTime;

        readonly KitchenMap layoutMap = KitchenMap.CreateDefault();
        readonly KillPredictor predictor = new KillPredictor();
        readonly Dictionary<int, KillPrediction> predictions = new Dictionary<int, KillPrediction>();
        readonly List<int> staleIds = new List<int>();
        readonly HashSet<MonsterType> targetTypes = new HashSet<MonsterType>();
        List<IngredientNeed> needs = new List<IngredientNeed>();
        System.Func<MonsterState, BoardView.MonsterHint> hintFunc;

        float accumulator;
        float needsTimer;
        float autosaveTimer;
        float animTime;
        bool dirty;
        int predictCursor;
        float nightOverTimer = -1f;
        float lastBackPress = -10f;

        // ───────────── 수명 주기 ─────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = false;
            Speed = PlayerPrefs.GetInt(SpeedKey, 1) == 2 ? 2 : 1;
            hintFunc = HintFor;

            SetupCamera();
            SetupUI();
            Board = new BoardView(layoutMap);
            Fx = new FxSystem(Board.Root);
            Board.SetVisible(false);
            ShowTitle();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationPause(bool pause)
        {
            if (!pause) return;
            if (InWave) SetPaused(true);
            SaveNow();
        }

        void OnApplicationFocus(bool focus)
        {
            // 모바일에서 알림창·전화로 포커스를 잃으면 바로 멈춘다(에디터에서는 멈추지 않음).
            if (!focus && Application.isMobilePlatform && InWave) SetPaused(true);
        }

        void OnApplicationQuit()
        {
            SaveNow();
        }

        bool InNight { get { return Session != null && Session.Phase == GamePhase.Night && Session.Night != null; } }
        bool InWave { get { return InNight && Session.Night.State.waveActive; } }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                Cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            Cam.orthographic = true;
            Cam.orthographicSize = 8f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Theme.Background;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.transform.rotation = Quaternion.identity;
        }

        void SetupUI()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var es = new GameObject("EventSystem");
                eventSystem = es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            // 고해상도 폰에서는 손가락 떨림만으로 10px을 넘어 스크롤 속 버튼 탭이 드래그로 취소된다.
            // 안드로이드 기본 터치 슬롭(8dp)에 맞춘다(Screen.dpi가 0일 수 있어 최소 10px).
            eventSystem.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.dpi / 160f * 8f));

            var canvasGo = new GameObject("UI", typeof(RectTransform));
            canvasGo.layer = UIKit.UILayer;
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // 층 순서: 밤 HUD → (카드 배경) → 카드 → 일시정지 → 토스트
            var nightArea = UIKit.Node("NightSafeArea", root);
            UIKit.Stretch(nightArea);
            nightArea.gameObject.AddComponent<SafeAreaFitter>();
            NightUI = new NightScreen(this, nightArea);
            NightUI.Show(false);

            backdrop = UIKit.Image(root, "CardBackdrop", Theme.Paper);
            UIKit.Stretch(backdrop.rectTransform);
            backdrop.gameObject.SetActive(false);

            var cardArea = UIKit.Node("CardSafeArea", root);
            UIKit.Stretch(cardArea);
            cardArea.gameObject.AddComponent<SafeAreaFitter>();
            var cardsRoot = UIKit.Node("Cards", cardArea);
            UIKit.Stretch(cardsRoot);
            var overlayRoot = UIKit.Node("Overlay", cardArea);
            UIKit.Stretch(overlayRoot);
            cards = new CardScreens(this, cardsRoot, overlayRoot, backdrop);

            toastBg = UIKit.Panel(cardArea, "Toast", new Color(0f, 0f, 0f, 0.8f), 2f);
            UIKit.Bottom(toastBg.rectTransform, 84f, NightScreen.BottomHeight + 16f, 90f, 90f);
            toastBg.raycastTarget = false;
            toastText = UIKit.FitLabel(toastBg.transform, "Text", "", 38, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(toastText.rectTransform, 20f, 6f, 20f, 6f);
            toastBg.gameObject.SetActive(false);
        }

        // ───────────── 매 프레임 ─────────────

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            UpdateScaler();
            if (toastTime > 0f)
            {
                toastTime -= dt;
                if (toastTime <= 0f) toastBg.gameObject.SetActive(false);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) OnBack();
            if (Session == null) return;

            if (InNight)
            {
                var sim = Session.Night;
                if (!Paused && sim.State.waveActive)
                {
                    accumulator += dt * Speed;
                    int steps = 0;
                    while (accumulator >= FixedStep && steps < MaxStepsPerFrame)
                    {
                        sim.Step(FixedStep);
                        accumulator -= FixedStep;
                        steps++;
                        HandleEvents(sim);
                        if (!sim.State.waveActive)
                        {
                            accumulator = 0f;
                            break;
                        }
                    }
                    if (steps >= MaxStepsPerFrame) accumulator = 0f;
                }
                else
                {
                    accumulator = 0f;
                    HandleEvents(sim);
                }

                needsTimer -= dt;
                if (needsTimer <= 0f)
                {
                    RefreshNeeds();
                    needsTimer = 0.25f;
                }
                float animDt = Paused ? 0f : dt;
                animTime += animDt;
                if (!Paused) UpdatePredictions(sim);
                Board.Sync(sim, hintFunc, NightUI.SelectedTowerId, animTime, animDt);
                Fx.Update(animDt);
                NightUI.Refresh(dt);

                if (Session.NightOver)
                {
                    // 마지막 처치 연출과 "밤을 지켜냈다!" 배너를 보여 준 뒤 넘어간다.
                    if (nightOverTimer < 0f) nightOverTimer = NightOverDelay;
                    if (!Paused) nightOverTimer -= dt;
                    if (nightOverTimer <= 0f)
                    {
                        nightOverTimer = -1f;
                        Session.EndNight();
                        SaveNow();
                        ShowPhase();
                        return;
                    }
                }
            }

            if (dirty)
            {
                autosaveTimer -= dt;
                if (autosaveTimer <= 0f) SaveNow();
            }
        }

        void LateUpdate()
        {
            if (InNight) FitCamera();
        }

        /// <summary>세로로 긴 폰은 가로 기준, 태블릿처럼 덜 긴 화면은 세로 기준으로 UI 크기를 맞춘다.</summary>
        void UpdateScaler()
        {
            float aspect = Screen.width > 0 ? (float)Screen.height / Screen.width : 1.78f;
            float match = aspect < 1.6f ? 1f : 0f;
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, match)) scaler.matchWidthOrHeight = match;
        }

        /// <summary>위 HUD와 아래 버튼 바 사이 남는 공간에 주방 격자가 꽉 차게 카메라를 맞춘다.</summary>
        void FitCamera()
        {
            Rect safe = Screen.safeArea;
            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float top = safe.yMax - NightScreen.TopHeight * scale;
            float bottom = safe.yMin + NightScreen.BottomHeight * scale;
            float availH = Mathf.Max(100f, top - bottom);
            float availW = Mathf.Max(100f, safe.width);
            float boardW = layoutMap.Cols + 0.4f;
            float boardH = layoutMap.Rows + 0.4f;
            float ppu = Mathf.Min(availW / boardW, availH / boardH);
            if (ppu <= 0f) return;
            Cam.orthographicSize = Screen.height / ppu * 0.5f;
            float cx = safe.center.x;
            float cy = (top + bottom) * 0.5f;
            Cam.transform.position = new Vector3(-(cx - Screen.width * 0.5f) / ppu, -(cy - Screen.height * 0.5f) / ppu, -10f);
        }

        // ───────────── 밤: 이벤트·예상 ─────────────

        void HandleEvents(NightSimulation sim)
        {
            var events = sim.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                var pos = BoardView.ToWorld(e.position);
                switch (e.type)
                {
                    case SimEventType.Attack:
                        OnAttackFx(e, pos);
                        break;
                    case SimEventType.Killed:
                        OnKillFx(e, pos);
                        if (e.ingredientId != null)
                        {
                            var ing = GameData.Ingredient(e.ingredientId);
                            bool wanted = DayLogic.IsStillNeeded(needs, e.ingredientId);
                            Color col = wanted ? Theme.Hex(0x9CFF8A) : (ing.scraps ? Theme.Muted : Theme.HudText);
                            Fx.Text(pos + new Vector3(0f, 0.38f, 0f), "+" + ing.name + (wanted ? " (예약!)" : ""), col, wanted ? 0.32f : 0.26f);
                            if (wanted)
                            {
                                needsTimer = 0f;
                                Fx.TryPlay(PixelFxLibrary.CollectBurst, pos + new Vector3(0f, 0.15f, 0f), 0.5f);
                            }
                        }
                        break;
                    case SimEventType.Split:
                        Fx.Pop(pos, 0.55f, Theme.MonsterColor(MonsterType.Slime), 0.25f);
                        break;
                    case SimEventType.Leaked:
                        Board.FlashStorage();
                        Fx.TryPlay(PixelFxLibrary.HeartLost, pos + new Vector3(0f, 0.25f, 0f), 0.7f);
                        Fx.Text(pos + new Vector3(0f, 0.45f, 0f), Texts.Heart + " -1", Theme.Heart, 0.36f);
                        if (e.eatenId != null)
                            Fx.Text(pos + new Vector3(0f, 0.9f, 0f), Texts.IngredientName(e.eatenId) + " 먹힘!", Theme.Bad, 0.28f);
                        needsTimer = 0f;
                        break;
                    case SimEventType.WaveStarted:
                        NightUI.Banner("웨이브 " + (e.amount + 1) + " 시작!", Theme.Fire);
                        break;
                    case SimEventType.WaveCleared:
                        NightUI.Banner(sim.State.finished ? "밤을 지켜냈다!" : "웨이브 클리어!", Theme.Hex(0x9CFF8A));
                        MarkDirty();
                        autosaveTimer = 0f;
                        break;
                    case SimEventType.SeasonFailed:
                        NightUI.Banner("가게가 무너졌다...", Theme.Bad);
                        break;
                }
            }
            events.Clear();
        }

        void OnAttackFx(SimEvent e, Vector3 pos)
        {
            Board.PunchTower(e.towerId);
            switch (e.toolType)
            {
                case ToolType.Knife:
                    if (!Fx.TryPlay(PixelFxLibrary.HitSpark, pos, 0.42f, Random.value < 0.5f)) Fx.Slash(pos, Color.white);
                    break;
                case ToolType.Grill:
                    Fx.Ring(pos, GameData.Tool(ToolType.Grill).range, Theme.Hex(0xFF7043, 0.7f), 0.35f);
                    break;
                case ToolType.Freezer:
                    Fx.Ring(pos, GameData.Tool(ToolType.Freezer).range, Theme.Hex(0x81D4FA, 0.75f), 0.4f);
                    break;
                case ToolType.Cauldron:
                    // 삶기 막타 연출(물보라)은 Killed 이벤트에서 나온다.
                    if (PixelFxLibrary.Get(PixelFxLibrary.Splash) == null) Fx.Pop(pos, 1f, Theme.Hex(0xC5E1A5, 0.9f), 0.35f);
                    Fx.Text(pos + new Vector3(0f, 0.3f, 0f), "보글!", Theme.Hex(0xC5E1A5), 0.3f);
                    break;
            }
        }

        /// <summary>막타 연출: 냉동=눈꽃, 삶기=물보라, 그 외=펑(pixelfx). 시트가 없으면 기구 색 원.</summary>
        void OnKillFx(SimEvent e, Vector3 pos)
        {
            string clip;
            float scale;
            switch (e.method)
            {
                case CookMethod.Freeze: clip = PixelFxLibrary.SnowflakeBurst; scale = 0.5f; break;
                case CookMethod.Boil: clip = PixelFxLibrary.Splash; scale = 0.45f; break;
                default: clip = PixelFxLibrary.Poof; scale = 0.5f; break;
            }
            if (!Fx.TryPlay(clip, pos, scale)) Fx.Pop(pos, 0.75f, Theme.ToolColor(e.toolType), 0.3f);
        }

        void RefreshNeeds()
        {
            needs = Session.UpcomingNeeds();
            targetTypes.Clear();
            foreach (var n in needs)
                if (n.have < n.needed) targetTypes.Add(GameData.Ingredient(n.ingredientId).monster);
        }

        void UpdatePredictions(NightSimulation sim)
        {
            var list = sim.State.monsters;
            if (predictions.Count > 0)
            {
                staleIds.Clear();
                foreach (var id in predictions.Keys)
                    if (sim.MonsterById(id) == null) staleIds.Add(id);
                foreach (var id in staleIds) predictions.Remove(id);
            }
            int count = list.Count;
            if (count == 0) return;
            int budget = Mathf.Min(count, PredictionsPerFrame);
            for (int k = 0; k < budget; k++)
            {
                predictCursor = (predictCursor + 1) % count;
                var m = list[predictCursor];
                if (m.alive) predictions[m.id] = predictor.Predict(sim, m);
            }
        }

        BoardView.MonsterHint HintFor(MonsterState m)
        {
            var h = new BoardView.MonsterHint();
            KillPrediction p;
            if (predictions.TryGetValue(m.id, out p))
            {
                h.hasPrediction = true;
                h.prediction = p;
                h.predictedNeeded = !p.leak && DayLogic.IsStillNeeded(needs, p.ingredientId);
            }
            h.isTarget = targetTypes.Contains(m.type);
            return h;
        }

        // ───────────── 단계 전환 ─────────────

        void ShowTitle()
        {
            Paused = false;
            cards.HideOverlay();
            Board.SetVisible(false);
            NightUI.Show(false);
            cards.ShowTitle();
        }

        void ShowPhase()
        {
            cards.HideOverlay();
            Paused = false;
            nightOverTimer = -1f;
            var phase = Session.Phase;
            bool night = phase == GamePhase.Night;
            Board.SetVisible(night);
            NightUI.Show(night);
            switch (phase)
            {
                case GamePhase.Intro: cards.ShowIntro(); break;
                case GamePhase.Night: cards.Hide(); break;
                case GamePhase.NightResult: cards.ShowNightResult(); break;
                case GamePhase.DayMenu: cards.ShowDayMenu(); break;
                case GamePhase.DayResult: cards.ShowDayResult(); break;
                case GamePhase.DayShop: cards.ShowShop(); break;
                case GamePhase.DayClosing: cards.ShowClosing(); break;
                case GamePhase.SeasonResult:
                    bool best = SaveSystem.SubmitScore(Session.Score);
                    cards.ShowSeasonResult(best);
                    break;
            }
        }

        void BeginNightView()
        {
            Board.ClearUnits();
            Fx.Clear();
            predictions.Clear();
            predictCursor = 0;
            accumulator = 0f;
            nightOverTimer = -1f;
            NightUI.ResetInteraction();
            RefreshNeeds();
        }

        public void NewSeason()
        {
            Session = GameSession.NewSeason(Random.Range(1, int.MaxValue));
            SaveNow();
            ShowPhase();
        }

        public void ContinueSeason()
        {
            var run = SaveSystem.Load();
            if (run == null)
            {
                Toast("세이브를 불러오지 못했어요");
                ShowTitle();
                return;
            }
            Session = new GameSession(run);
            if (run.phase == GamePhase.Night) BeginNightView();
            ShowPhase();
            // 웨이브 도중에 나갔다면 멈춘 상태로 돌아와 숨 돌릴 틈을 준다.
            if (InWave) SetPaused(true);
        }

        public void GoToTitle()
        {
            SaveNow();
            Session = null;
            ShowTitle();
        }

        public void AbandonSeason()
        {
            SaveSystem.Delete();
            Session = null;
            ShowTitle();
        }

        public void ResolveOfferAndStartNight(bool accept)
        {
            if (Session == null) return;
            if (accept) Session.AcceptOffer();
            else Session.DeclineOffer();
            Session.StartNight();
            BeginNightView();
            SaveNow();
            ShowPhase();
        }

        public void BeginDay()
        {
            if (Session == null) return;
            Session.BeginDay();
            SaveNow();
            ShowPhase();
        }

        public void RunBusiness()
        {
            if (Session == null) return;
            Session.RunBusiness();
            SaveNow();
            ShowPhase();
        }

        public void AfterResult()
        {
            if (Session == null) return;
            Session.AfterResult();
            SaveNow();
            ShowPhase();
        }

        public void CloseShop()
        {
            if (Session == null) return;
            Session.CloseShop();
            SaveNow();
            ShowPhase();
        }

        // ───────────── 일시정지·기타 ─────────────

        public void ShowPause()
        {
            if (!InNight) return;
            SetPaused(true);
        }

        void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused)
            {
                cards.ShowPause();
                SaveNow();
            }
            else cards.HideOverlay();
        }

        public void ResumeFromPause()
        {
            // 일시정지 중에 연 '게임 방법' 카드가 남아 있으면 같이 닫는다(카드 뒤에서 웨이브가 돌지 않게).
            if (InNight) cards.Hide();
            SetPaused(false);
        }

        public void ReturnFromHelp()
        {
            cards.Hide();
            if (InNight) SetPaused(true);
            else ShowTitle();
        }

        /// <summary>안드로이드 뒤로 버튼. 밤에는 일시정지, 낮·타이틀에서는 두 번 누르면 타이틀로/종료.</summary>
        void OnBack()
        {
            if (cards.HasOverlay)
            {
                ResumeFromPause();
                return;
            }
            if (cards.HelpOpen)
            {
                cards.BackFromHelp();
                return;
            }
            if (InNight)
            {
                ShowPause();
                return;
            }
            bool second = Time.unscaledTime - lastBackPress < 2f;
            lastBackPress = Time.unscaledTime;
            if (Session == null)
            {
                if (second) Application.Quit();
                else Toast("뒤로 버튼을 한 번 더 누르면 종료합니다");
            }
            else
            {
                if (second) GoToTitle();
                else Toast("뒤로 버튼을 한 번 더 누르면 저장하고 타이틀로 갑니다");
            }
        }

        public void ToggleSpeed()
        {
            Speed = Speed == 1 ? 2 : 1;
            PlayerPrefs.SetInt(SpeedKey, Speed);
        }

        public void Toast(string text)
        {
            toastText.text = text;
            toastBg.gameObject.SetActive(true);
            toastTime = 1.8f;
        }

        public void MarkDirty()
        {
            dirty = true;
            autosaveTimer = 1f;
        }

        public void SaveNow()
        {
            dirty = false;
            if (Session == null) return;
            // 끝난 시즌은 이어할 것이 없으니 세이브를 지운다(최고 점수는 따로 남는다).
            if (Session.Phase == GamePhase.SeasonResult) SaveSystem.Delete();
            else SaveSystem.Save(Session.Run);
        }
    }
}
