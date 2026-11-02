using System;
using System.Collections.Generic;
using System.Text;

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;

namespace NamelessGod
{
    public sealed class FirstStageView : MonoBehaviour
    {
        private enum SoundCue
        {
            Wisdom,
            Destruction,
            Prosperity,
            Common,
            PrayerResolved,
            FaithWarning,
            EventChoice,
            Flood,
            Ascension,
            Count
        }

        private const int AUDIO_SAMPLE_RATE = 22050;
        private const float AUDIO_VOLUME = 0.32f;

        private FirstStageRules _rules;
        private TMP_FontAsset _font;
        private Sprite _cardSprite;
        private AudioSource _audioSource;
        private AudioClip[] _soundCues;
        private bool _soundEnabled = true;
        private RectTransform _content;
        private GameObject _tooltip;
        private TextMeshProUGUI _tooltipText;
        private int _seed;
        private int _selectedCard = -1;
        private int _candidate = -1;
        private int _dragCard = -1;
        private bool _started;
        private bool _exchanging;
        private int _pendingTarget = -1;
        private int _eventChoice = -1;
        private bool _awaitingSecondaryTarget;
        private bool _secondaryTargetOptional;
        private bool _discardingReorganize;
        private bool _showingResolutionPopup;
        private string _resolutionPopupText;
        private readonly HashSet<int> _bossRemovalSelection = new HashSet<int>();
        private readonly List<int> _exchangeIndices = new List<int>();
        private string _message = "";

        internal bool CanDragCard => !_exchanging && !_awaitingSecondaryTarget && !_discardingReorganize && _pendingTarget < 0 && !_rules.NeedsReorganizeDiscard && !_showingResolutionPopup && FindPendingInterpretationIndex() < 0;

        void Awake()
        {
            Font sourceFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
            _font = TMP_FontAsset.CreateFontAsset(sourceFont, 40, 5, GlyphRenderMode.SDFAA, 2048, 2048);
            _font.isMultiAtlasTexturesEnabled = true;
            _cardSprite = CreateCardSprite();
            var canvasObject = new GameObject("First Stage Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            _content = canvasObject.GetComponent<RectTransform>();
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            _seed = Environment.TickCount;
            _rules = new FirstStageRules(_seed);
            InitializeAudio();
            Refresh();
        }

        void OnDestroy()
        {
            if (_font != null) Destroy(_font);
            if (_cardSprite != null) { Destroy(_cardSprite.texture); Destroy(_cardSprite); }
            if (_soundCues != null)
                foreach (AudioClip clip in _soundCues)
                    if (clip != null) Destroy(clip);
        }

        /// <summary>계열별 음향 피드백을 재생할 AudioSource와 기본 음색을 만든다.</summary>
        private void InitializeAudio()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0;
            _audioSource.volume = 0.5f;
            _soundCues = new AudioClip[(int)SoundCue.Count];
            _soundCues[(int)SoundCue.Wisdom] = CreateToneClip("Wisdom Cue", 523.25f, 659.25f, 784f, 0.24f);
            _soundCues[(int)SoundCue.Destruction] = CreateToneClip("Destruction Cue", 196f, 146.83f, 0, 0.18f);
            _soundCues[(int)SoundCue.Prosperity] = CreateToneClip("Prosperity Cue", 392f, 493.88f, 587.33f, 0.28f);
            _soundCues[(int)SoundCue.Common] = CreateToneClip("Common Cue", 329.63f, 440f, 0, 0.16f);
            _soundCues[(int)SoundCue.PrayerResolved] = CreateToneClip("Prayer Resolved Cue", 523.25f, 659.25f, 783.99f, 0.36f);
            _soundCues[(int)SoundCue.FaithWarning] = CreateToneClip("Faith Warning Cue", 246.94f, 185f, 0, 0.28f);
            _soundCues[(int)SoundCue.EventChoice] = CreateToneClip("Event Choice Cue", 440f, 554.37f, 0, 0.22f);
            _soundCues[(int)SoundCue.Flood] = CreateToneClip("Flood Cue", 110f, 82.41f, 55f, 0.48f);
            _soundCues[(int)SoundCue.Ascension] = CreateToneClip("Ascension Cue", 523.25f, 659.25f, 783.99f, 0.68f);
        }

        /// <summary>지정한 주파수와 길이로 짧은 합성 음향 클립을 만든다.</summary>
        private AudioClip CreateToneClip(string clipName, float firstFrequency, float secondFrequency, float thirdFrequency, float duration)
        {
            int sampleCount = Mathf.CeilToInt(AUDIO_SAMPLE_RATE * duration);
            int frequencyCount = (firstFrequency > 0 ? 1 : 0) + (secondFrequency > 0 ? 1 : 0) + (thirdFrequency > 0 ? 1 : 0);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)(sampleCount - 1);
                float time = i / (float)AUDIO_SAMPLE_RATE;
                float tone = Mathf.Sin(2 * Mathf.PI * firstFrequency * time);
                if (secondFrequency > 0) tone += Mathf.Sin(2 * Mathf.PI * secondFrequency * time);
                if (thirdFrequency > 0) tone += Mathf.Sin(2 * Mathf.PI * thirdFrequency * time);
                float envelope = Mathf.Sin(Mathf.PI * progress);
                samples[i] = tone / frequencyCount * envelope * AUDIO_VOLUME;
            }
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, AUDIO_SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>효과음이 켜져 있을 때 지정한 UI·게임 상태 음향을 재생한다.</summary>
        private void PlayCue(SoundCue cue)
        {
            if (_soundEnabled && _audioSource != null && _soundCues != null)
                _audioSource.PlayOneShot(_soundCues[(int)cue]);
        }

        /// <summary>계열 번호에 해당하는 권능 카드 음향을 반환한다.</summary>
        private SoundCue GetFamilyCue(int family)
        {
            switch (family)
            {
                case 0: return SoundCue.Wisdom;
                case 1: return SoundCue.Destruction;
                case 2: return SoundCue.Prosperity;
                default: return SoundCue.Common;
            }
        }

        /// <summary>현재 해결된 기도 수를 계산해 카드·턴 처리 전후를 비교한다.</summary>
        private int CountResolvedPrayers()
        {
            int count = 0;
            foreach (FirstStagePrayer prayer in _rules.Prayers)
                if (prayer.IsResolved) count++;
            return count;
        }

        /// <summary>신앙이 5 이하로 내려가는 순간 경고 음향을 재생한다.</summary>
        private void PlayFaithWarning(int previousFaith)
        {
            if (previousFaith > 5 && _rules.Faith <= 5)
                PlayCue(SoundCue.FaithWarning);
        }

        /// <summary>현재 화면에서 음향을 켜거나 끈다.</summary>
        private void ToggleSound()
        {
            _soundEnabled = !_soundEnabled;
            if (_soundEnabled) PlayCue(SoundCue.Common);
            Refresh();
        }

        /// <summary>현재 단계에 맞는 화면을 다시 그린다.</summary>
        private void Refresh()
        {
            ApplyUniqueInterpretationIfNeeded();
            foreach (Transform child in _content) Destroy(child.gameObject);
            Panel("Background", 0, 0, 1, 1, new Color(0.055f, 0.075f, 0.11f));
            if (!_started) ShowStart();
            else if (_rules.Phase == FirstStagePhase.Selection) ShowSelection();
            else if (_rules.Phase == FirstStagePhase.Battle) ShowBattle();
            else if (_rules.Phase == FirstStagePhase.Reward) ShowReward();
            else if (_rules.Phase == FirstStagePhase.Event) ShowEvent();
            else if (_rules.Phase == FirstStagePhase.EventResult) ShowEventResult();
            else if (_rules.Phase == FirstStagePhase.BossPreparation) ShowBossPreparation();
            else if (_rules.Phase == FirstStagePhase.BossBattle) ShowBattle();
            else ShowResult();
            AddSoundToggle();
        }

        /// <summary>런 시작과 홍수 예고 및 시드 선택을 표시한다.</summary>
        private void ShowStart()
        {
            Label("아직 이름 없는 신", 0.12f, 0.72f, 0.88f, 0.89f, 42, TextAlignmentOptions.Center);
            Label("마을은 언젠가 강이 넘칠 것을 두려워합니다.\n주민의 기도에 응답하고, 다가올 대홍수에 대비하세요.\n\n신앙이 0이 되면 잊힙니다. 고난은 각 기도의 해결 목표입니다.", 0.15f, 0.38f, 0.85f, 0.70f, 24, TextAlignmentOptions.Center);
            Button("새 시드로 시작", 0.27f, 0.22f, 0.49f, 0.32f, () => StartRun(Environment.TickCount));
            Button("같은 시드로 시작", 0.51f, 0.22f, 0.73f, 0.32f, () => StartRun(_seed));
            Label("시드: " + _seed, 0.2f, 0.12f, 0.8f, 0.18f, 16, TextAlignmentOptions.Center);
        }

        /// <summary>지정한 seed로 새 S1 상태를 만들고 기도 선택 화면을 연다.</summary>
        private void StartRun(int seed)
        {
            _seed = seed;
            _rules = new FirstStageRules(seed);
            _started = true;
            _candidate = -1;
            _selectedCard = -1;
            _exchanging = false;
            _pendingTarget = -1;
            _eventChoice = -1;
            _awaitingSecondaryTarget = false;
            _secondaryTargetOptional = false;
            _discardingReorganize = false;
            _showingResolutionPopup = false;
            _resolutionPopupText = null;
            _bossRemovalSelection.Clear();
            _dragCard = -1;
            _exchangeIndices.Clear();
            _message = "";
            Refresh();
        }

        /// <summary>중대 기도 후보와 미선택 결과, 두 작은 기도를 보여준다.</summary>
        private void ShowSelection()
        {
            Label("기도를 고르세요  ·  " + _rules.Stage + " / 6", 0.04f, 0.89f, 0.78f, 0.97f, 30, TextAlignmentOptions.Center);
            for (int i = 0; i < 3; i++)
            {
                int choice = i;
                float x = 0.04f + i * 0.31f;
                PrayerData data = PrayerData.Find(_rules.MajorCandidates[i]);
                string pattern = _rules.Stage == 1 ? "안정 (학습)" : data.Pattern == "악화" ? "악화: 3턴부터 손실 +1" : data.Pattern == "기회" ? "기회: 짝수 턴 첫 직접 감소 +2" : "안정";
                string ignored = _rules.WasIgnored(data.Id) ? "이미 적용됨 · 반복 없음" : data.IgnoreText;
                string text = (_candidate == i ? "▶ " : "") + data.Name + "\n고난 " + _rules.StageHardship + " · 손실 " + _rules.StageLoss + "\n" + pattern + "\n\n미선택 영향\n" + ignored + "\n" + _rules.DescribeIgnoredStatChanges(data.Id) + "\n\n지혜  " + _rules.DescribeStatChanges(data.Id, Interpretation.Wisdom) + "\n파괴  " + _rules.DescribeStatChanges(data.Id, Interpretation.Destruction) + "\n풍요  " + _rules.DescribeStatChanges(data.Id, Interpretation.Prosperity);
                RectTransform prayer = Button(text, x, 0.43f, x + 0.29f, 0.86f, () => { _candidate = choice; Refresh(); }, 13);
                prayer.GetComponent<UnityEngine.UI.Image>().sprite = _cardSprite;
                prayer.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
                prayer.GetComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.19f, 0.24f);
                AddOutline(prayer, new Color(0.69f, 0.57f, 0.36f));
            }
            PrayerData smallA = PrayerData.Find(_rules.SmallCandidates[0]);
            PrayerData smallB = PrayerData.Find(_rules.SmallCandidates[1]);
            Label("작은 기도  ·  " + smallA.Name + "   /   " + smallB.Name + "\n미해결이면 턴 종료 때 신앙을 잃습니다.", 0.08f, 0.36f, 0.92f, 0.42f, 17, TextAlignmentOptions.Center);
            DrawVillageStats(0.27f, 0.32f);
            if (_candidate >= 0) Button("이 기도에 응답하기", 0.34f, 0.13f, 0.66f, 0.22f, ConfirmMajor);
        }

        /// <summary>선택한 중대 기도를 확정하고 전투를 시작한다.</summary>
        private void ConfirmMajor()
        {
            if (_candidate < 0 || !_rules.SelectMajor(_rules.MajorCandidates[_candidate])) return;
            _message = "선택하지 않은 두 기도의 결과가 기록되었습니다.";
            Refresh();
        }

        /// <summary>진행 자원, 마을 지표, 기도와 손패를 게임 HUD로 배치한다.</summary>
        private void ShowBattle()
        {
            bool isBossBattle = _rules.Phase == FirstStagePhase.BossBattle;
            Label((isBossBattle ? "대홍수" : "제 " + _rules.Stage + "번째 기도") + "     ·     " + _rules.Turn + "턴", 0.03f, 0.95f, 0.68f, 0.99f, 25, TextAlignmentOptions.Left);
            AddProgressBar("신앙", _rules.Faith, 30, 0.03f, 0.90f, 0.34f, 0.94f, new Color(0.59f, 0.71f, 0.93f), true);
            DrawPowerMeter(0.38f, 0.90f, 0.64f, 0.94f);
            Label("위안 " + _rules.Comfort, 0.67f, 0.90f, 0.78f, 0.94f, 15, TextAlignmentOptions.Right);
            if (_rules.Grace > 0) DrawGraceMeter(0.80f, 0.90f, 0.96f, 0.94f);
            DrawVillageStats(0.80f, 0.84f);
            if (isBossBattle)
            {
                AddProgressBar("수위", _rules.BossHeight, 6, 0.03f, 0.75f, 0.25f, 0.79f, new Color(0.31f, 0.67f, 0.84f), true);
                Label("예상 " + _rules.PredictedBossHeight + "  ·  " + _rules.CurrentWeather + "  →  " + _rules.NextWeather + "  ·  범람 " + _rules.BossFloodCount + "회", 0.27f, 0.75f, 0.70f, 0.79f, 14, TextAlignmentOptions.Center);
                Label(_rules.BossPerkSummary + (string.IsNullOrEmpty(_rules.WaterChannelForecast) ? "" : "  ·  " + _rules.WaterChannelForecast), 0.03f, 0.70f, 0.96f, 0.74f, 13, TextAlignmentOptions.Center);
                Label(_message, 0.03f, 0.665f, 0.96f, 0.70f, 14, TextAlignmentOptions.Center);
            }
            else Label(_message, 0.03f, 0.70f, 0.96f, 0.75f, 15, TextAlignmentOptions.Center);
            if (_showingResolutionPopup)
            {
                ShowResolutionPopup();
                return;
            }
            int pendingInterpretation = FindPendingInterpretationIndex();
            if (pendingInterpretation >= 0)
            {
                ShowPrayerInterpretationChoice(pendingInterpretation);
                return;
            }
            for (int i = 0; i < _rules.Prayers.Count; i++)
            {
                int target = i;
                FirstStagePrayer prayer = _rules.Prayers[i];
                float width = _rules.Prayers.Count == 1 ? 0.40f : 0.29f;
                float x = _rules.Prayers.Count == 1 ? 0.30f : 0.04f + i * 0.31f;
                int maximum = prayer.Id == "BOSS" ? 60 : prayer.IsMajor ? _rules.StageHardship : PrayerData.Find(prayer.Id).Hardship;
                string status = prayer.IsResolved ? "해결 완료 · 남은 고난 0 / " + maximum : "남은 고난 " + prayer.Hardship + " / " + maximum + " · 턴 손실 " + _rules.TurnLoss(prayer);
                if (prayer.Solution > 0 && !prayer.IsResolved) status += " · 해법 " + prayer.Solution;
                if (prayer.HasPromise && !prayer.IsResolved) status += " · 약속";
                if (prayer.HasRift && !prayer.IsResolved) status += " · 균열 +3";
                string title = (isBossBattle ? "대홍수 기도" : prayer.IsMajor ? "중대 기도" : "작은 기도") + "\n" + prayer.Name + "\n\n" + status;
                RectTransform prayerCard = Button(title, x, 0.46f, x + width, 0.69f, () => SelectTarget(target), 16);
                prayerCard.GetComponent<UnityEngine.UI.Image>().sprite = _cardSprite;
                prayerCard.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
                prayerCard.GetComponent<UnityEngine.UI.Image>().color = prayer.IsResolved ? new Color(0.12f, 0.25f, 0.23f) : new Color(0.18f, 0.21f, 0.25f);
                AddOutline(prayerCard, prayer.IsResolved ? new Color(0.49f, 0.78f, 0.62f) : new Color(0.69f, 0.57f, 0.36f));
                AddProgressBar("", prayer.Hardship, maximum, x + 0.025f, 0.465f, x + width - 0.025f, 0.482f, prayer.IsResolved ? new Color(0.49f, 0.78f, 0.62f) : new Color(0.79f, 0.55f, 0.32f), false);
                DrawPrayerContribution(prayer, x + 0.02f, 0.49f, width - 0.04f);
                prayerCard.gameObject.AddComponent<FirstStagePointer>().Configure(this, target, false);
            }
            Label("권능 카드", 0.04f, 0.39f, 0.25f, 0.43f, 17, TextAlignmentOptions.Left);
            Label("카드를 기도에 끌어 놓거나 선택하세요", 0.25f, 0.39f, 0.78f, 0.43f, 14, TextAlignmentOptions.Center);
            int count = _rules.Hand.Count;
            float cardWidth = 0.145f;
            float totalWidth = count <= 1 ? cardWidth : Mathf.Min(0.88f, cardWidth + (count - 1) * 0.15f);
            float step = count <= 1 ? 0 : (totalWidth - cardWidth) / (count - 1);
            float firstX = (1f - totalWidth) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int cardIndex = i;
                float x = firstX + i * step;
                string mark = _selectedCard == i ? "▶ " : (_exchangeIndices.Contains(i) ? "선택 " : "");
                RectTransform cardRect = Button("", x, 0.075f, x + cardWidth, 0.37f, () => SelectCard(cardIndex), 13);
                StyleCard(cardRect, _rules.Hand[i], mark);
                cardRect.gameObject.AddComponent<FirstStagePointer>().Configure(this, cardIndex, true);
            }
            Label("뽑기 " + _rules.DrawCount + "  ·  버림 " + _rules.DiscardCount + "  ·  소진 " + _rules.ExhaustedCount, 0.04f, 0.35f, 0.55f, 0.38f, 13, TextAlignmentOptions.Left);
            if (_discardingReorganize)
            {
                Label("재정비 · 버릴 손패 한 장을 누르세요", 0.04f, 0.015f, 0.70f, 0.065f, 15, TextAlignmentOptions.Left);
                if (_rules.Hand.Count == 0) Button("건너뛰기", 0.78f, 0.01f, 0.95f, 0.07f, () => CompleteReorganize(-1), 15);
            }
            else if (_awaitingSecondaryTarget)
            {
                Label(_rules.Hand[_selectedCard].Kind == StartingCard.D01 ? "붕괴의 연쇄 · 초과량을 전달할 다른 기도를 선택하세요" : "나눔 · 두 번째 기도를 고르거나 한 대상만 처리하세요", 0.04f, 0.015f, 0.70f, 0.065f, 14, TextAlignmentOptions.Left);
                if (_secondaryTargetOptional) Button("한 대상만", 0.78f, 0.01f, 0.95f, 0.07f, () => CompleteSecondaryTarget(-1), 14);
            }
            else if (_pendingTarget >= 0 && _selectedCard >= 0 && (_rules.Hand[_selectedCard].Kind == StartingCard.Harvest || _rules.Hand[_selectedCard].Kind == StartingCard.P07))
            {
                bool isHarvest = _rules.Hand[_selectedCard].Kind == StartingCard.Harvest;
                int maximum = Math.Min(isHarvest ? 3 : 6, _rules.Grace);
                Label("소비할 은총을 선택하세요", 0.04f, 0.015f, 0.36f, 0.065f, 14, TextAlignmentOptions.Left);
                for (int i = 0; i <= maximum; i++)
                {
                    int spent = i;
                    float x = 0.38f + i * 0.062f;
                    Button(i.ToString(), x, 0.01f, x + 0.054f, 0.07f, () => ConfirmTargetCard(spent), 13);
                }
                Button("취소", 0.86f, 0.01f, 0.96f, 0.07f, CancelCardChoice, 14);
            }
            else if (_selectedCard >= 0 && _rules.Hand[_selectedCard].Kind == StartingCard.P06)
            {
                Label("풍요의 기적 · 모든 기도에 적용", 0.04f, 0.015f, 0.43f, 0.065f, 14, TextAlignmentOptions.Left);
                Button("3 감소", 0.47f, 0.01f, 0.60f, 0.07f, () => ConfirmGlobalCard(0), 14);
                if (_rules.Grace >= 3) Button("은총 3 · 6 감소", 0.62f, 0.01f, 0.81f, 0.07f, () => ConfirmGlobalCard(3), 13);
                Button("취소", 0.86f, 0.01f, 0.96f, 0.07f, CancelCardChoice, 14);
            }
            else if (_exchanging)
            {
                string instruction = _rules.Hand[_selectedCard].Kind == StartingCard.Consideration ? "버릴 카드 " + _exchangeIndices.Count + "장 · 같은 수만큼 드로우" : "버릴 카드 " + _exchangeIndices.Count + "/1 · 은총 +3";
                Label(instruction, 0.04f, 0.015f, 0.52f, 0.065f, 14, TextAlignmentOptions.Left);
                if (_rules.Hand[_selectedCard].Kind == StartingCard.Consideration || _exchangeIndices.Count == 1)
                    Button("선택 확정", 0.58f, 0.01f, 0.78f, 0.07f, ConfirmExchange, 14);
                else Label("버릴 카드를 한 장 고르세요", 0.58f, 0.01f, 0.80f, 0.07f, 13, TextAlignmentOptions.Center);
                Button("취소", 0.80f, 0.01f, 0.96f, 0.07f, CancelCardChoice, 14);
            }
            else
            {
                int loss = _rules.PredictLoss();
                bool majorWillResolve = !_rules.Prayers[0].IsResolved && _rules.Prayers[0].Solution >= _rules.Prayers[0].Hardship;
                string prediction = majorWillResolve ? "중대 기도 해결 예정 · 손실 없음" : "턴 종료 시 신앙 손실 " + loss;
                Label(prediction, 0.04f, 0.015f, 0.68f, 0.065f, 14, TextAlignmentOptions.Left);
                if (isBossBattle)
                {
                    if (_rules.CanUseWaterChannel && _rules.Power >= _rules.WaterChannelCost)
                        Button("물길 돌리기", 0.68f, 0.01f, 0.83f, 0.07f, UseWaterChannel, 13);
                    Button("턴 종료", 0.84f, 0.01f, 0.96f, 0.07f, EndTurn, 15);
                }
                else Button("턴 종료", 0.75f, 0.01f, 0.96f, 0.07f, EndTurn, 15);
            }
            CreateTooltip();
        }

        /// <summary>해석을 선택해야 하는 첫 번째 해결 기도의 인덱스를 반환한다.</summary>
        private int FindPendingInterpretationIndex()
        {
            for (int i = 0; i < _rules.Prayers.Count; i++)
                if (_rules.Prayers[i].IsResolved && !_rules.IsInterpreted(i)) return i;
            return -1;
        }

        /// <summary>최다 기여 계열이 하나인 해결 기도는 자동 응답하고 결과 팝업을 연다.</summary>
        private void ApplyUniqueInterpretationIfNeeded()
        {
            if (!_started || _showingResolutionPopup || (_rules.Phase != FirstStagePhase.Battle && _rules.Phase != FirstStagePhase.BossBattle && _rules.Phase != FirstStagePhase.Resolved && _rules.Phase != FirstStagePhase.Defeated && _rules.Phase != FirstStagePhase.BossResolved)) return;
            int prayerIndex = FindPendingInterpretationIndex();
            if (prayerIndex < 0 || !_rules.TryGetUniqueInterpretation(prayerIndex, out Interpretation interpretation)) return;
            SetInterpretationResult(prayerIndex, interpretation);
        }

        /// <summary>해결 기도의 가능한 응답 방식과 각 마을 지표 변화를 선택창에 표시한다.</summary>
        private void ShowPrayerInterpretationChoice(int prayerIndex)
        {
            FirstStagePrayer prayer = _rules.Prayers[prayerIndex];
            RectTransform panel = Panel("Prayer Interpretation", 0.10f, 0.18f, 0.90f, 0.70f, new Color(0.055f, 0.07f, 0.10f, 0.98f));
            AddOutline(panel, new Color(0.78f, 0.64f, 0.38f));
            Label("기도에 응답했습니다", 0.16f, 0.61f, 0.84f, 0.67f, 26, TextAlignmentOptions.Center);
            Label(prayer.Name + "  ·  어떤 권능으로 마을을 바꾸겠습니까?", 0.16f, 0.55f, 0.84f, 0.61f, 17, TextAlignmentOptions.Center);
            string[] names = { "지혜", "파괴", "풍요" };
            int optionCount = 0;
            for (int i = 0; i < 3; i++) if (_rules.CanInterpret(prayerIndex, (Interpretation)i)) optionCount++;
            float width = optionCount == 1 ? 0.29f : 0.25f;
            float gap = optionCount == 1 ? 0 : 0.025f;
            float total = width * optionCount + gap * Math.Max(0, optionCount - 1);
            float x = (1 - total) * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                Interpretation interpretation = (Interpretation)i;
                if (!_rules.CanInterpret(prayerIndex, interpretation)) continue;
                float left = x;
                string changes = _rules.DescribeStatChanges(prayer.Id, interpretation);
                RectTransform choice = Button(names[i] + "\n\n" + changes, left, 0.31f, left + width, 0.51f, () => ChoosePrayerInterpretation(prayerIndex, interpretation), 17);
                choice.GetComponent<UnityEngine.UI.Image>().sprite = _cardSprite;
                choice.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
                choice.GetComponent<UnityEngine.UI.Image>().color = CardColorForInterpretation(interpretation);
                AddOutline(choice, CardAccentForInterpretation(interpretation));
                x += width + gap;
            }
            bool hasContribution = prayer.GetContribution(Interpretation.Wisdom) + prayer.GetContribution(Interpretation.Destruction) + prayer.GetContribution(Interpretation.Prosperity) > 0;
            Label(hasContribution ? "최다 기여가 동률인 계열 중 응답 방식을 선택하세요." : "계열별 고난 감소 기여가 없어 응답 방식을 선택하세요.", 0.16f, 0.23f, 0.84f, 0.29f, 14, TextAlignmentOptions.Center);
        }

        /// <summary>응답을 확정하고 결과와 실제 마을 지표 변화를 팝업에 담는다.</summary>
        private void ChoosePrayerInterpretation(int prayerIndex, Interpretation interpretation)
        {
            SetInterpretationResult(prayerIndex, interpretation);
            Refresh();
        }

        /// <summary>응답을 적용하고 서사 결과와 실제 마을 지표 변화를 팝업에 담는다.</summary>
        private void SetInterpretationResult(int prayerIndex, Interpretation interpretation)
        {
            FirstStagePrayer prayer = _rules.Prayers[prayerIndex];
            string outcome = FirstStageRules.DescribeOutcome(prayer.Id, interpretation);
            string changes = _rules.DescribeStatChanges(prayer.Id, interpretation);
            if (!_rules.ChooseInterpretation(prayerIndex, interpretation)) return;
            _resolutionPopupText = outcome + "\n\n마을의 변화\n" + changes;
            _showingResolutionPopup = true;
            PlayCue(GetFamilyCue((int)interpretation));
        }

        /// <summary>확정된 응답의 서사 결과와 마을 변화량을 확인 팝업에 표시한다.</summary>
        private void ShowResolutionPopup()
        {
            RectTransform panel = Panel("Prayer Result", 0.18f, 0.25f, 0.82f, 0.71f, new Color(0.045f, 0.055f, 0.08f, 0.99f));
            AddOutline(panel, new Color(0.78f, 0.64f, 0.38f));
            Label("신의 응답", 0.24f, 0.63f, 0.76f, 0.68f, 25, TextAlignmentOptions.Center);
            Label(_resolutionPopupText ?? "마을에 변화가 남았습니다.", 0.26f, 0.39f, 0.74f, 0.62f, 20, TextAlignmentOptions.Center);
            Button("계속", 0.40f, 0.29f, 0.60f, 0.37f, () => { _showingResolutionPopup = false; _resolutionPopupText = null; Refresh(); }, 17);
        }

        /// <summary>행사력을 사용 가능한 점 형태로 보여준다.</summary>
        private void DrawPowerMeter(float left, float bottom, float right, float top)
        {
            Label("행사력", left, bottom, left + 0.075f, top, 15, TextAlignmentOptions.Left);
            float pipWidth = 0.038f;
            for (int i = 0; i < 3; i++)
            {
                float x = left + 0.082f + i * 0.046f;
                RectTransform pip = Panel("Power Pip", x, bottom + 0.008f, x + pipWidth, top - 0.008f, i < _rules.Power ? new Color(0.83f, 0.67f, 0.33f) : new Color(0.20f, 0.23f, 0.28f));
                AddOutline(pip, new Color(0.49f, 0.42f, 0.30f));
            }
        }

        /// <summary>은총을 최대 여섯 칸의 점 표시로 보여준다.</summary>
        private void DrawGraceMeter(float left, float bottom, float right, float top)
        {
            Label("은총", left, bottom, left + 0.052f, top, 14, TextAlignmentOptions.Left);
            float pipWidth = 0.015f;
            for (int i = 0; i < 6; i++)
            {
                float x = left + 0.058f + i * 0.017f;
                Panel("Grace Pip", x, bottom + 0.010f, x + pipWidth, top - 0.010f, i < _rules.Grace ? new Color(0.57f, 0.81f, 0.66f) : new Color(0.20f, 0.23f, 0.28f));
            }
        }

        /// <summary>기도에 누적된 지혜·파괴·풍요 기여를 수치와 막대로 표시한다.</summary>
        private void DrawPrayerContribution(FirstStagePrayer prayer, float left, float bottom, float width)
        {
            Interpretation[] families = { Interpretation.Wisdom, Interpretation.Destruction, Interpretation.Prosperity };
            string[] names = { "지혜", "파괴", "풍요" };
            Color[] colors = { new Color(0.40f, 0.74f, 0.91f), new Color(0.91f, 0.48f, 0.39f), new Color(0.52f, 0.79f, 0.48f) };
            int total = prayer.GetContribution(Interpretation.Wisdom) + prayer.GetContribution(Interpretation.Destruction) + prayer.GetContribution(Interpretation.Prosperity);
            float gap = 0.006f;
            float columnWidth = (width - gap * 2) / 3;
            for (int i = 0; i < families.Length; i++)
            {
                int amount = prayer.GetContribution(families[i]);
                float x = left + i * (columnWidth + gap);
                Label(names[i] + " " + amount, x, bottom + 0.013f, x + columnWidth, bottom + 0.032f, 10, TextAlignmentOptions.Center);
                AddProgressBar("", amount, Math.Max(1, total), x, bottom, x + columnWidth, bottom + 0.010f, colors[i], false);
            }
        }

        /// <summary>다섯 마을 지표를 이름표와 진행 막대로 배치한다.</summary>
        private void DrawVillageStats(float bottom, float top)
        {
            float gap = 0.012f;
            int statCount = Enum.GetValues(typeof(VillageStat)).Length;
            float width = (0.94f - gap * (statCount - 1)) / statCount;
            for (int i = 0; i < statCount; i++)
            {
                VillageStat stat = (VillageStat)i;
                float left = 0.03f + i * (width + gap);
                Label(FirstStageRules.VillageStatName(stat), left, top - 0.022f, left + width, top, 12, TextAlignmentOptions.Center);
                AddProgressBar("", _rules.GetVillageStat(stat), FirstStageRules.VillageStatMaximum, left, bottom, left + width, top - 0.026f, GetStatColor(stat), false);
            }
        }

        /// <summary>라벨과 색상 채움으로 현재 수치를 진행 바로 표시한다.</summary>
        private void AddProgressBar(string label, int value, int maximum, float left, float bottom, float right, float top, Color fillColor, bool showValue)
        {
            Panel("Progress Track", left, bottom, right, top, new Color(0.12f, 0.15f, 0.19f));
            float fraction = maximum > 0 ? Mathf.Clamp01(value / (float)maximum) : 0;
            if (fraction > 0) Panel("Progress Fill", left, bottom, left + (right - left) * fraction, top, fillColor);
            if (showValue) Label(label + "  " + value + " / " + maximum, left + 0.006f, bottom, right - 0.006f, top, 13, TextAlignmentOptions.Left);
            else if (!string.IsNullOrEmpty(label)) Label(label, left, bottom, right, top, 12, TextAlignmentOptions.Center);
        }

        /// <summary>마을 지표마다 구분되는 진행 막대 색상을 반환한다.</summary>
        private Color GetStatColor(VillageStat stat)
        {
            switch (stat)
            {
                case VillageStat.Security: return new Color(0.47f, 0.66f, 0.83f);
                case VillageStat.Economy: return new Color(0.83f, 0.68f, 0.37f);
                case VillageStat.Military: return new Color(0.78f, 0.36f, 0.33f);
                case VillageStat.Learning: return new Color(0.58f, 0.68f, 0.86f);
                case VillageStat.Food: return new Color(0.43f, 0.70f, 0.46f);
                default: return new Color(0.72f, 0.53f, 0.75f);
            }
        }

        /// <summary>응답 계열에 맞는 카드 배경색을 반환한다.</summary>
        private Color CardColorForInterpretation(Interpretation interpretation)
        {
            return interpretation == Interpretation.Wisdom ? new Color(0.10f, 0.25f, 0.37f) : interpretation == Interpretation.Destruction ? new Color(0.38f, 0.15f, 0.16f) : new Color(0.13f, 0.30f, 0.21f);
        }

        /// <summary>응답 계열에 맞는 카드 테두리 색을 반환한다.</summary>
        private Color CardAccentForInterpretation(Interpretation interpretation)
        {
            return interpretation == Interpretation.Wisdom ? new Color(0.40f, 0.74f, 0.91f) : interpretation == Interpretation.Destruction ? new Color(0.91f, 0.48f, 0.39f) : new Color(0.52f, 0.79f, 0.48f);
        }

        /// <summary>시작 카드의 이름, 비용과 효과를 반환한다.</summary>
        private string CardDescription(StartingCard card)
        {
            CardData data = CardData.Find(card);
            string family = data.Family < 0 ? "공용" : new[] { "지혜", "파괴", "풍요" }[data.Family];
            return "[" + family + "] 행사력 " + data.Cost + "\n\n" + data.Name + "\n\n" + data.Description;
        }

        /// <summary>강화 비용이 반영된 인스턴스의 계열과 효과 설명을 반환한다.</summary>
        private string CardDescription(CardInstance instance)
        {
            string description = CardDescription(instance.Kind);
            if (instance.Cost != CardData.Find(instance.Kind).Cost)
                description = description.Replace("행사력 " + CardData.Find(instance.Kind).Cost, "행사력 " + instance.Cost + " (강화)");
            return description;
        }

        /// <summary>카드 계열별 배경색을 반환한다.</summary>
        private Color CardColor(StartingCard card)
        {
            switch (CardData.Find(card).Family)
            {
                case 0: return new Color(0.10f, 0.25f, 0.37f);
                case 1: return new Color(0.38f, 0.15f, 0.16f);
                case 2: return new Color(0.13f, 0.30f, 0.21f);
                default: return new Color(0.25f, 0.24f, 0.22f);
            }
        }

        /// <summary>카드 계열별 테두리 색을 반환한다.</summary>
        private Color CardAccent(StartingCard card)
        {
            switch (CardData.Find(card).Family)
            {
                case 0: return new Color(0.40f, 0.74f, 0.91f);
                case 1: return new Color(0.91f, 0.48f, 0.39f);
                case 2: return new Color(0.52f, 0.79f, 0.48f);
                default: return new Color(0.80f, 0.72f, 0.55f);
            }
        }

        /// <summary>둥근 모서리를 가진 카드 프레임용 슬라이스 스프라이트를 만든다.</summary>
        private Sprite CreateCardSprite()
        {
            const int SIZE = 64;
            const float RADIUS = 10f;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            texture.name = "Card Shape";
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = Mathf.Max(RADIUS - x, 0, x - (SIZE - 1 - RADIUS));
                    float dy = Mathf.Max(RADIUS - y, 0, y - (SIZE - 1 - RADIUS));
                    float alpha = Mathf.Clamp01(RADIUS + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        }

        /// <summary>계열색 프레임과 제목 띠를 손패 카드에 적용한다.</summary>
        private void StyleCard(RectTransform rect, CardInstance instance, string mark)
        {
            StartingCard card = instance.Kind;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.sprite = _cardSprite; image.type = UnityEngine.UI.Image.Type.Sliced; image.color = CardColor(card);
            AddOutline(rect, CardAccent(card));
            var text = rect.GetComponentInChildren<TextMeshProUGUI>();
            string[] lines = CardDescription(card).Split(new[] { "\n\n" }, StringSplitOptions.None);
            text.text = lines.Length > 2 ? lines[2] : CardDescription(card);
            text.fontSize = 12;
            var body = text.rectTransform;
            body.anchorMin = new Vector2(0.05f, 0.04f); body.anchorMax = new Vector2(0.95f, 0.65f);
            var titleObject = new GameObject("Card Heading", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(rect, false);
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.65f); titleRect.anchorMax = new Vector2(0.96f, 0.96f);
            titleRect.offsetMin = Vector2.zero; titleRect.offsetMax = Vector2.zero;
            var title = titleObject.GetComponent<TextMeshProUGUI>();
            title.font = _font; title.fontSize = 13; title.color = CardAccent(card);
            title.alignment = TextAlignmentOptions.Center; title.raycastTarget = false;
            string heading = lines.Length > 1 ? lines[1] + "\n" + lines[0] : CardDescription(card);
            if (instance.IsUpgraded) heading = heading.Replace("행사력 " + CardData.Find(card).Cost, "행사력 " + instance.Cost + " (강화)");
            title.text = mark + heading;
        }

        /// <summary>손패 index의 카드를 선택하거나 헤아림의 버릴 카드로 지정한다.</summary>
        private void SelectCard(int index)
        {
            if (_discardingReorganize)
            {
                CompleteReorganize(index);
                return;
            }
            if (_exchanging)
            {
                if (index == _selectedCard) return;
                if (_exchangeIndices.Contains(index)) _exchangeIndices.Remove(index);
                else if (_exchangeIndices.Count < (_rules.Hand[_selectedCard].Kind == StartingCard.Consideration ? 2 : 1)) _exchangeIndices.Add(index);
                Refresh();
                return;
            }
            _selectedCard = index;
            StartingCard card = _rules.Hand[index].Kind;
            if (card == StartingCard.Consideration)
            {
                _exchanging = true;
                _exchangeIndices.Clear();
                _message = "헤아림 · 버릴 다른 카드 0~2장을 선택하세요.";
            }
            else if (card == StartingCard.P04)
            {
                if (_rules.Hand.Count <= 1) { _message = "봉헌은 버릴 다른 손패가 필요합니다."; _selectedCard = -1; }
                else { _exchanging = true; _exchangeIndices.Clear(); _message = "봉헌 · 버릴 카드 한 장을 선택하세요."; }
            }
            else if (card == StartingCard.Comfort || card == StartingCard.D05 || card == StartingCard.P02 || card == StartingCard.P03 || card == StartingCard.W05 || card == StartingCard.N02 || card == StartingCard.N03)
                CommitCard(-1, 0, -1, -1);
            else if (card == StartingCard.P06 && _rules.Grace < 3)
                CommitCard(-1, 0, -1, -1);
            else _message = _rules.Power >= _rules.Hand[index].Cost ? "권능을 사용할 기도를 선택하세요." : "행사력이 부족합니다.";
            Refresh();
        }

        /// <summary>선택한 권능 카드를 대상 기도 또는 추가 대상 절차에 연결한다.</summary>
        private void SelectTarget(int index)
        {
            if (_selectedCard < 0 || _exchanging || _discardingReorganize || index < 0 || index >= _rules.Prayers.Count) return;
            if (_awaitingSecondaryTarget)
            {
                if (index == _pendingTarget || _rules.Prayers[index].IsResolved) return;
                CompleteSecondaryTarget(index);
                return;
            }
            if (_pendingTarget >= 0) return;
            if (_rules.Prayers[index].IsResolved)
            {
                _message = "이미 해결된 기도에는 권능을 사용할 수 없습니다.";
                Refresh();
                return;
            }
            StartingCard card = _rules.Hand[_selectedCard].Kind;
            if (card == StartingCard.Sharing || card == StartingCard.D01)
            {
                if (_rules.HasActiveOtherPrayer(index))
                {
                    _pendingTarget = index;
                    _awaitingSecondaryTarget = true;
                    _secondaryTargetOptional = card == StartingCard.Sharing;
                    _message = card == StartingCard.D01 ? "초과량을 전달할 다른 기도를 선택하세요." : "나눔의 두 번째 기도를 선택하거나 한 대상만 처리하세요.";
                    Refresh();
                    return;
                }
            }
            if ((card == StartingCard.Harvest || card == StartingCard.P07) && _rules.Grace > 0)
            {
                _pendingTarget = index;
                _message = card == StartingCard.Harvest ? "결실에 쓸 은총량을 선택하세요." : "소비할 은총량을 선택하세요.";
                Refresh();
                return;
            }
            CommitCard(index, 0, -1, -1);
        }

        /// <summary>선택한 카드 입력을 적용하고 임시 대상·버리기 상태를 닫는다.</summary>
        private void CommitCard(int prayerIndex, int graceSpent, int secondaryPrayerIndex, int discardIndex)
        {
            if (_selectedCard < 0) return;
            int previousFaith = _rules.Faith;
            int previousResolvedCount = CountResolvedPrayers();
            int family = CardData.Find(_rules.Hand[_selectedCard].Kind).Family;
            bool played = _rules.PlayCard(_selectedCard, prayerIndex, graceSpent, secondaryPrayerIndex, discardIndex);
            _message = played ? "권능을 사용했습니다." : "행사력, 대상 또는 카드 전제 조건을 확인하세요.";
            if (played)
            {
                PlayCue(GetFamilyCue(family));
                if (_rules.Phase == FirstStagePhase.Ascended) PlayCue(SoundCue.Ascension);
                else if (CountResolvedPrayers() > previousResolvedCount) PlayCue(SoundCue.PrayerResolved);
                PlayFaithWarning(previousFaith);
            }
            _selectedCard = -1;
            _pendingTarget = -1;
            _awaitingSecondaryTarget = false;
            _secondaryTargetOptional = false;
            _exchanging = false;
            _exchangeIndices.Clear();
            _discardingReorganize = _rules.NeedsReorganizeDiscard;
            Refresh();
        }

        /// <summary>드래그한 손패를 기도 대상에 사용하거나 비대상 카드의 선택을 연다.</summary>
        internal void DropCard(int cardIndex, int prayerIndex)
        {
            _dragCard = -1;
            if (!CanDragCard || cardIndex < 0 || cardIndex >= _rules.Hand.Count) return;
            StartingCard card = _rules.Hand[cardIndex].Kind;
            if (card == StartingCard.Comfort || card == StartingCard.Consideration || card == StartingCard.D05 || card == StartingCard.P02 || card == StartingCard.P03 || card == StartingCard.P04 || card == StartingCard.P06 || card == StartingCard.W05 || card == StartingCard.N02 || card == StartingCard.N03) { SelectCard(cardIndex); return; }
            _selectedCard = cardIndex;
            SelectTarget(prayerIndex);
        }

        /// <summary>드래그 중인 손패 인덱스를 기록해 대상 설명에 종료 주의를 표시한다.</summary>
        internal void BeginCardDrag(int cardIndex) => _dragCard = cardIndex;

        /// <summary>대상 밖에 놓인 드래그를 취소한다.</summary>
        internal void EndCardDrag() => _dragCard = -1;

        /// <summary>선택한 은총량으로 대상 카드를 사용한다.</summary>
        private void ConfirmTargetCard(int spent)
        {
            CommitCard(_pendingTarget, spent, -1, -1);
        }

        /// <summary>선택한 전체 대상 카드에 은총 소비 선택을 적용한다.</summary>
        private void ConfirmGlobalCard(int spent)
        {
            CommitCard(-1, spent, -1, -1);
        }

        /// <summary>선택한 두 번째 대상 또는 나눔의 단일 대상 확정을 처리한다.</summary>
        private void CompleteSecondaryTarget(int prayerIndex)
        {
            int primary = _pendingTarget;
            _awaitingSecondaryTarget = false;
            _secondaryTargetOptional = false;
            CommitCard(primary, 0, prayerIndex, -1);
        }

        /// <summary>재정비에 선택한 카드를 버리고 다음 턴 입력을 다시 연다.</summary>
        private void CompleteReorganize(int handIndex)
        {
            _message = _rules.CompleteReorganize(handIndex) ? "재정비를 마쳤습니다." : "버릴 카드를 선택하세요.";
            _discardingReorganize = false;
            Refresh();
        }

        /// <summary>대상·카드 선택을 취소하고 원래 손패 화면으로 돌아간다.</summary>
        private void CancelCardChoice()
        {
            _selectedCard = -1;
            _pendingTarget = -1;
            _awaitingSecondaryTarget = false;
            _secondaryTargetOptional = false;
            _exchanging = false;
            _exchangeIndices.Clear();
            _message = "카드 선택을 취소했습니다.";
            Refresh();
        }

        /// <summary>선택한 손패를 헤아림으로 교환한다.</summary>
        private void ConfirmExchange()
        {
            bool success;
            if (_rules.Hand[_selectedCard].Kind == StartingCard.Consideration)
                success = _rules.Exchange(_selectedCard, _exchangeIndices.ToArray());
            else
                success = _exchangeIndices.Count == 1 && _rules.PlayCard(_selectedCard, -1, 0, -1, _exchangeIndices[0]);
            _message = success ? "선택한 카드를 처리했습니다." : "카드 조건 또는 버릴 카드를 확인하세요.";
            _selectedCard = -1;
            _exchanging = false;
            _exchangeIndices.Clear();
            Refresh();
        }

        /// <summary>현재 턴을 종료하고 예측된 손실을 적용한다.</summary>
        private void EndTurn()
        {
            int loss = _rules.PredictLoss();
            int previousFaith = _rules.Faith;
            int previousResolvedCount = CountResolvedPrayers();
            bool isBossBattle = _rules.Phase == FirstStagePhase.BossBattle;
            int oldHeight = _rules.BossHeight;
            int oldFloods = _rules.BossFloodCount;
            string weather = isBossBattle ? _rules.CurrentWeather : "";
            if (_rules.EndTurn())
            {
                bool bossResolved = isBossBattle && (_rules.Phase == FirstStagePhase.BossResolved || _rules.Phase == FirstStagePhase.Ascended);
                if (_rules.Phase == FirstStagePhase.Ascended) PlayCue(SoundCue.Ascension);
                else if (CountResolvedPrayers() > previousResolvedCount) PlayCue(SoundCue.PrayerResolved);
                if (isBossBattle && !bossResolved && _rules.BossFloodCount > oldFloods) PlayCue(SoundCue.Flood);
                PlayFaithWarning(previousFaith);
                _message = bossResolved
                    ? "해법으로 보스를 해결해 날씨와 범람을 건너뛰었습니다."
                    : isBossBattle
                        ? weather + " · 수위 " + oldHeight + "→" + _rules.BossHeight + (_rules.BossFloodCount > oldFloods ? " · 범람 " + (_rules.BossFloodCount == 1 && _rules.BossPerkSummary.Contains("첫 범람") ? "손실 3" : "손실 6") : "") + " · 신앙 " + loss + " 감소"
                        : "신앙 " + loss + " 감소.";
            }
            _selectedCard = -1;
            Refresh();
        }

        /// <summary>현재 보스 턴의 물길 돌리기를 사용하고 예상 손실을 갱신한다.</summary>
        private void UseWaterChannel()
        {
            bool used = _rules.UseWaterChannel();
            _message = used ? "물길을 돌려 수위를 2 낮췄습니다." : "물길 돌리기 조건을 확인하세요.";
            if (used) PlayCue(SoundCue.Common);
            Refresh();
        }

        /// <summary>첫 보상 세 장의 효과와 건너뛰기를 표시한다.</summary>
        private void ShowReward()
        {
            Label(_rules.Stage == 1 ? "첫 권능을 선택하세요" : "S" + _rules.Stage + " · 새 권능을 선택하세요", 0.06f, 0.87f, 0.78f, 0.97f, 30, TextAlignmentOptions.Center);
            for (int i = 0; i < _rules.RewardOffers.Count; i++)
            {
                StartingCard card = _rules.RewardOffers[i];
                float x = 0.1f + i * 0.27f;
                RectTransform rect = Button(CardDescription(card) + "\n\n" + CardPrerequisite(card), x, 0.30f, x + 0.24f, 0.82f, () => ChooseReward(card), 18);
                var image = rect.GetComponent<UnityEngine.UI.Image>();
                image.sprite = _cardSprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = CardColor(card);
                AddOutline(rect, CardAccent(card));
            }
            Button("보상 건너뛰기", 0.36f, 0.12f, 0.64f, 0.22f, () => ChooseReward(null), 18);
        }

        /// <summary>보상 선택을 덱에 반영하고 다음 기도 선택을 연다.</summary>
        private void ChooseReward(StartingCard? card)
        {
            if (!_rules.SelectReward(card)) return;
            if (card.HasValue) PlayCue(GetFamilyCue(CardData.Find(card.Value).Family));
            _candidate = -1;
            _selectedCard = -1;
            _message = "";
            Refresh();
        }

        /// <summary>보상 카드의 사용 전제와 중요한 부가 규칙을 표시한다.</summary>
        private string CardPrerequisite(StartingCard card)
        {
            switch (card)
            {
                case StartingCard.Insight: return "해결 전까지 매 턴 종료에 작동";
                case StartingCard.W01: case StartingCard.W04: case StartingCard.W06: return "해법이 있으면 효과 상승";
                case StartingCard.W02: return "다른 활성 기도에 해법이 있어야 사용";
                case StartingCard.W03: case StartingCard.W07: return "대상 기도에 해법이 있어야 사용";
                case StartingCard.D01: return "초과량을 다른 활성 기도에 전달";
                case StartingCard.D06: return "기도에 균열이 남으면 다음 파괴 카드 +3";
                case StartingCard.P02: return "은총 2 이상 필요";
                case StartingCard.P04: return "다른 손패가 1장 이상 필요";
                case StartingCard.Harvest: return "은총이 없으면 감소 후 은총 +2";
                case StartingCard.P06: return "은총 3을 소비하면 모든 기도 감소량 2배";
                case StartingCard.P07: return "은총 0~6 소비량을 선택";
                case StartingCard.D05: return "현재 신앙이 3보다 많아야 사용 가능";
                case StartingCard.N01: return "이번 턴 대상 기도의 신앙 감소 차단";
                case StartingCard.N03: return "2장 뽑은 뒤 손패 1장 버림";
                default: return "";
            }
        }

        /// <summary>패배 또는 해결 결과와 기도별 해석 선택을 표시한다.</summary>
        private void ShowResult()
        {
            if (_showingResolutionPopup)
            {
                ShowResolutionPopup();
                return;
            }
            int pendingInterpretation = FindPendingInterpretationIndex();
            if (pendingInterpretation >= 0)
            {
                ShowPrayerInterpretationChoice(pendingInterpretation);
                return;
            }
            bool won = _rules.Phase == FirstStagePhase.Resolved || _rules.Phase == FirstStagePhase.BossResolved || _rules.Phase == FirstStagePhase.Ascended;
            bool ascended = _rules.Phase == FirstStagePhase.Ascended;
            string title = ascended ? "홍수를 막고 영지의 신으로 승격했습니다" : _rules.Phase == FirstStagePhase.BossResolved ? "홍수 보스를 해결했습니다" : won ? "중대 기도를 해결했습니다" : "신앙이 사라졌습니다";
            Label(title, 0.05f, 0.88f, 0.78f, 0.97f, 29, TextAlignmentOptions.Center);
            Label("S" + _rules.Stage + "  ·  작은 기도 " + _rules.ResolvedSmallCount + "/" + _rules.PresentedSmallCount + " 응답", 0.05f, 0.81f, 0.95f, 0.86f, 18, TextAlignmentOptions.Center);
            AddProgressBar("신앙", _rules.Faith, 30, 0.30f, 0.76f, 0.70f, 0.80f, new Color(0.59f, 0.71f, 0.93f), true);
            DrawVillageStats(0.68f, 0.73f);
            var history = new StringBuilder();
            int first = Math.Max(0, _rules.Chronicle.Count - 12);
            for (int i = first; i < _rules.Chronicle.Count; i++) history.AppendLine(_rules.Chronicle[i]);
            if (_rules.AllInterpretationsChosen)
                history.Append("\n문명: ").Append(_rules.Civilization).Append("  ·  신의 평판: ").Append(_rules.Reputation);
            Label(history.ToString(), 0.09f, 0.26f, 0.91f, 0.65f, 16, TextAlignmentOptions.TopLeft);
            if (_rules.AllInterpretationsChosen)
            {
                if (ascended)
                    Button("새 런 시작", 0.39f, 0.09f, 0.61f, 0.18f, () => { _started = false; Refresh(); }, 18);
                else if (won && _rules.Phase == FirstStagePhase.Resolved && _rules.Stage <= 6)
                    Button("카드 보상으로", 0.37f, 0.09f, 0.63f, 0.18f, () => { _rules.OpenReward(); Refresh(); }, 18);
                else
                {
                    Label(won ? "해결 기록을 확인했습니다." : "이 기록에서 다시 시작할 수 있습니다.", 0.05f, 0.19f, 0.95f, 0.25f, 16, TextAlignmentOptions.Center);
                    Button("처음으로", 0.39f, 0.09f, 0.61f, 0.18f, () => { _started = false; Refresh(); }, 18);
                }
            }
        }

        /// <summary>사건의 발생 근거와 세 선택 또는 세부 강화 대상을 표시한다.</summary>
        private void ShowEvent()
        {
            Label("마을의 변화 · " + _rules.EventTitle(), 0.06f, 0.87f, 0.78f, 0.97f, 30, TextAlignmentOptions.Center);
            Label(_rules.EventReason + "\n" + _rules.EventStory(), 0.09f, 0.73f, 0.91f, 0.86f, 20, TextAlignmentOptions.Center);
            if (_eventChoice < 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    int choice = i;
                    int family = _rules.EventUpgradeFamily(choice);
                    string text = _rules.EventChoiceText(choice);
                    if (family != -2)
                    {
                        List<CardInstance> targets = _rules.EventUpgradeTargets(choice);
                        if (targets.Count == 0) text += "\n강화 가능한 카드가 없습니다";
                        else if (family >= 0 && family <= 2 && CardData.Find(targets[0].Kind).Family != family) text += "\n해당 계열이 없어 기본 공격 강화";
                        if (targets.Count == 0) { Label(text, 0.08f + i * 0.29f, 0.40f, 0.35f + i * 0.29f, 0.69f, 17, TextAlignmentOptions.Center); continue; }
                    }
                    Button(text, 0.08f + i * 0.29f, 0.40f, 0.35f + i * 0.29f, 0.69f, () => SelectEventChoice(choice), 18);
                }
            }
            else if (_rules.EventUpgradeFamily(_eventChoice) != -2)
            {
                List<CardInstance> targets = _rules.EventUpgradeTargets(_eventChoice);
                Label("강화할 카드 한 장을 선택하세요 · 카드당 1회, 비용 -1", 0.08f, 0.64f, 0.92f, 0.72f, 18, TextAlignmentOptions.Center);
                int columns = Math.Min(4, targets.Count);
                for (int i = 0; i < targets.Count; i++)
                {
                    CardInstance card = targets[i];
                    int column = i % columns, row = i / columns;
                    float x = 0.07f + column * 0.215f;
                    float top = 0.63f - row * 0.10f;
                    Button(CardData.Find(card.Kind).Name + " #" + card.Id + " · " + card.Cost + "→" + (card.Cost - 1), x, top - 0.085f, x + 0.20f, top, () => ConfirmEvent(card.Id), 16);
                }
                Button("이전 선택으로", 0.37f, 0.06f, 0.63f, 0.14f, () => { _eventChoice = -1; Refresh(); }, 17);
            }
            else
            {
                Label("마을 지표 하나를 골라 성장시키세요", 0.1f, 0.64f, 0.9f, 0.72f, 18, TextAlignmentOptions.Center);
                int statCount = Enum.GetValues(typeof(VillageStat)).Length;
                for (int i = 0; i < statCount; i++)
                {
                    int stat = i;
                    int column = i % 3;
                    int row = i / 3;
                    float left = 0.13f + column * 0.25f;
                    float top = 0.61f - row * 0.12f;
                    Button(FirstStageRules.VillageStatName((VillageStat)i) + " +1", left, top - 0.09f, left + 0.22f, top, () => ConfirmEvent(stat), 16);
                }
                Button("이전 선택으로", 0.37f, 0.06f, 0.63f, 0.14f, () => { _eventChoice = -1; Refresh(); }, 17);
            }
            DrawVillageStats(0.17f, 0.24f);
            AddProgressBar("신앙", _rules.Faith, 30, 0.38f, 0.11f, 0.62f, 0.15f, new Color(0.59f, 0.71f, 0.93f), true);
        }

        /// <summary>사건 선택에 추가 입력이 필요하면 선택 화면을 열고 그렇지 않으면 즉시 적용한다.</summary>
        private void SelectEventChoice(int choice)
        {
            if (_rules.EventUpgradeFamily(choice) != -2 || (_rules.EventId == "E09" && choice == 2))
            {
                _eventChoice = choice;
                Refresh();
                return;
            }
            if (_rules.ChooseEvent(choice))
            {
                PlayCue(SoundCue.EventChoice);
                Refresh();
            }
        }

        /// <summary>사건의 카드 인스턴스 또는 문명 계열 선택을 확정한다.</summary>
        private void ConfirmEvent(int parameter)
        {
            if (!_rules.ChooseEvent(_eventChoice, parameter)) return;
            PlayCue(SoundCue.EventChoice);
            _eventChoice = -1;
            Refresh();
        }

        /// <summary>확정된 사건 결과와 그 원인이 된 이전 행적을 표시한다.</summary>
        private void ShowEventResult()
        {
            Label("마을에 남은 변화", 0.06f, 0.87f, 0.78f, 0.97f, 30, TextAlignmentOptions.Center);
            Label(_rules.EventReason + "\n\n" + _rules.EventResult, 0.10f, 0.61f, 0.90f, 0.83f, 21, TextAlignmentOptions.Center);
            DrawVillageStats(0.51f, 0.58f);
            AddProgressBar("신앙", _rules.Faith, 30, 0.38f, 0.44f, 0.62f, 0.48f, new Color(0.59f, 0.71f, 0.93f), true);
            var history = new StringBuilder();
            int first = Math.Max(0, _rules.Chronicle.Count - 7);
            for (int i = first; i < _rules.Chronicle.Count; i++) history.AppendLine(_rules.Chronicle[i]);
            Label(history.ToString(), 0.12f, 0.20f, 0.88f, 0.41f, 16, TextAlignmentOptions.TopLeft);
            Button(_rules.Stage == 6 ? "보스 준비로" : "다음 기도 선택", 0.36f, 0.07f, 0.64f, 0.16f, ContinueAfterEvent, 18);
        }

        /// <summary>보스 전 휴식과 일시 제외할 카드 두 장까지 선택하도록 표시한다.</summary>
        private void ShowBossPreparation()
        {
            Label("마지막 대비 · 대홍수 보스", 0.05f, 0.89f, 0.78f, 0.98f, 30, TextAlignmentOptions.Center);
            Label("휴식으로 신앙 +5 (최대 30)\n이번 전투에서 카드 인스턴스 최대 2장 제외 · 덱 최소 10장\n" + _rules.BossPerkSummary, 0.06f, 0.74f, 0.94f, 0.88f, 18, TextAlignmentOptions.Center);
            int columns = 9;
            for (int i = 0; i < _rules.Owned.Count; i++)
            {
                CardInstance card = _rules.Owned[i];
                int column = i % columns;
                int row = i / columns;
                float left = 0.035f + column * 0.103f;
                float top = 0.66f - row * 0.16f;
                string text = (_bossRemovalSelection.Contains(card.Id) ? "제외\n" : "") + CardData.Find(card.Kind).Name + "\n#" + card.Id + " · 비용 " + card.Cost;
                RectTransform rect = Button(text, left, top - 0.135f, left + 0.098f, top, () => ToggleBossRemoval(card.Id), 11);
                rect.GetComponent<UnityEngine.UI.Image>().sprite = _cardSprite;
                rect.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
                rect.GetComponent<UnityEngine.UI.Image>().color = _bossRemovalSelection.Contains(card.Id) ? new Color(0.22f, 0.18f, 0.18f) : CardColor(card.Kind);
                AddOutline(rect, _bossRemovalSelection.Contains(card.Id) ? Color.white : CardAccent(card.Kind));
            }
            int remaining = _rules.Owned.Count - _bossRemovalSelection.Count;
            Label("제외 " + _bossRemovalSelection.Count + "/2  ·  보스 전투 덱 " + remaining + "장", 0.12f, 0.29f, 0.88f, 0.37f, 18, TextAlignmentOptions.Center);
            Label(_message, 0.12f, 0.37f, 0.88f, 0.43f, 15, TextAlignmentOptions.Center);
            Label("수위 0에서 시작 · 비 날씨와 물길 돌리기로 범람을 늦추세요.\n승리하면 마을 기록을 남기고 영지의 신으로 승격합니다.", 0.12f, 0.19f, 0.88f, 0.29f, 16, TextAlignmentOptions.Center);
            if (remaining >= 10 && _bossRemovalSelection.Count <= 2)
                Button("휴식하고 보스전에 진입", 0.36f, 0.06f, 0.64f, 0.15f, StartBossBattle, 18);
            else
                Label("보스 덱은 10장 이상이어야 합니다.", 0.25f, 0.07f, 0.75f, 0.15f, 16, TextAlignmentOptions.Center);
        }

        /// <summary>보스 전투에서 제외할 카드 인스턴스를 선택하거나 선택 해제한다.</summary>
        private void ToggleBossRemoval(int cardId)
        {
            if (_bossRemovalSelection.Contains(cardId)) _bossRemovalSelection.Remove(cardId);
            else if (_bossRemovalSelection.Count < 2) _bossRemovalSelection.Add(cardId);
            else _message = "이번 보스에서 제외할 카드는 최대 두 장입니다.";
            Refresh();
        }

        /// <summary>보스 전 휴식과 덱 제외를 확정하고 최종 전투를 시작한다.</summary>
        private void StartBossBattle()
        {
            if (!_rules.StartBossBattle(new List<int>(_bossRemovalSelection).ToArray()))
            {
                _message = "덱 제외 조건을 확인하세요.";
                Refresh();
                return;
            }
            _bossRemovalSelection.Clear();
            _message = "휴식으로 신앙을 회복했습니다. 홍수가 시작됩니다.";
            Refresh();
        }

        /// <summary>사건 선택을 마치고 다음 스테이지 또는 보스 준비로 이동한다.</summary>
        private void ContinueAfterEvent()
        {
            if (!_rules.ContinueAfterEvent()) return;
            _eventChoice = -1;
            _message = "";
            Refresh();
        }

        /// <summary>화면 비율 좌표와 색상으로 배경 패널을 만든다.</summary>
        private RectTransform Panel(string name, float left, float bottom, float right, float top, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(_content, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = obj.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>카드의 계열 또는 기도 종류를 구분하는 테두리를 추가한다.</summary>
        private void AddOutline(RectTransform rect, Color color)
        {
            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2, -2);
        }

        /// <summary>응답 방식별 마을 지표 변화만 보여주는 비차단 설명 패널을 준비한다.</summary>
        private void CreateTooltip()
        {
            RectTransform rect = Panel("Prayer Tooltip", 0.24f, 0.25f, 0.76f, 0.49f, new Color(0.07f, 0.09f, 0.12f, 0.98f));
            AddOutline(rect, new Color(0.69f, 0.57f, 0.36f));
            var obj = new GameObject("Outcomes", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(rect, false);
            var textRect = obj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8); textRect.offsetMax = new Vector2(-12, -8);
            _tooltipText = obj.GetComponent<TextMeshProUGUI>();
            _tooltipText.font = _font; _tooltipText.fontSize = 15;
            _tooltipText.color = Color.white; _tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
            _tooltipText.raycastTarget = false;
            _tooltip = rect.gameObject;
            _tooltip.SetActive(false);
        }

        /// <summary>기도 index에 마우스를 올리면 응답 방식별 수치 변화만 표시한다.</summary>
        internal void ShowPrayerTooltip(int index)
        {
            if (_tooltip == null || index < 0 || index >= _rules.Prayers.Count) return;
            string id = _rules.Prayers[index].Id;
            _tooltipText.text = "응답 방식별 마을 지표 변화\n지혜  " + _rules.DescribeStatChanges(id, Interpretation.Wisdom) + "\n파괴  " + _rules.DescribeStatChanges(id, Interpretation.Destruction) + "\n풍요  " + _rules.DescribeStatChanges(id, Interpretation.Prosperity);
            _tooltip.transform.SetAsLastSibling();
            _tooltip.SetActive(true);
        }

        /// <summary>기도 설명 패널을 닫는다.</summary>
        internal void HidePrayerTooltip()
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        /// <summary>화면 비율 좌표에 한국어 텍스트를 배치한다.</summary>
        private void Label(string text, float left, float bottom, float right, float top, int size, TextAlignmentOptions alignment)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(_content, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = obj.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = size;
            label.color = new Color(0.93f, 0.94f, 0.91f);
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = text;
        }

        /// <summary>화면 비율 좌표에 텍스트 버튼을 만들고 클릭 동작을 연결한다.</summary>
        private RectTransform Button(string text, float left, float bottom, float right, float top, Action action, int size = 20)
        {
            RectTransform rect = Panel("Button", left, bottom, right, top, new Color(0.16f, 0.23f, 0.29f));
            var buttonImage = rect.GetComponent<UnityEngine.UI.Image>();
            buttonImage.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = buttonImage;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.94f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.92f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => action());
            var obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(rect, false);
            var textRect = obj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(7, 5);
            textRect.offsetMax = new Vector2(-7, -5);
            var label = obj.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = text;
            return rect;
        }

        /// <summary>모든 화면에서 효과음을 켜거나 끄는 접근 가능한 버튼을 표시한다.</summary>
        private void AddSoundToggle()
        {
            Button("효과음 " + (_soundEnabled ? "켬" : "끔"), 0.82f, 0.93f, 0.97f, 0.99f, ToggleSound, 14);
        }
    }
}
