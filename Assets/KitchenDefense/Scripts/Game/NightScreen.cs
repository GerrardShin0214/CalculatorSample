using System.Text;
using KitchenDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDefense.Game
{
    /// <summary>
    /// 밤 화면 HUD와 조작. 세로 한 손 플레이 기준으로 위에는 정보, 아래(엄지 닿는 곳)에는 모든 버튼.
    /// 조작: 빈 칸 탭 → 하단 바에서 기구 선택 / 기구 먼저 고르고 초록 칸 탭 / 설치된 기구 탭 → 약불·강화·이동·회수.
    /// </summary>
    public sealed class NightScreen
    {
        public const float TopHeight = 250f;
        public const float BottomHeight = 500f;

        readonly GameController ctl;
        public readonly RectTransform Root;

        Text title;
        Text stats;
        Text goal;
        Text context;
        Text banner;
        RectTransform towerPanel;
        Text towerInfo;
        Button lowHeatButton;
        Button upgradeButton;
        Button moveButton;
        Button removeButton;
        readonly Button[] toolButtons = new Button[4];
        readonly Text[] toolCosts = new Text[4];
        readonly Outline[] toolFrames = new Outline[4];
        Button speedButton;
        Button layoutButton;
        Button waveButton;

        bool hasCell;
        Cell selectedCell;
        int selectedTowerId = -1;
        bool placing;
        ToolType placingType;
        bool moveMode;

        float bannerTime;
        float textTimer;

        public NightScreen(GameController ctl, RectTransform parent)
        {
            this.ctl = ctl;
            Root = UIKit.Node("NightScreen", parent);
            UIKit.Stretch(Root);

            // 보드 탭 판(가장 아래). 위/아래 바가 이 위를 덮는다.
            var input = UIKit.Image(Root, "BoardInput", new Color(0f, 0f, 0f, 0f));
            UIKit.Stretch(input.rectTransform);
            input.gameObject.AddComponent<BoardInput>().Tapped = OnBoardTap;

            BuildTop();
            BuildBottom();

            banner = UIKit.FitLabel(Root, "Banner", "", 64, Theme.HudText, TextAnchor.MiddleCenter);
            UIKit.Top(banner.rectTransform, 100f, TopHeight + 24f, 40f, 40f);
            var shadow = banner.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(3f, -3f);
        }

        void BuildTop()
        {
            var top = UIKit.Image(Root, "TopBar", Theme.HudBg);
            UIKit.Top(top.rectTransform, TopHeight);

            title = UIKit.FitLabel(top.transform, "Title", "", 46, Theme.HudText, TextAnchor.MiddleLeft);
            UIKit.Top(title.rectTransform, 76f, 6f, 32f, 170f);

            var pause = UIKit.Button(top.transform, "Pause", "II", 44, Theme.HudPanel, Theme.HudText, ctl.ShowPause);
            var prt = (RectTransform)pause.transform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.sizeDelta = new Vector2(120f, 70f);
            prt.anchoredPosition = new Vector2(-24f, -10f);

            stats = UIKit.FitLabel(top.transform, "Stats", "", 40, Theme.HudText, TextAnchor.MiddleLeft);
            UIKit.Top(stats.rectTransform, 60f, 84f, 32f, 32f);

            var goalBg = UIKit.Panel(top.transform, "GoalBox", Theme.HudPanel, 1.6f);
            UIKit.Top(goalBg.rectTransform, 96f, 148f, 20f, 20f);
            goal = UIKit.Label(goalBg.transform, "Goal", "", 32, Theme.HudText, TextAnchor.MiddleLeft);
            goal.resizeTextForBestFit = true;
            goal.resizeTextMinSize = 20;
            goal.resizeTextMaxSize = 32;
            goal.verticalOverflow = VerticalWrapMode.Truncate;
            UIKit.Stretch(goal.rectTransform, 20f, 4f, 20f, 4f);
        }

        void BuildBottom()
        {
            var bottom = UIKit.Image(Root, "BottomBar", Theme.HudBg);
            UIKit.Bottom(bottom.rectTransform, BottomHeight);
            var b = bottom.transform;

            // 상황 설명 / 선택한 기구 조작
            var ctxBg = UIKit.Panel(b, "Context", Theme.HudPanel, 1.6f);
            UIKit.Bottom(ctxBg.rectTransform, 136f, 352f, 20f, 20f);
            context = UIKit.Label(ctxBg.transform, "Text", "", 32, Theme.HudText, TextAnchor.MiddleLeft);
            context.resizeTextForBestFit = true;
            context.resizeTextMinSize = 20;
            context.resizeTextMaxSize = 32;
            context.verticalOverflow = VerticalWrapMode.Truncate;
            UIKit.Stretch(context.rectTransform, 22f, 6f, 22f, 6f);

            towerPanel = UIKit.Node("TowerPanel", ctxBg.transform);
            UIKit.Stretch(towerPanel, 12f, 8f, 12f, 8f);
            towerInfo = UIKit.FitLabel(towerPanel, "Info", "", 30, Theme.HudText, TextAnchor.MiddleLeft);
            UIKit.Top(towerInfo.rectTransform, 44f, 0f, 10f, 10f);
            var row = UIKit.Node("Buttons", towerPanel);
            UIKit.Bottom(row, 70f, 0f);
            UIKit.HStack(row.gameObject, 12f, 0, 0);
            lowHeatButton = UIKit.Button(row, "LowHeat", "약불", 30, Theme.Blue, Color.white, OnLowHeat);
            upgradeButton = UIKit.Button(row, "Upgrade", "강화", 30, Theme.Accent, Color.white, OnUpgrade);
            moveButton = UIKit.Button(row, "Move", "이동", 30, Theme.Hex(0x6D5A4E), Color.white, OnMove);
            removeButton = UIKit.Button(row, "Remove", "회수", 30, Theme.Hex(0x8D3B30), Color.white, OnRemove);
            towerPanel.gameObject.SetActive(false);

            // 기구 선택 바
            var tools = UIKit.Node("Tools", b);
            UIKit.Bottom(tools, 190f, 152f, 20f, 20f);
            UIKit.HStack(tools.gameObject, 14f, 0, 0);
            for (int i = 0; i < toolButtons.Length; i++)
            {
                var type = (ToolType)i;
                var def = GameData.Tool(type);
                var btn = UIKit.Button(tools, "Tool " + type, null, 30, Theme.HudPanel, Theme.HudText, () => OnToolButton(type));
                var bt = btn.transform;
                // 고른 기구는 노란 테두리(Outline)로 표시한다. 글자를 가리지 않는다.
                var frame = btn.gameObject.AddComponent<Outline>();
                frame.effectColor = Theme.Selection;
                frame.effectDistance = new Vector2(6f, -6f);
                frame.enabled = false;
                toolFrames[i] = frame;

                var iconImg = UIKit.Image(bt, "Icon", Theme.ToolColor(type), type == ToolType.Cauldron ? SpriteFactory.Circle : SpriteFactory.UiRounded, 2.5f);
                var irt = iconImg.rectTransform;
                irt.anchorMin = new Vector2(0.5f, 1f);
                irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.sizeDelta = new Vector2(76f, 76f);
                irt.anchoredPosition = new Vector2(0f, -12f);
                iconImg.raycastTarget = false;
                var glyph = UIKit.Label(iconImg.transform, "Glyph", def.glyph, 44, Theme.Ink, TextAnchor.MiddleCenter);
                UIKit.Stretch(glyph.rectTransform);

                var name = UIKit.FitLabel(bt, "Name", def.name, 28, Theme.HudText, TextAnchor.MiddleCenter);
                UIKit.Bottom(name.rectTransform, 40f, 46f, 6f, 6f);
                var cost = UIKit.FitLabel(bt, "Cost", "화력 " + def.cost, 28, Theme.Fire, TextAnchor.MiddleCenter);
                UIKit.Bottom(cost.rectTransform, 38f, 8f, 6f, 6f);
                toolButtons[i] = btn;
                toolCosts[i] = cost;
            }

            // 속도 / 어제 배치 / 다음 웨이브
            var actions = UIKit.Node("Actions", b);
            UIKit.Bottom(actions, 118f, 20f, 20f, 20f);
            var h = UIKit.HStack(actions.gameObject, 14f, 0, 0);
            h.childForceExpandWidth = false;
            speedButton = UIKit.Button(actions, "Speed", "1배속", 36, Theme.HudPanel, Theme.HudText, ctl.ToggleSpeed);
            UIKit.Layout(speedButton, -1f, 190f, 0f);
            layoutButton = UIKit.Button(actions, "Layout", "어제 배치", 36, Theme.Hex(0x5C4A40), Theme.HudText, OnRestoreLayout);
            UIKit.Layout(layoutButton, -1f, 250f, 0f);
            waveButton = UIKit.Button(actions, "Wave", "웨이브 시작", 44, Theme.Accent, Color.white, OnWaveButton);
            UIKit.Layout(waveButton, -1f, 400f, 1f);
        }

        // ───────────── 상태 ─────────────

        GameSession Session { get { return ctl.Session; } }
        NightSimulation Sim { get { return ctl.Session != null ? ctl.Session.Night : null; } }

        public void Show(bool visible)
        {
            if (Root.gameObject.activeSelf != visible) Root.gameObject.SetActive(visible);
            if (visible) return;
            ResetInteraction();
            // 지난밤 배너가 다음 밤 첫 화면에 남지 않게 지운다.
            bannerTime = 0f;
            banner.text = "";
        }

        public void ResetInteraction()
        {
            hasCell = false;
            selectedTowerId = -1;
            placing = false;
            moveMode = false;
            if (ctl.Board != null)
            {
                ctl.Board.SetHighlights(null);
                ctl.Board.SetSelection(false, 0, 0);
                ctl.Board.HideRange();
            }
        }

        public int SelectedTowerId { get { return selectedTowerId; } }

        public void Banner(string text, Color color)
        {
            banner.text = text;
            banner.color = color;
            bannerTime = 1.6f;
        }

        void Toast(string text) { ctl.Toast(text); }

        // ───────────── 입력 ─────────────

        void OnBoardTap(Vector2 screenPos)
        {
            var sim = Sim;
            if (sim == null || ctl.Paused || sim.State.finished) return;
            var map = Session.Map;
            Vector3 w = ctl.Cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            Cell cell;
            if (!map.TryGetCell(new Vec2(w.x, w.y), out cell))
            {
                ResetInteraction();
                return;
            }
            var tower = sim.TowerAt(cell.col, cell.row);
            var selected = selectedTowerId >= 0 ? sim.TowerById(selectedTowerId) : null;

            if (moveMode && selected != null)
            {
                if (tower == selected)
                {
                    moveMode = false;
                    return;
                }
                var r = sim.CheckMove(selected, cell.col, cell.row);
                if (r == BuildResult.Ok)
                {
                    sim.Move(selected, cell.col, cell.row);
                    moveMode = false;
                    ctl.MarkDirty();
                }
                else Toast(Texts.BuildFailReason(r));
                return;
            }

            if (placing)
            {
                var r = sim.CheckBuild(placingType, cell.col, cell.row);
                if (r == BuildResult.Ok)
                {
                    Build(placingType, cell);
                    return;
                }
                if (tower != null)
                {
                    placing = false;
                    SelectTower(tower);
                    return;
                }
                Toast(Texts.BuildFailReason(r));
                return;
            }

            if (tower != null)
            {
                if (selectedTowerId == tower.id) ResetInteraction();
                else SelectTower(tower);
                return;
            }

            if (map.IsReserved(cell.col, cell.row))
            {
                ResetInteraction();
                Toast(cell == map.Entrance ? "뒷문: 몬스터가 들어오는 곳" : "창고: 몬스터가 닿으면 재료를 먹고 내구도가 1 깎여요");
                return;
            }

            selectedTowerId = -1;
            if (hasCell && selectedCell == cell)
            {
                hasCell = false;
                return;
            }
            hasCell = true;
            selectedCell = cell;
        }

        void SelectTower(TowerState t)
        {
            hasCell = false;
            placing = false;
            moveMode = false;
            selectedTowerId = t.id;
        }

        void OnToolButton(ToolType type)
        {
            var sim = Sim;
            if (sim == null || ctl.Paused) return;
            if (hasCell)
            {
                var r = sim.CheckBuild(type, selectedCell.col, selectedCell.row);
                if (r == BuildResult.Ok) Build(type, selectedCell);
                else Toast(Texts.BuildFailReason(r));
                return;
            }
            selectedTowerId = -1;
            moveMode = false;
            if (placing && placingType == type)
            {
                placing = false;
                return;
            }
            if (sim.State.firepower < GameData.Tool(type).cost)
            {
                Toast(Texts.BuildFailReason(BuildResult.NoFirepower));
                return;
            }
            placing = true;
            placingType = type;
        }

        void Build(ToolType type, Cell cell)
        {
            var t = Sim.Build(type, cell.col, cell.row);
            if (t == null) return;
            hasCell = false;
            placing = false;
            ctl.MarkDirty();
            ctl.Board.PunchTower(t.id);
            ctl.Fx.Pop(ctl.Board.CellPosition(cell.col, cell.row), 1.1f, Theme.ToolColor(type), 0.3f);
        }

        TowerState SelectedTower { get { return selectedTowerId >= 0 && Sim != null ? Sim.TowerById(selectedTowerId) : null; } }

        void OnLowHeat()
        {
            var t = SelectedTower;
            if (t == null) return;
            if (Sim.ToggleLowHeat(t))
            {
                Toast(t.lowHeat ? "약불: 피해 절반, 막타를 다음 기구에 넘겨요" : "약불 해제");
                ctl.MarkDirty();
            }
        }

        void OnUpgrade()
        {
            var t = SelectedTower;
            if (t == null) return;
            if (Sim.Upgrade(t))
            {
                ctl.Board.PunchTower(t.id);
                Toast("강화! (오늘 밤 동안 피해 +" + Mathf.RoundToInt(Balance.ToolUpgradeDamageBonus * 100f) + "%)");
                ctl.MarkDirty();
            }
            else if (Combat.UpgradeCost(t) < 0) Toast("이미 최대 강화예요");
            else Toast(Texts.BuildFailReason(BuildResult.NoFirepower));
        }

        void OnMove()
        {
            var t = SelectedTower;
            if (t == null) return;
            if (!Sim.IsPrep)
            {
                Toast(Texts.BuildFailReason(BuildResult.NotNow));
                return;
            }
            moveMode = !moveMode;
        }

        void OnRemove()
        {
            var t = SelectedTower;
            if (t == null) return;
            int refund = Sim.RefundValue(t);
            if (Sim.Remove(t))
            {
                Toast("회수: 화력 +" + refund);
                selectedTowerId = -1;
                moveMode = false;
                ctl.MarkDirty();
            }
        }

        void OnRestoreLayout()
        {
            var sim = Sim;
            if (sim == null || !sim.IsPrep) return;
            int placed = sim.RestoreLayout(Session.Run.lastLayout);
            Toast(placed > 0 ? "어제 배치 " + placed + "개를 되살렸어요" : "화력이 부족해 되살리지 못했어요");
            if (placed > 0) ctl.MarkDirty();
        }

        void OnWaveButton()
        {
            var sim = Sim;
            if (sim == null || ctl.Paused) return;
            if (sim.StartWave())
            {
                moveMode = false;
                ctl.SaveNow();
            }
        }

        // ───────────── 매 프레임 ─────────────

        public void Refresh(float dt)
        {
            var sim = Sim;
            if (sim == null) return;
            var run = Session.Run;
            var n = sim.State;

            if (selectedTowerId >= 0 && sim.TowerById(selectedTowerId) == null)
            {
                selectedTowerId = -1;
                moveMode = false;
            }
            if (moveMode && !sim.IsPrep) moveMode = false;
            if (hasCell && sim.TowerAt(selectedCell.col, selectedCell.row) != null) hasCell = false;

            bannerTime -= dt;
            if (bannerTime <= 0f) UIKit.SetText(banner, "");
            else
            {
                var c = banner.color;
                c.a = Mathf.Clamp01(bannerTime / 0.4f);
                banner.color = c;
            }

            UpdateBoardMarks(sim);
            UpdateButtons(sim, run);

            textTimer -= dt;
            if (textTimer > 0f) return;
            textTimer = 0.1f;
            UpdateTexts(sim, run, n);
        }

        void UpdateBoardMarks(NightSimulation sim)
        {
            var board = ctl.Board;
            var tower = SelectedTower;
            if (tower != null)
            {
                board.SetSelection(true, tower.col, tower.row);
                if (tower.type == ToolType.Cauldron) board.HideRange();
                else board.ShowRange(board.CellPosition(tower.col, tower.row), Combat.Range(tower), Theme.ToolColor(tower.type));
                if (moveMode) board.SetHighlights((c, r) => sim.CheckMove(tower, c, r) == BuildResult.Ok && !(c == tower.col && r == tower.row));
                else board.SetHighlights(null);
            }
            else if (placing)
            {
                board.SetSelection(false, 0, 0);
                board.HideRange();
                var type = placingType;
                board.SetHighlights((c, r) => sim.CheckBuild(type, c, r) == BuildResult.Ok);
            }
            else if (hasCell)
            {
                board.SetSelection(true, selectedCell.col, selectedCell.row);
                board.HideRange();
                board.SetHighlights(null);
            }
            else
            {
                board.SetSelection(false, 0, 0);
                board.HideRange();
                board.SetHighlights(null);
            }
        }

        void UpdateButtons(NightSimulation sim, RunState run)
        {
            var n = sim.State;
            for (int i = 0; i < toolButtons.Length; i++)
            {
                var type = (ToolType)i;
                var def = GameData.Tool(type);
                bool affordable = n.firepower >= def.cost;
                bool enabled;
                if (hasCell) enabled = sim.CheckBuild(type, selectedCell.col, selectedCell.row) == BuildResult.Ok;
                else enabled = affordable && !n.finished;
                if (toolButtons[i].interactable != enabled) toolButtons[i].interactable = enabled;
                var costColor = affordable ? Theme.Fire : Theme.Bad;
                if (toolCosts[i].color != costColor) toolCosts[i].color = costColor;
                bool framed = placing && placingType == type;
                if (toolFrames[i].enabled != framed) toolFrames[i].enabled = framed;
            }

            var tower = SelectedTower;
            bool panel = tower != null;
            if (towerPanel.gameObject.activeSelf != panel) towerPanel.gameObject.SetActive(panel);
            if (context.gameObject.activeSelf == panel) context.gameObject.SetActive(!panel);
            if (panel)
            {
                bool pot = tower.type == ToolType.Cauldron;
                lowHeatButton.interactable = !pot;
                UIKit.SetLabel(lowHeatButton, pot ? "약불 없음" : (tower.lowHeat ? "약불 끄기" : "약불 켜기"));
                int cost = Combat.UpgradeCost(tower);
                upgradeButton.interactable = cost > 0 && n.firepower >= cost;
                UIKit.SetLabel(upgradeButton, cost > 0 ? "강화 " + cost : "최대 강화");
                moveButton.interactable = sim.IsPrep;
                UIKit.SetLabel(moveButton, moveMode ? "이동 취소" : "이동");
                UIKit.SetLabel(removeButton, "회수 +" + sim.RefundValue(tower));
            }

            bool prep = sim.IsPrep && sim.HasNextWave;
            waveButton.interactable = prep;
            bool canRestore = prep && n.towers.Count == 0 && run.lastLayout.Count > 0;
            if (layoutButton.gameObject.activeSelf != canRestore) layoutButton.gameObject.SetActive(canRestore);
            UIKit.SetLabel(speedButton, ctl.Speed > 1 ? "2배속" : "1배속");
        }

        void UpdateTexts(NightSimulation sim, RunState run, NightState n)
        {
            int total = n.waves.Count;
            string t;
            if (n.waveActive) t = n.index + "일 밤  |  웨이브 " + (n.waveIndex + 1) + "/" + total;
            else if (n.finished) t = n.index + "일 밤  |  방어 완료!";
            else t = n.index + "일 밤  |  준비 시간 (웨이브 " + (n.waveIndex + 1) + "/" + total + ")";
            UIKit.SetText(title, t);

            UIKit.SetText(stats,
                Theme.Colorize(Texts.Heart + " " + run.durability + "/" + Balance.MaxDurability, Theme.Heart) + "     " +
                Theme.Colorize("화력 " + n.firepower, Theme.Fire) + "     " +
                Theme.Colorize("골드 " + run.gold, Theme.Gold) + "     " +
                Theme.Colorize(Texts.Star + " " + run.reputation, Theme.Star));

            var res = Session.UpcomingReservation;
            string g;
            if (res != null)
            {
                g = Theme.Colorize("내일 예약", Theme.Fire) + "  " + res.party + " | " + Texts.RecipeName(res.recipeId) + " x" + res.servings
                    + "\n" + Texts.NeedsProgress(ctl.Needs);
            }
            else g = Theme.Colorize("내일 예약 없음", Theme.HudSoft) + "\n마음껏 재료를 모으세요. 막타 친 기구의 조리법이 재료가 됩니다.";
            UIKit.SetText(goal, g);

            if (SelectedTower != null)
            {
                var tw = SelectedTower;
                var def = GameData.Tool(tw.type);
                var sb = new StringBuilder();
                sb.Append(def.name).Append(" Lv").Append(tw.level + 1).Append("  |  ").Append(GameData.MethodName(def.method));
                sb.Append("  |  처치 ").Append(tw.kills);
                if (tw.lowHeat) sb.Append("  |  ").Append(Theme.Colorize("약불", Theme.Hex(0x90CAF9)));
                if (sim.TowerRate(tw) < 1f) sb.Append("  |  ").Append(Theme.Colorize("포자: 느려짐", Theme.Hex(0xCE93D8)));
                UIKit.SetText(towerInfo, sb.ToString());
                return;
            }

            string c;
            if (moveMode) c = "옮길 칸을 탭하세요. (기구를 다시 탭하면 취소)";
            else if (placing)
            {
                var def = GameData.Tool(placingType);
                c = def.name + ": 초록 칸을 탭해 설치 (화력 " + def.cost + ")\n" + def.description + ". 다시 누르면 취소";
            }
            else if (hasCell)
            {
                bool path = Session.Map.IsPath(selectedCell.col, selectedCell.row);
                c = "칸을 골랐어요. 아래에서 기구를 고르세요.\n" + (path ? "경로 위: 가마솥만 놓을 수 있어요" : "경로 옆: 식칼 거치대, 그릴, 냉동고");
            }
            else if (n.finished) c = "모든 웨이브를 막았습니다!";
            else if (n.waveActive)
            {
                int left = sim.AliveCount + sim.PendingSpawns;
                c = "남은 몬스터 " + left + "\n몬스터 옆 아이콘 = 지금 막타를 칠 기구. 기구를 탭하면 약불/강화";
            }
            else if (n.index == 0 && n.towers.Count == 0)
                c = "경로 옆 빈 칸을 탭하고 아래에서 기구를 고르세요.\n몬스터에게 마지막 피해를 준 기구가 재료를 정합니다!";
            else
                c = "다음: " + Texts.WavePreview(sim.NextWave) + "\n준비 시간에는 이동과 회수가 무료예요.";
            UIKit.SetText(context, c);
        }
    }
}
