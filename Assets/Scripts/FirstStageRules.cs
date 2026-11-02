using System;
using System.Collections.Generic;

namespace NamelessGod
{
    public enum StartingCard { W08, D08, P08, Comfort, Consideration, Insight, Sever, Harvest, Sharing, W01, W02, W03, W04, W05, W06, W07, D01, D02, D03, D04, D05, D06, D07, P02, P03, P04, P05, P06, P07, N01, N02, N03 }
    public enum FirstStagePhase { Selection, Battle, Resolved, Defeated, Reward, Event, EventResult, BossPreparation, BossBattle, BossResolved, Ascended }
    public enum Interpretation { Wisdom, Destruction, Prosperity }
    public enum VillageStat { Security, Economy, Military, Learning, Food }

    public sealed class FirstStagePrayer
    {
        private readonly string _id;
        private readonly string _name;
        private readonly int _loss;
        private readonly bool _isMajor;
        private int _hardship;
        private readonly int[] _contributions = new int[3];
        private int _solution;
        private int _backlash;
        private bool _hasPromise;
        private bool _hasRift;

        public string Id => _id;
        public string Name => _name;
        public int Loss => _loss;
        public bool IsMajor => _isMajor;
        public int Hardship => _hardship;
        public bool IsResolved => _hardship == 0;
        public int Solution => _solution;
        public int Backlash => _backlash;
        public bool HasPromise => _hasPromise;
        public bool HasRift => _hasRift;

        /// <summary>기도의 식별자, 이름, 초기 고난, 턴 손실과 중대 여부를 설정한다.</summary>
        public FirstStagePrayer(string id, string name, int hardship, int loss, bool isMajor)
        {
            _id = id;
            _name = name;
            _hardship = hardship;
            _loss = loss;
            _isMajor = isMajor;
        }

        /// <summary>남은 고난을 amount만큼 줄이고 실제 감소량을 반환한다.</summary>
        internal int Reduce(int amount)
        {
            int actual = Math.Min(_hardship, amount);
            _hardship -= actual;
            return actual;
        }

        /// <summary>해당 계열이 실제로 감소시킨 고난을 누적한다.</summary>
        internal void AddContribution(Interpretation interpretation, int amount) => _contributions[(int)interpretation] += amount;

        /// <summary>지정한 계열의 실제 고난 감소량을 반환한다.</summary>
        public int GetContribution(Interpretation interpretation) => _contributions[(int)interpretation];

        /// <summary>기도에 턴 종료마다 작동하는 해법을 설치한다.</summary>
        internal void InstallSolution(int amount = 2) => _solution = amount;

        /// <summary>이번 턴의 기도별 신앙 손실을 1 늘린다.</summary>
        internal void AddBacklash(int amount = 1) => _backlash += amount;

        /// <summary>이번 턴의 기도별 신앙 손실을 약속으로 막는다.</summary>
        internal void InstallPromise() => _hasPromise = true;

        /// <summary>다음 파괴 카드가 사용할 균열을 설치한다.</summary>
        internal void InstallRift() => _hasRift = true;

        /// <summary>다음 직접 파괴 효과에 사용할 균열을 소비하고 적용량을 반환한다.</summary>
        internal int ConsumeRiftBonus()
        {
            if (!_hasRift) return 0;
            _hasRift = false;
            return 3;
        }

        /// <summary>턴이 바뀌며 반동을 초기화한다.</summary>
        internal void ResetBacklash() { _backlash = 0; _hasPromise = false; }

        /// <summary>해결된 기도의 해법과 이번 턴 반동을 제거한다.</summary>
        internal void ClearOnResolve() { _solution = 0; _backlash = 0; _hasPromise = false; _hasRift = false; }
    }

    public sealed class FirstStageRules
    {
        private const int MAX_FAITH = 30;
        private const int TURN_POWER = 3;
        private const int HAND_LIMIT = 10;
        private const int MAX_STAGES = 6;
        private const int BOSS_HARDSHIP = 60;
        private const int BOSS_MAX_HEIGHT = 6;
        private const int MAX_VILLAGE_STAT = 100;
        private static readonly string[] VILLAGE_STAT_NAMES = { "치안", "경제", "군사", "배움", "식량" };
        private static readonly string[] CIVILIZATION_NAMES = { "치안", "경제", "군사", "학문", "식량" };
        private static readonly int[] STAGE_HARDSHIP = { 12, 16, 20, 24, 28, 32 };
        private static readonly int[] STAGE_LOSS = { 2, 2, 3, 3, 3, 4 };
        private readonly Random _random;
        private readonly List<CardInstance> _draw = new List<CardInstance>();
        private readonly List<CardInstance> _hand = new List<CardInstance>();
        private readonly List<CardInstance> _discard = new List<CardInstance>();
        private readonly List<CardInstance> _exhausted = new List<CardInstance>();
        private readonly List<FirstStagePrayer> _prayers = new List<FirstStagePrayer>();
        private readonly Dictionary<string, string> _facts = new Dictionary<string, string>();
        private readonly List<string> _chronicle = new List<string>();
        private readonly HashSet<string> _interpreted = new HashSet<string>();
        private readonly int[] _villageStats = { 50, 50, 50, 50, 50 };
        private readonly List<CardInstance> _owned = new List<CardInstance>();
        private readonly List<StartingCard> _rewardOffers = new List<StartingCard>();
        private readonly HashSet<string> _resolvedSmall = new HashSet<string>();
        private readonly HashSet<string> _presentedSmall = new HashSet<string>();
        private readonly HashSet<string> _selectedMajors = new HashSet<string>();
        private readonly HashSet<string> _usedEvents = new HashSet<string>();
        private readonly HashSet<string> _resolvedThisTurn = new HashSet<string>();
        private readonly HashSet<int> _bossExcludedIds = new HashSet<int>();
        private int _nextCardId;
        private string _eventId;
        private string _eventReason;
        private string _eventResult;
        private readonly List<string> _majorCandidates = new List<string>();
        private readonly List<string> _smallCandidates = new List<string>();
        private readonly HashSet<string> _ignored = new HashSet<string>();
        private int _stage = 1;
        private int _grace;
        private bool _opportunityUsed;
        private int _faith = MAX_FAITH;
        private int _power;
        private int _comfort;
        private int _turn;
        private int _bossHeight;
        private int _bossWeatherIndex;
        private int _bossFloodCount;
        private bool _bossFirstFloodUsed;
        private bool _bossChannelUsed;
        private bool _bossChannelFreeUsed;
        private bool _bossRainUsed;
        private bool _needsReorganizeDiscard;
        private FirstStagePhase _phase = FirstStagePhase.Selection;

        public int Faith => _faith;
        public int Power => _power;
        public int Comfort => _comfort;
        public int Turn => _turn;
        public int Stage => _stage;
        public int StageHardship => STAGE_HARDSHIP[_stage - 1];
        public int StageLoss => STAGE_LOSS[_stage - 1];
        public int Grace => _grace;
        public IReadOnlyList<string> MajorCandidates => _majorCandidates.AsReadOnly();
        public IReadOnlyList<string> SmallCandidates => _smallCandidates.AsReadOnly();
        public FirstStagePhase Phase => _phase;
        public IReadOnlyList<CardInstance> Hand => _hand.AsReadOnly();
        public IReadOnlyList<CardInstance> Owned => _owned.AsReadOnly();
        public IReadOnlyList<StartingCard> RewardOffers => _rewardOffers.AsReadOnly();
        public string EventId => _eventId;
        public string EventReason => _eventReason;
        public string EventResult => _eventResult;
        public IReadOnlyList<FirstStagePrayer> Prayers => _prayers.AsReadOnly();
        public bool NeedsReorganizeDiscard => _needsReorganizeDiscard;
        public int BossHeight => _bossHeight;
        public int BossFloodCount => _bossFloodCount;
        public int BossWeatherIndex => _bossWeatherIndex;
        public int PredictedBossHeight => PredictBossHeight(_bossHeight);
        public int PredictedFloodDamage => PredictFloodDamage(_bossHeight);
        public IReadOnlyCollection<int> BossExcludedIds => _bossExcludedIds;
        public int PresentedSmallCount => _presentedSmall.Count;
        public string Reputation => GetReputation();
        public string Civilization => GetCivilization();
        public string CurrentWeather => WeatherText(_bossWeatherIndex);
        public string NextWeather => WeatherText((_bossWeatherIndex + 1) % 4);
        public bool CanUseWaterChannel => _phase == FirstStagePhase.BossBattle && !_bossChannelUsed && _bossHeight > 0;
        public int WaterChannelCost => HasFact("evacuation.plan", "true") && !_bossChannelFreeUsed ? 0 : 1;
        public string BossPerkSummary => GetBossPerkSummary();
        public string WaterChannelForecast => GetWaterChannelForecast();
        public int DrawCount => _draw.Count;
        public int DiscardCount => _discard.Count;
        public int ExhaustedCount => _exhausted.Count;
        public IReadOnlyDictionary<string, string> Facts => _facts;
        public IReadOnlyList<string> Chronicle => _chronicle.AsReadOnly();
        public int ResolvedSmallCount => _prayers.FindAll(prayer => !prayer.IsMajor && prayer.IsResolved).Count;
        public bool AllInterpretationsChosen => _interpreted.Count == _prayers.FindAll(prayer => prayer.IsResolved).Count;

        /// <summary>기도의 미선택 결과가 이전에 적용되었는지 반환한다.</summary>
        public bool WasIgnored(string id) => _ignored.Contains(id);

        /// <summary>시드로 시작 덱의 재현 가능한 셔플을 준비한다.</summary>
        public FirstStageRules(int seed)
        {
            _random = new Random(seed);
            for (int i = 0; i < 3; i++) AddOwned(StartingCard.W08);
            for (int i = 0; i < 3; i++) AddOwned(StartingCard.D08);
            for (int i = 0; i < 3; i++) AddOwned(StartingCard.P08);
            for (int i = 0; i < 2; i++) AddOwned(StartingCard.Comfort);
            AddOwned(StartingCard.Consideration);
            _majorCandidates.AddRange(new[] { "M01", "M03", "M06" });
            _smallCandidates.AddRange(new[] { "L01", "L02" });
            _presentedSmall.UnionWith(_smallCandidates);
        }

        /// <summary>정의 kind의 새 카드 인스턴스를 고유 ID와 함께 덱에 추가한다.</summary>
        private void AddOwned(StartingCard kind) => _owned.Add(new CardInstance(++_nextCardId, kind));

        /// <summary>카드 종류에 따라 단일 기도 대상을 요구하는지 반환한다.</summary>
        private static bool RequiresTarget(StartingCard card)
        {
            return card != StartingCard.Comfort && card != StartingCard.Consideration && card != StartingCard.D05 && card != StartingCard.P02 && card != StartingCard.P03 && card != StartingCard.P04 && card != StartingCard.P06 && card != StartingCard.W05 && card != StartingCard.N02 && card != StartingCard.N03;
        }

        /// <summary>전투 기도 목록에서 index가 미해결 대상을 가리키는지 반환한다.</summary>
        private bool IsActivePrayerIndex(int index)
        {
            return index >= 0 && index < _prayers.Count && !_prayers[index].IsResolved;
        }

        /// <summary>해법이 설치된 미해결 기도가 하나라도 있는지 반환한다.</summary>
        private bool HasActiveSolution()
        {
            foreach (FirstStagePrayer prayer in _prayers) if (!prayer.IsResolved && prayer.Solution > 0) return true;
            return false;
        }

        /// <summary>지정 기도 외에 활성 기도가 남아 있는지 반환한다.</summary>
        public bool HasActiveOtherPrayer(int prayerIndex)
        {
            for (int i = 0; i < _prayers.Count; i++) if (i != prayerIndex && !_prayers[i].IsResolved) return true;
            return false;
        }

        /// <summary>손패에서 카드 자신이 아닌 유효한 버릴 카드인지 반환한다.</summary>
        private bool IsOtherHandIndex(int cardIndex, int discardIndex)
        {
            return discardIndex >= 0 && discardIndex < _hand.Count && discardIndex != cardIndex;
        }

        /// <summary>재정비의 선택한 손패를 버리고 후속 입력 대기를 끝낸다.</summary>
        public bool CompleteReorganize(int handIndex)
        {
            if (!_needsReorganizeDiscard || (_phase != FirstStagePhase.Battle && _phase != FirstStagePhase.BossBattle)) return false;
            if (_hand.Count == 0 && handIndex == -1)
            {
                _needsReorganizeDiscard = false;
                return true;
            }
            if (handIndex < 0 || handIndex >= _hand.Count) return false;
            _discard.Add(_hand[handIndex]);
            _hand.RemoveAt(handIndex);
            _needsReorganizeDiscard = false;
            return true;
        }

        /// <summary>첫 손패에 필요한 카드 한 장을 뽑기 더미에서 손패로 옮긴다.</summary>
        private void MoveStartingCard(StartingCard kind)
        {
            int index = _draw.FindIndex(card => card.Kind == kind);
            _hand.Add(_draw[index]);
            _draw.RemoveAt(index);
        }

        /// <summary>중대 기도 ID를 확정하고 미선택 결과와 덱을 준비하여 전투를 시작한다.</summary>
        public bool SelectMajor(string id)
        {
            if (_phase != FirstStagePhase.Selection || !_majorCandidates.Contains(id)) return false;
            PrayerData major = PrayerData.Find(id);
            _selectedMajors.Add(id);
            _prayers.Clear();
            _interpreted.Clear();
            _resolvedThisTurn.Clear();
            _prayers.Add(new FirstStagePrayer(id, major.Name, STAGE_HARDSHIP[_stage - 1], STAGE_LOSS[_stage - 1], true));
            foreach (string smallId in _smallCandidates)
            {
                PrayerData small = PrayerData.Find(smallId);
                _prayers.Add(new FirstStagePrayer(small.Id, small.Name, small.Hardship, 1, false));
            }
            foreach (string candidate in _majorCandidates)
            {
                if (candidate == id || !_ignored.Add(candidate)) continue;
                PrayerData ignored = PrayerData.Find(candidate);
                _facts[ignored.IgnoreKey] = ignored.IgnoreValue;
                int[] changes = ApplyVillageStatChanges(ignored.GetIgnoredStatChanges());
                _chronicle.Add("미선택: " + ignored.IgnoreText + " · " + FormatVillageStatChanges(changes));
            }
            _draw.Clear(); _hand.Clear(); _discard.Clear(); _exhausted.Clear();
            _draw.AddRange(_owned);
            if (_stage == 1)
            {
                MoveStartingCard(StartingCard.W08);
                MoveStartingCard(StartingCard.D08);
                MoveStartingCard(StartingCard.P08);
                MoveStartingCard(StartingCard.Comfort);
                MoveStartingCard(StartingCard.Consideration);
            }
            Shuffle(_draw);
            _phase = FirstStagePhase.Battle;
            _turn = 1;
            _power = TURN_POWER;
            _comfort = 0; _grace = 0; _opportunityUsed = false; _needsReorganizeDiscard = false;
            if (_stage > 1) Draw(5);
            return true;
        }

        /// <summary>선택한 카드와 기도, 은총 소비량을 검증하고 비용과 효과를 적용한다.</summary>
        public bool PlayCard(int handIndex, int prayerIndex, int graceSpent = 0)
        {
            return PlayCard(handIndex, prayerIndex, graceSpent, -1, -1);
        }

        /// <summary>대상·은총·추가 대상·버릴 카드를 검증한 뒤 카드 효과를 원자적으로 적용한다.</summary>
        public bool PlayCard(int handIndex, int prayerIndex, int graceSpent, int secondaryPrayerIndex, int discardIndex)
        {
            if ((_phase != FirstStagePhase.Battle && _phase != FirstStagePhase.BossBattle) || _needsReorganizeDiscard || handIndex < 0 || handIndex >= _hand.Count) return false;
            CardInstance instance = _hand[handIndex];
            StartingCard card = instance.Kind;
            if (card == StartingCard.Consideration || _power < instance.Cost) return false;
            bool needsTarget = RequiresTarget(card);
            if (needsTarget && !IsActivePrayerIndex(prayerIndex)) return false;
            FirstStagePrayer target = needsTarget ? _prayers[prayerIndex] : null;
            if (card == StartingCard.Insight && target.Solution > 0) return false;
            if (card == StartingCard.W02 && (!HasActiveSolution() || target.Solution > 0)) return false;
            if (card == StartingCard.W03 && target.Solution <= 0) return false;
            if (card == StartingCard.W07 && (target.Solution <= 0 || target.Solution == 4)) return false;
            if (card == StartingCard.N01 && target.HasPromise) return false;
            if (card == StartingCard.Sharing && secondaryPrayerIndex >= 0 && (!IsActivePrayerIndex(secondaryPrayerIndex) || secondaryPrayerIndex == prayerIndex)) return false;
            if (card == StartingCard.D01 && secondaryPrayerIndex >= 0 && (!IsActivePrayerIndex(secondaryPrayerIndex) || secondaryPrayerIndex == prayerIndex)) return false;
            if (card == StartingCard.D01 && secondaryPrayerIndex < 0 && HasActiveOtherPrayer(prayerIndex)) return false;
            if (card == StartingCard.Harvest && (graceSpent < 0 || graceSpent > Math.Min(3, _grace))) return false;
            if (card == StartingCard.P07 && (graceSpent < 0 || graceSpent > Math.Min(6, _grace))) return false;
            if (card == StartingCard.P06 && (graceSpent != 0 && graceSpent != 3 || graceSpent > _grace)) return false;
            if (card == StartingCard.P02 && _grace < 2) return false;
            if (card == StartingCard.D05 && _faith <= 3) return false;
            if (card == StartingCard.P04 && !IsOtherHandIndex(handIndex, discardIndex)) return false;

            _power -= instance.Cost;
            _hand.RemoveAt(handIndex);
            switch (card)
            {
                case StartingCard.W08:
                    ReduceDirect(target, 2, 0, true, true, out _);
                    if (!target.IsResolved) target.InstallSolution();
                    break;
                case StartingCard.D08:
                    ReduceDirect(target, 6, 1, true, true, out _);
                    if (!target.IsResolved) target.AddBacklash();
                    break;
                case StartingCard.P08:
                    ReduceDirect(target, 3, 2, true, true, out _);
                    if (target.IsResolved) _grace = Math.Min(6, _grace + 2);
                    break;
                case StartingCard.Comfort:
                    _comfort += 3;
                    break;
                case StartingCard.Insight:
                    target.InstallSolution();
                    break;
                case StartingCard.Sharing:
                    int shared = 0;
                    if (ReduceDirect(target, 2, 2, true, true, out _) > 0) shared++;
                    if (secondaryPrayerIndex >= 0 && ReduceDirect(_prayers[secondaryPrayerIndex], 2, 2, true, true, out _) > 0) shared++;
                    if (shared > 0) _grace = Math.Min(6, _grace + 1);
                    break;
                case StartingCard.Sever:
                    ReduceDirect(target, 5, 1, true, true, out _);
                    if (!target.IsResolved) target.AddBacklash();
                    break;
                case StartingCard.W01:
                    ReduceDirect(target, 2 + (target.Solution > 0 ? 4 : 0), 0, true, true, out _);
                    break;
                case StartingCard.W02:
                    target.InstallSolution();
                    break;
                case StartingCard.W03:
                    int activation = target.Solution;
                    for (int i = 0; i < 2 && !target.IsResolved; i++) ReducePrayer(target, activation, 0);
                    break;
                case StartingCard.W04:
                    bool hadSolution = target.Solution > 0;
                    ReduceDirect(target, 3, 0, true, true, out _);
                    if (hadSolution) _comfort += 2;
                    break;
                case StartingCard.W05:
                    Draw(3);
                    break;
                case StartingCard.W06:
                    int solutionCount = 0;
                    foreach (FirstStagePrayer prayer in _prayers) if (!prayer.IsResolved && prayer.Solution > 0) solutionCount++;
                    ReduceDirect(target, 4 + 2 * Math.Min(solutionCount, 3), 0, true, true, out _);
                    break;
                case StartingCard.W07:
                    target.InstallSolution(4);
                    break;
                case StartingCard.D01:
                    ReduceDirect(target, 8, 1, true, true, out int overflow);
                    if (overflow > 0 && secondaryPrayerIndex >= 0) ReducePrayer(_prayers[secondaryPrayerIndex], overflow, 1);
                    break;
                case StartingCard.D02:
                    ReduceDirect(target, 3, 1, true, true, out _);
                    if (target.IsResolved) _power++;
                    break;
                case StartingCard.D03:
                    ReduceDirect(target, 7, 1, true, true, out _);
                    if (!target.IsResolved) target.AddBacklash(2);
                    break;
                case StartingCard.D04:
                    int amount = 3;
                    foreach (string resolvedId in _resolvedThisTurn)
                        if (resolvedId != target.Id) { amount = 7; break; }
                    ReduceDirect(target, amount, 1, true, true, out _);
                    break;
                case StartingCard.D05:
                    _faith -= 3;
                    _power++;
                    break;
                case StartingCard.D06:
                    ReduceDirect(target, 3, 1, true, true, out _);
                    if (!target.IsResolved) target.InstallRift();
                    break;
                case StartingCard.D07:
                    ReduceDirect(target, 14, 1, true, true, out _);
                    if (!target.IsResolved) _faith = Math.Max(0, _faith - 3);
                    break;
                case StartingCard.Harvest:
                    bool graceWasEmpty = _grace == 0;
                    if (!graceWasEmpty) _grace -= graceSpent;
                    if (ReduceDirect(target, graceWasEmpty ? 2 : 2 + 2 * graceSpent, 2, true, true, out _) > 0 && graceWasEmpty)
                        _grace = Math.Min(6, _grace + 2);
                    break;
                case StartingCard.P02:
                    _grace -= 2;
                    _faith = Math.Min(MAX_FAITH, _faith + 4);
                    break;
                case StartingCard.P03:
                    int graceGained = 0;
                    FirstStagePrayer[] activeForBlessing = _prayers.FindAll(prayer => !prayer.IsResolved).ToArray();
                    foreach (FirstStagePrayer prayer in activeForBlessing)
                        if (ReduceDirect(prayer, 2, 2, true, true, out _) > 0) graceGained++;
                    _grace = Math.Min(6, _grace + graceGained);
                    break;
                case StartingCard.P04:
                    int discardAt = discardIndex > handIndex ? discardIndex - 1 : discardIndex;
                    _discard.Add(_hand[discardAt]);
                    _hand.RemoveAt(discardAt);
                    _grace = Math.Min(6, _grace + 3);
                    break;
                case StartingCard.P05:
                    ReduceDirect(target, 3, 2, true, true, out _);
                    if (target.IsResolved) _grace = Math.Min(6, _grace + 2);
                    break;
                case StartingCard.P06:
                    if (graceSpent == 3) _grace -= 3;
                    int blessingAmount = graceSpent == 3 ? 6 : 3;
                    FirstStagePrayer[] activeForMiracle = _prayers.FindAll(prayer => !prayer.IsResolved).ToArray();
                    foreach (FirstStagePrayer prayer in activeForMiracle) ReduceDirect(prayer, blessingAmount, 2, true, true, out _);
                    break;
                case StartingCard.P07:
                    _grace -= graceSpent;
                    ReduceDirect(target, 2 + 2 * graceSpent, 2, true, true, out _);
                    break;
                case StartingCard.N01:
                    target.InstallPromise();
                    break;
                case StartingCard.N02:
                    _faith = Math.Min(MAX_FAITH, _faith + 4);
                    break;
                case StartingCard.N03:
                    Draw(2);
                    _needsReorganizeDiscard = _hand.Count > 0;
                    break;
            }

            if (card == StartingCard.Insight || card == StartingCard.W05 || card == StartingCard.D05 || card == StartingCard.P02 || card == StartingCard.P04 || card == StartingCard.N02 || card == StartingCard.N03)
                _exhausted.Add(instance);
            else
                _discard.Add(instance);

            if (_faith == 0)
            {
                FinishDefeat();
                return true;
            }
            FinishIfMajorResolved();
            return true;
        }

        /// <summary>고난을 줄이고 실제 감소량의 계열 기여와 해결 시 신앙 회복을 기록한다.</summary>
        private void ReducePrayer(FirstStagePrayer prayer, int amount, int family)
        {
            int actual = prayer.Reduce(amount);
            if (family >= 0) prayer.AddContribution((Interpretation)family, actual);
            if (prayer.IsResolved && actual > 0)
            {
                prayer.ClearOnResolve();
                if (!prayer.IsMajor) _resolvedSmall.Add(prayer.Id);
                _resolvedThisTurn.Add(prayer.Id);
                _faith = Math.Min(MAX_FAITH, _faith + (prayer.IsMajor ? 2 : 1));
            }
        }

        /// <summary>카드의 직접 감소에 균열과 기회 보너스를 적용하고 실제 초과량을 반환한다.</summary>
        private int ReduceDirect(FirstStagePrayer prayer, int amount, int family, bool allowOpportunity, bool consumeRift, out int overflow)
        {
            int requested = amount;
            if (consumeRift && family == (int)Interpretation.Destruction) requested += prayer.ConsumeRiftBonus();
            if (allowOpportunity && _phase != FirstStagePhase.BossBattle && _stage >= 2 && _turn % 2 == 0 && !_opportunityUsed && prayer.IsMajor && PrayerData.Find(prayer.Id).Pattern == "기회")
            {
                requested += 2;
                _opportunityUsed = true;
            }
            int actual = prayer.Reduce(requested);
            if (family >= 0) prayer.AddContribution((Interpretation)family, actual);
            overflow = Math.Max(0, requested - actual);
            if (prayer.IsResolved && actual > 0)
            {
                prayer.ClearOnResolve();
                if (!prayer.IsMajor) _resolvedSmall.Add(prayer.Id);
                _resolvedThisTurn.Add(prayer.Id);
                _faith = Math.Min(MAX_FAITH, _faith + (prayer.IsMajor ? 2 : 1));
            }
            return actual;
        }

        /// <summary>중대 해결 시 전투를 종료하고 고유한 최다 기여 해석을 확정한다.</summary>
        private void FinishIfMajorResolved()
        {
            if (!_prayers[0].IsResolved) return;
            if (_phase == FirstStagePhase.Battle) _phase = FirstStagePhase.Resolved;
            else if (_phase == FirstStagePhase.BossBattle) _phase = FirstStagePhase.BossResolved;
            foreach (FirstStagePrayer prayer in _prayers)
                if (!prayer.IsMajor && !prayer.IsResolved) _chronicle.Add("미응답: " + prayer.Name);
        }

        /// <summary>헤아림을 소진하고 다른 손패에서 지정한 최대 두 장을 버린 뒤 같은 수를 뽑는다.</summary>
        public bool Exchange(int handIndex, int[] discardIndices)
        {
            if ((_phase != FirstStagePhase.Battle && _phase != FirstStagePhase.BossBattle) || _needsReorganizeDiscard || handIndex < 0 || handIndex >= _hand.Count || _hand[handIndex].Kind != StartingCard.Consideration || discardIndices == null || discardIndices.Length > 2) return false;
            var indices = new List<int>(discardIndices);
            indices.Sort();
            for (int i = 0; i < indices.Count; i++)
                if (indices[i] < 0 || indices[i] >= _hand.Count || indices[i] == handIndex || (i > 0 && indices[i] == indices[i - 1])) return false;
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int index = indices[i];
                _discard.Add(_hand[index]);
                _hand.RemoveAt(index);
                if (index < handIndex) handIndex--;
            }
            CardInstance exhausted = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            _exhausted.Add(exhausted);
            Draw(indices.Count);
            return true;
        }

        /// <summary>현재 미해결 기도와 위안으로 턴 종료 시 예상 신앙 손실을 반환한다.</summary>
        public int PredictLoss()
        {
            if ((_phase != FirstStagePhase.Battle && _phase != FirstStagePhase.BossBattle) || _prayers.Count == 0) return 0;
            if (!_prayers[0].IsResolved && _prayers[0].Solution >= _prayers[0].Hardship) return 0;
            int total = 0;
            foreach (FirstStagePrayer prayer in _prayers)
                if (!prayer.IsResolved && prayer.Solution < prayer.Hardship) total += TurnLoss(prayer);
            if (_phase == FirstStagePhase.BossBattle)
            {
                int forecastHeight = Math.Max(0, _bossHeight + WeatherChange(_bossWeatherIndex, false));
                if (forecastHeight >= BOSS_MAX_HEIGHT) total += HasFactPrefix("levee.state", "restored.") && !_bossFirstFloodUsed ? 3 : 6;
            }
            return Math.Max(0, total - _comfort);
        }

        /// <summary>현재 턴에 기도에 적용될 기본 손실과 악화·반동의 합을 반환한다.</summary>
        public int TurnLoss(FirstStagePrayer prayer)
        {
            if (prayer.HasPromise) return 0;
            int worsening = _phase != FirstStagePhase.BossBattle && _stage >= 2 && prayer.IsMajor && _turn >= 3 && PrayerData.Find(prayer.Id).Pattern == "악화" ? 1 : 0;
            return prayer.Loss + worsening + prayer.Backlash;
        }

        /// <summary>기도에서 선택 가능한 최다 기여 해석인지 반환한다.</summary>
        public bool CanInterpret(int prayerIndex, Interpretation interpretation)
        {
            if (prayerIndex < 0 || prayerIndex >= _prayers.Count || !Enum.IsDefined(typeof(Interpretation), interpretation)) return false;
            FirstStagePrayer prayer = _prayers[prayerIndex];
            int max = Math.Max(prayer.GetContribution(Interpretation.Wisdom), Math.Max(prayer.GetContribution(Interpretation.Destruction), prayer.GetContribution(Interpretation.Prosperity)));
            return max == 0 || prayer.GetContribution(interpretation) == max;
        }

        /// <summary>해결된 기도에서 최다 기여 계열이 하나뿐이면 그 계열을 반환한다.</summary>
        public bool TryGetUniqueInterpretation(int prayerIndex, out Interpretation interpretation)
        {
            interpretation = Interpretation.Wisdom;
            if (prayerIndex < 0 || prayerIndex >= _prayers.Count || !_prayers[prayerIndex].IsResolved) return false;
            FirstStagePrayer prayer = _prayers[prayerIndex];
            int max = Math.Max(prayer.GetContribution(Interpretation.Wisdom), Math.Max(prayer.GetContribution(Interpretation.Destruction), prayer.GetContribution(Interpretation.Prosperity)));
            if (max == 0) return false;
            int winner = -1;
            int winnerCount = 0;
            for (int i = 0; i < 3; i++)
            {
                if (prayer.GetContribution((Interpretation)i) != max) continue;
                winner = i;
                winnerCount++;
            }
            if (winnerCount != 1) return false;
            interpretation = (Interpretation)winner;
            return true;
        }

        /// <summary>해결된 기도의 응답 방식을 즉시 적용하고 사실, 마을 지표와 기록을 갱신한다.</summary>
        public bool ChooseInterpretation(int prayerIndex, Interpretation interpretation)
        {
            if ((_phase != FirstStagePhase.Battle && _phase != FirstStagePhase.BossBattle && _phase != FirstStagePhase.Resolved && _phase != FirstStagePhase.Defeated && _phase != FirstStagePhase.BossResolved) || prayerIndex < 0 || prayerIndex >= _prayers.Count || !Enum.IsDefined(typeof(Interpretation), interpretation)) return false;
            FirstStagePrayer prayer = _prayers[prayerIndex];
            if (!prayer.IsResolved || !CanInterpret(prayerIndex, interpretation) || !_interpreted.Add(prayer.Id)) return false;
            int index = (int)interpretation;
            int[] changes = ApplyVillageStatChanges(GetPrayerStatChanges(prayer.Id, interpretation));
            string[] methods = { "지혜", "파괴", "풍요" };
            PrayerData data = PrayerData.Find(prayer.Id);
            if (prayer.Id == "M03") _facts.Remove("education.plan");
            if (data != null) _facts[data.ResultKey] = data.Value(interpretation);
            else if (prayer.Id == "BOSS") _facts["flood.solution"] = new[] { "engineering", "new_channel", "regeneration" }[index];
            _facts["resolve." + prayer.Id] = new[] { "W", "D", "P" }[index];
            _chronicle.Add(prayer.Name + " — " + methods[index] + ": " + DescribeOutcome(prayer.Id, interpretation) + " · " + FormatVillageStatChanges(changes));
            if (_phase == FirstStagePhase.BossResolved && _interpreted.Count == _prayers.FindAll(item => item.IsResolved).Count) _phase = FirstStagePhase.Ascended;
            return true;
        }

        /// <summary>기도 ID와 응답 방식에 해당하는 세계 변화 문장을 반환한다.</summary>
        public static string DescribeOutcome(string id, Interpretation interpretation)
        {
            if (id == "BOSS" && Enum.IsDefined(typeof(Interpretation), interpretation))
                return new[] { "수로를 복원해 물길을 정리함", "새 물길을 내어 홍수를 돌림", "대지를 회복해 마을을 지킴" }[(int)interpretation];
            PrayerData data = PrayerData.Find(id);
            return data != null && Enum.IsDefined(typeof(Interpretation), interpretation) ? data.Outcome(interpretation) : "";
        }

        /// <summary>기도 해결 방식이 현재 마을 지표에 주는 실제 변화를 반환한다.</summary>
        public string DescribeStatChanges(string id, Interpretation interpretation)
        {
            if (!Enum.IsDefined(typeof(Interpretation), interpretation)) return "";
            int[] requested = GetPrayerStatChanges(id, interpretation);
            var actual = new int[requested.Length];
            for (int i = 0; i < requested.Length; i++)
                actual[i] = Math.Max(0, Math.Min(MAX_VILLAGE_STAT, _villageStats[i] + requested[i])) - _villageStats[i];
            return FormatVillageStatChanges(actual);
        }

        /// <summary>미선택 중대 기도가 현재 마을 지표에 줄 실제 변화를 반환한다.</summary>
        public string DescribeIgnoredStatChanges(string id)
        {
            PrayerData data = PrayerData.Find(id);
            if (data == null) return "";
            int[] requested = data.GetIgnoredStatChanges();
            var actual = new int[requested.Length];
            for (int i = 0; i < requested.Length; i++)
                actual[i] = Math.Max(0, Math.Min(MAX_VILLAGE_STAT, _villageStats[i] + requested[i])) - _villageStats[i];
            return FormatVillageStatChanges(actual);
        }

        /// <summary>기도 ID와 해석 방식의 복합 마을 지표 변화를 반환한다.</summary>
        private int[] GetPrayerStatChanges(string id, Interpretation interpretation)
        {
            if (id == "BOSS")
            {
                int[][] bossChanges =
                {
                    new[] { 2, 0, 0, 0, 2 },
                    new[] { 2, 0, 2, 0, 0 },
                    new[] { 1, 0, 0, 0, 3 }
                };
                return bossChanges[(int)interpretation];
            }
            PrayerData data = PrayerData.Find(id);
            return data == null ? new int[VILLAGE_STAT_NAMES.Length] : data.GetStatChanges(interpretation);
        }

        /// <summary>요청한 변화량을 마을 지표 상한 안에 적용하고 실제 변화량을 반환한다.</summary>
        private int[] ApplyVillageStatChanges(int[] changes)
        {
            var applied = new int[VILLAGE_STAT_NAMES.Length];
            for (int i = 0; i < applied.Length; i++)
            {
                int previous = _villageStats[i];
                _villageStats[i] = Math.Max(0, Math.Min(MAX_VILLAGE_STAT, previous + changes[i]));
                applied[i] = _villageStats[i] - previous;
            }
            return applied;
        }

        /// <summary>마을 지표 변화량 배열을 이름과 부호가 있는 간결한 문자열로 반환한다.</summary>
        private static string FormatVillageStatChanges(int[] changes)
        {
            var entries = new List<string>();
            for (int i = 0; i < changes.Length; i++)
                if (changes[i] != 0) entries.Add(VILLAGE_STAT_NAMES[i] + " " + (changes[i] > 0 ? "+" : "") + changes[i]);
            return entries.Count == 0 ? "마을 지표 변화 없음" : string.Join(" · ", entries);
        }

        /// <summary>기도에 대한 결과 해석이 이미 확정되었는지 반환한다.</summary>
        public bool IsInterpreted(int prayerIndex)
        {
            return prayerIndex >= 0 && prayerIndex < _prayers.Count && _interpreted.Contains(_prayers[prayerIndex].Id);
        }

        /// <summary>요청한 마을 지표의 현재 수치를 반환한다.</summary>
        public int GetVillageStat(VillageStat stat)
        {
            return Enum.IsDefined(typeof(VillageStat), stat) ? _villageStats[(int)stat] : 0;
        }

        /// <summary>마을 지표의 화면 표시 이름을 반환한다.</summary>
        public static string VillageStatName(VillageStat stat) => Enum.IsDefined(typeof(VillageStat), stat) ? VILLAGE_STAT_NAMES[(int)stat] : "";

        /// <summary>마을 지표의 최댓값을 반환한다.</summary>
        public static int VillageStatMaximum => MAX_VILLAGE_STAT;

        /// <summary>예상 손실을 적용하고 패배하지 않으면 손패를 버린 후 다음 턴을 시작한다.</summary>
        public bool EndTurn()
        {
            if ((_phase != FirstStagePhase.Battle && _phase != FirstStagePhase.BossBattle) || _needsReorganizeDiscard) return false;
            bool isBossBattle = _phase == FirstStagePhase.BossBattle;
            int loss = PredictLoss();
            foreach (FirstStagePrayer prayer in _prayers)
                if (!prayer.IsResolved && prayer.Solution > 0) ReducePrayer(prayer, prayer.Solution, (int)Interpretation.Wisdom);
            FinishIfMajorResolved();
            if (_phase == FirstStagePhase.Resolved || _phase == FirstStagePhase.BossResolved || _phase == FirstStagePhase.Ascended) return true;
            if (isBossBattle) ApplyBossWeather();
            _faith = Math.Max(0, _faith - loss);
            if (_faith == 0)
            {
                FinishDefeat();
                return true;
            }
            _discard.AddRange(_hand);
            _hand.Clear();
            _turn++;
            _power = TURN_POWER;
            _comfort = 0;
            _opportunityUsed = false;
            _bossChannelUsed = false;
            _resolvedThisTurn.Clear();
            foreach (FirstStagePrayer prayer in _prayers) prayer.ResetBacklash();
            Draw(5);
            return true;
        }

        /// <summary>보스 턴의 날씨를 적용하고 수위와 범람 횟수를 갱신한다.</summary>
        private void ApplyBossWeather()
        {
            int change = WeatherChange(_bossWeatherIndex, true);
            _bossHeight = Math.Max(0, _bossHeight + change);
            if (_bossHeight >= BOSS_MAX_HEIGHT)
            {
                _bossFloodCount++;
                _bossHeight = 2;
                if (HasFactPrefix("levee.state", "restored.") && !_bossFirstFloodUsed) _bossFirstFloodUsed = true;
            }
            _bossWeatherIndex = (_bossWeatherIndex + 1) % 4;
        }

        /// <summary>신앙이 0이 된 런을 패배로 끝내고 미응답 기록과 해석을 확정한다.</summary>
        private void FinishDefeat()
        {
            _phase = FirstStagePhase.Defeated;
            _needsReorganizeDiscard = false;
            foreach (FirstStagePrayer prayer in _prayers)
                if (!prayer.IsResolved) _chronicle.Add("미응답: " + prayer.Name);
        }

        /// <summary>보스 전용 물길 돌리기를 적용하고 행사력 또는 일회 무료 특전을 소비한다.</summary>
        public bool UseWaterChannel()
        {
            if (_needsReorganizeDiscard || !CanUseWaterChannel || _power < WaterChannelCost) return false;
            if (WaterChannelCost == 0) _bossChannelFreeUsed = true;
            else _power--;
            _bossHeight = Math.Max(0, _bossHeight - 2);
            _bossChannelUsed = true;
            return true;
        }

        /// <summary>기도 결과 확정 후 단계에 맞는 카드 보상 후보를 준비한다.</summary>
        public bool OpenReward()
        {
            if (_stage > MAX_STAGES || _phase != FirstStagePhase.Resolved || !AllInterpretationsChosen) return false;
            _rewardOffers.Clear();
            if (_stage == 1) _rewardOffers.AddRange(new[] { StartingCard.Insight, StartingCard.Sever, StartingCard.Harvest });
            else PrepareRewardOffers();
            _phase = FirstStagePhase.Reward;
            return true;
        }

        /// <summary>소유 카드와 전제 조건을 기준으로 세 보상 후보를 시드 추첨한다.</summary>
        private void PrepareRewardOffers()
        {
            List<StartingCard> eligible = CardData.RewardPool();
            bool hasSolution = _owned.Exists(card => card.Kind == StartingCard.Insight);
            bool hasProducer = _owned.Exists(card => card.Kind == StartingCard.Harvest || card.Kind == StartingCard.Sharing || card.Kind == StartingCard.P03 || card.Kind == StartingCard.P04 || card.Kind == StartingCard.P05);
            eligible.RemoveAll(card => (!hasSolution && (card == StartingCard.W01 || card == StartingCard.W02 || card == StartingCard.W03 || card == StartingCard.W04 || card == StartingCard.W06 || card == StartingCard.W07)) || (!hasProducer && card == StartingCard.P02));
            int[] counts = new int[3];
            foreach (CardInstance card in _owned)
            {
                int family = CardData.Find(card.Kind).Family;
                if (family >= 0) counts[family]++;
            }
            int max = Math.Max(counts[0], Math.Max(counts[1], counts[2]));
            var leaders = new List<int>();
            for (int i = 0; i < 3; i++) if (counts[i] == max) leaders.Add(i);
            int primary = leaders[_random.Next(leaders.Count)];
            var others = new List<int>();
            for (int i = 0; i < 3; i++) if (i != primary) others.Add(i);
            int secondary = others[_random.Next(others.Count)];
            DrawOffer(eligible, primary);
            DrawOffer(eligible, secondary);
            DrawOffer(eligible, -1);
        }

        /// <summary>지정 계열 후보를 우선하고 부족하면 전체 적격 후보에서 한 장을 뽑는다.</summary>
        private void DrawOffer(List<StartingCard> eligible, int family)
        {
            var pool = eligible.FindAll(card => family < 0 || CardData.Find(card).Family == family);
            if (pool.Count == 0) pool.AddRange(eligible);
            if (pool.Count == 0) return;
            StartingCard selected = pool[_random.Next(pool.Count)];
            _rewardOffers.Add(selected);
            eligible.Remove(selected);
        }

        /// <summary>표시된 카드 한 장을 추가하거나 건너뛰고 다음 상태로 이동한다.</summary>
        public bool SelectReward(StartingCard? reward)
        {
            if (_phase != FirstStagePhase.Reward) return false;
            if (reward.HasValue && !_rewardOffers.Contains(reward.Value)) return false;
            if (reward.HasValue) AddOwned(reward.Value);
            if (_stage == 2 || _stage == 4 || _stage == 6)
            {
                PrepareEvent();
                _phase = FirstStagePhase.Event;
            }
            else
                PrepareNextStage();
            return true;
        }

        /// <summary>이벤트 결과 뒤 다음 일반 스테이지 또는 보스 준비로 이동한다.</summary>
        public bool ContinueAfterEvent()
        {
            if (_phase != FirstStagePhase.EventResult) return false;
            if (_stage == MAX_STAGES) _phase = FirstStagePhase.BossPreparation;
            else PrepareNextStage();
            return true;
        }

        /// <summary>보스 준비에서 제외할 카드 인스턴스를 확정하고 휴식 뒤 보스전을 시작한다.</summary>
        public bool StartBossBattle(int[] excludedCardIds)
        {
            if (_phase != FirstStagePhase.BossPreparation || excludedCardIds == null || excludedCardIds.Length > 2 || _owned.Count - excludedCardIds.Length < 10) return false;
            var selected = new HashSet<int>();
            foreach (int id in excludedCardIds)
                if (!selected.Add(id) || !_owned.Exists(card => card.Id == id)) return false;
            _bossExcludedIds.Clear();
            foreach (int id in selected) _bossExcludedIds.Add(id);
            _faith = Math.Min(MAX_FAITH, _faith + 5);
            _prayers.Clear();
            _prayers.Add(new FirstStagePrayer("BOSS", "강이 마을을 삼키기 전에", BOSS_HARDSHIP, 2, true));
            _interpreted.Clear();
            _resolvedThisTurn.Clear();
            _draw.Clear(); _hand.Clear(); _discard.Clear(); _exhausted.Clear();
            foreach (CardInstance card in _owned) if (!_bossExcludedIds.Contains(card.Id)) _draw.Add(card);
            Shuffle(_draw);
            _stage = MAX_STAGES;
            _turn = 1;
            _power = TURN_POWER;
            _comfort = 0;
            _grace = 0;
            _opportunityUsed = false;
            _bossHeight = 0;
            _bossWeatherIndex = 0;
            _bossFloodCount = 0;
            _bossFirstFloodUsed = false;
            _bossChannelUsed = false;
            _bossChannelFreeUsed = false;
            _bossRainUsed = false;
            _needsReorganizeDiscard = false;
            _phase = FirstStagePhase.BossBattle;
            Draw(5);
            return true;
        }

        /// <summary>아직 선택하지 않은 중대 기도와 작은 기도 후보를 다음 단계에 배치한다.</summary>
        private void PrepareNextStage()
        {
            _stage++;
            var majors = new List<string> { "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09" };
            majors.RemoveAll(id => _selectedMajors.Contains(id));
            Shuffle(majors);
            _majorCandidates.Clear();
            _majorCandidates.AddRange(majors.GetRange(0, Math.Min(3, majors.Count)));
            var small = new List<string> { "L01", "L02", "L03", "L04", "L05", "L06", "L07", "L08", "L09", "L10", "L11", "L12" };
            small.RemoveAll(id => _presentedSmall.Contains(id));
            Shuffle(small);
            _smallCandidates.Clear();
            _smallCandidates.AddRange(small.GetRange(0, Math.Min(2, small.Count)));
            _presentedSmall.UnionWith(_smallCandidates);
            ResetStageSelection();
        }

        /// <summary>새 기도 선택을 위해 스테이지 임시 상태를 초기화한다.</summary>
        private void ResetStageSelection()
        {
            _prayers.Clear();
            _interpreted.Clear();
            _eventId = null;
            _eventReason = null;
            _eventResult = null;
            _phase = FirstStagePhase.Selection;
        }

        /// <summary>발전과 세계 사실로 적격 사건을 추려 사용 이력 제외 후 시드 추첨한다.</summary>
        private void PrepareEvent()
        {
            var candidates = new List<string>();
            int learning = _villageStats[(int)VillageStat.Learning];
            int military = _villageStats[(int)VillageStat.Military];
            int economy = _villageStats[(int)VillageStat.Economy];
            int food = _villageStats[(int)VillageStat.Food];
            if (learning >= 52 && (_facts.ContainsKey("child.literate") || _facts.ContainsKey("education.model"))) candidates.Add("E01");
            if (military >= 52 && (HasFact("road.control", "liberated") || HasFact("labor.rule", "freed"))) candidates.Add("E02");
            if ((economy >= 52 || food >= 52) && (_facts.ContainsKey("tree.fruit") || HasFact("food.supply", "stored") || HasFact("food.supply", "requisitioned") || HasFact("food.supply", "plentiful"))) candidates.Add("E03");
            if (learning >= 52 && IsVillageStatLeader(VillageStat.Learning)) candidates.Add("E04");
            if (military >= 52 && IsVillageStatLeader(VillageStat.Military)) candidates.Add("E05");
            if (economy >= 52 && IsVillageStatLeader(VillageStat.Economy)) candidates.Add("E06");
            if (_resolvedSmall.Count >= 2) candidates.Add("E07");
            if (HasFact("water.supply", "purified") || learning >= 54) candidates.Add("E08");
            candidates.RemoveAll(id => _usedEvents.Contains(id));
            _eventId = candidates.Count == 0 ? "E09" : candidates[_random.Next(candidates.Count)];
            _usedEvents.Add(_eventId);
            _eventReason = DescribeEventReason(_eventId);
            _eventResult = "";
        }

        /// <summary>현재 사실의 값이 기대값과 일치하는지 반환한다.</summary>
        private bool HasFact(string key, string value) => _facts.TryGetValue(key, out string actual) && actual == value;

        /// <summary>활성 세계 사실의 값이 접두 문장과 일치하는지 반환한다.</summary>
        private bool HasFactPrefix(string key, string prefix)
        {
            return _facts.TryGetValue(key, out string actual) && actual.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>지정 지표가 마을의 최고 수치와 같고 기준값 이상인지 반환한다.</summary>
        private bool IsVillageStatLeader(VillageStat stat)
        {
            int value = _villageStats[(int)stat];
            if (value < 52) return false;
            foreach (int other in _villageStats) if (other > value) return false;
            return true;
        }

        /// <summary>현재 보스 규칙에 적용되는 날씨 수위 변화를 계산한다.</summary>
        private int WeatherChange(int index, bool applyPerk)
        {
            if (index == 0 || index == 1) return 2;
            if (index == 2)
            {
                if (HasFact("rain.researched", "true") && !_bossRainUsed)
                {
                    if (applyPerk) _bossRainUsed = true;
                    return 2;
                }
                return 3;
            }
            return -2;
        }

        /// <summary>날씨 순번과 남은 특전에 맞는 날씨 안내 문장을 반환한다.</summary>
        private string WeatherText(int index)
        {
            if (index == 0) return "비 +2";
            if (index == 1) return "계속되는 비 +2";
            if (index == 2) return HasFact("rain.researched", "true") && !_bossRainUsed ? "폭우 +2 · 비 연구" : "폭우 +3";
            return "소강 -2";
        }

        /// <summary>현재 날씨를 적용한 뒤 보스의 예상 수위를 반환한다.</summary>
        private int PredictBossHeight(int height)
        {
            if (_phase != FirstStagePhase.BossBattle || _prayers.Count == 0 || _prayers[0].Solution >= _prayers[0].Hardship) return height;
            int forecast = Math.Max(0, height + WeatherChange(_bossWeatherIndex, false));
            return forecast >= BOSS_MAX_HEIGHT ? 2 : forecast;
        }

        /// <summary>현재 수위에서 날씨가 임계값을 넘길 때 발생할 범람 손실을 반환한다.</summary>
        private int PredictFloodDamage(int height)
        {
            if (_phase != FirstStagePhase.BossBattle || _prayers.Count == 0 || _prayers[0].Solution >= _prayers[0].Hardship) return 0;
            int forecast = Math.Max(0, height + WeatherChange(_bossWeatherIndex, false));
            if (forecast < BOSS_MAX_HEIGHT) return 0;
            return HasFactPrefix("levee.state", "restored.") && !_bossFirstFloodUsed ? 3 : 6;
        }

        /// <summary>물길을 돌렸을 때의 수위, 범람과 신앙 손실 예상 문장을 반환한다.</summary>
        private string GetWaterChannelForecast()
        {
            if (!CanUseWaterChannel || _power < WaterChannelCost) return "";
            if (_prayers[0].Solution >= _prayers[0].Hardship) return "해법 해결 예정 · 날씨와 범람을 건너뜁니다.";
            int afterChannel = Math.Max(0, _bossHeight - 2);
            int afterWeather = Math.Max(0, afterChannel + WeatherChange(_bossWeatherIndex, false));
            int floodDamage = afterWeather >= BOSS_MAX_HEIGHT ? (HasFactPrefix("levee.state", "restored.") && !_bossFirstFloodUsed ? 3 : 6) : 0;
            int prayerLoss = _prayers[0].HasPromise ? 0 : TurnLoss(_prayers[0]);
            int expectedLoss = Math.Max(0, prayerLoss + floodDamage - _comfort);
            int resultingHeight = floodDamage > 0 ? 2 : afterWeather;
            return "물길 후 수위 " + afterChannel + " → 날씨 뒤 " + resultingHeight + (floodDamage > 0 ? " · 범람 손실 " + floodDamage : " · 범람 없음") + " · 예상 신앙 손실 " + expectedLoss;
        }

        /// <summary>획득한 보스 대응 특전과 그 발동 이유를 표시한다.</summary>
        private string GetBossPerkSummary()
        {
            var perks = new List<string>();
            if (HasFactPrefix("levee.state", "restored.")) perks.Add("제방 복구 · 첫 범람 손실 3");
            if (HasFact("evacuation.plan", "true")) perks.Add("대피 계획 · 첫 물길 돌리기 무료");
            if (HasFact("rain.researched", "true")) perks.Add("비 연구 · 첫 폭우 수위 +2");
            return perks.Count == 0 ? "기록 특전 없음 · 범람 손실 6" : string.Join("  ·  ", perks);
        }

        /// <summary>제시한 작은 기도와 해결 비율에 따른 신의 평판을 반환한다.</summary>
        private string GetReputation()
        {
            if (_presentedSmall.Count == 0) return "아직 알려지지 않은 신";
            if (_resolvedSmall.Count * 3 >= _presentedSmall.Count * 2) return "자비로운 신";
            if (_resolvedSmall.Count * 3 >= _presentedSmall.Count) return "살피는 신";
            return "엄정한 신";
        }

        /// <summary>현재 다섯 마을 지표의 상대적인 우세로 문명의 발전 방향을 반환한다.</summary>
        private string GetCivilization()
        {
            int[] ordered = (int[])_villageStats.Clone();
            Array.Sort(ordered);
            int highestIndex = ordered.Length - 1;
            int secondHighestIndex = highestIndex - 1;
            if (ordered[highestIndex] <= 50) return "아직 미정";
            if (ordered[0] == ordered[highestIndex]) return "균형 잡힌 마을";
            int top = Array.IndexOf(_villageStats, ordered[highestIndex]);
            if (ordered[highestIndex] - ordered[secondHighestIndex] >= 8) return CIVILIZATION_NAMES[top] + "의 마을";
            if (ordered[highestIndex] - ordered[secondHighestIndex] <= 3)
            {
                int second = -1;
                for (int i = 0; i < _villageStats.Length; i++)
                    if (i != top && _villageStats[i] == ordered[secondHighestIndex]) { second = i; break; }
                return CIVILIZATION_NAMES[top] + "·" + CIVILIZATION_NAMES[second] + "의 마을";
            }
            return "여러 지표가 성장하는 마을";
        }

        /// <summary>사건 id의 성립 조건에 실제 사용된 기록과 수치를 설명한다.</summary>
        private string DescribeEventReason(string id)
        {
            switch (id)
            {
                case "E01": return "배움 " + _villageStats[(int)VillageStat.Learning] + " · " + (_facts.ContainsKey("child.literate") ? "글을 배운 아이의 기록" : "학교 설립 기록");
                case "E02": return "군사 " + _villageStats[(int)VillageStat.Military] + " · " + (HasFact("road.control", "liberated") ? "해방된 길의 기록" : "강제노역 해방 기록");
                case "E03": return "경제 " + _villageStats[(int)VillageStat.Economy] + " · " + (HasFact("tree.fruit", "true") ? "열매를 맺은 사과나무 기록" : "겨울 양식을 해결한 기록");
                case "E04": return "배움 " + _villageStats[(int)VillageStat.Learning] + "이 마을의 최고 지표입니다.";
                case "E05": return "군사 " + _villageStats[(int)VillageStat.Military] + "가 마을의 최고 지표입니다.";
                case "E06": return "경제 " + _villageStats[(int)VillageStat.Economy] + "가 마을의 최고 지표입니다.";
                case "E07": return "작은 기도 " + _resolvedSmall.Count + "개에 응답했습니다.";
                case "E08": return HasFact("water.supply", "purified") ? "우물을 정수한 기록이 있습니다." : "배움이 " + _villageStats[(int)VillageStat.Learning] + "에 도달했습니다.";
                default: return "방금 '" + _prayers[0].Name + "' 기도를 해결한 일을 주민들이 기억합니다.";
            }
        }

        /// <summary>현재 사건에서 지정한 선택의 강화 계열을 반환하고 강화가 아니면 -2를 반환한다.</summary>
        public int EventUpgradeFamily(int choice)
        {
            if (_eventId == "E09" && choice == 0) return -1;
            if ((_eventId == "E01" && choice == 1) || (_eventId == "E04" && choice == 0) || (_eventId == "E08" && choice == 1)) return 0;
            if ((_eventId == "E02" && choice == 1) || (_eventId == "E05" && choice == 0)) return 1;
            if ((_eventId == "E03" && choice == 1) || (_eventId == "E06" && choice == 0)) return 2;
            if (_eventId == "E07" && choice == 1) return 3;
            return -2;
        }

        /// <summary>사건 선택의 강화 가능 인스턴스를 반환하며 지정 계열이 없으면 해당 계열 기본 공격으로 폴백한다.</summary>
        public List<CardInstance> EventUpgradeTargets(int choice)
        {
            int family = EventUpgradeFamily(choice);
            var result = new List<CardInstance>();
            if (family == -2) return result;
            foreach (CardInstance card in _owned)
            {
                int actualFamily = CardData.Find(card.Kind).Family;
                if (!card.IsUpgraded && card.Cost >= 1 && (family == -1 || actualFamily == family || (family == 3 && actualFamily == -1))) result.Add(card);
            }
            if (result.Count == 0 && family != -1)
            {
                StartingCard fallback = family == 0 ? StartingCard.W08 : family == 1 ? StartingCard.D08 : family == 2 ? StartingCard.P08 : StartingCard.W08;
                foreach (CardInstance card in _owned) if (card.Kind == fallback && !card.IsUpgraded && card.Cost >= 1) result.Add(card);
            }
            return result;
        }

        /// <summary>사건 선택과 강화 대상 ID 또는 문명 계열을 검증해 사실과 자원을 반영한다.</summary>
        public bool ChooseEvent(int choice, int parameter = -1)
        {
            if (_phase != FirstStagePhase.Event || choice < 0 || choice > 2) return false;
            int family = EventUpgradeFamily(choice);
            string effect;
            if (family != -2)
            {
                CardInstance card = EventUpgradeTargets(choice).Find(item => item.Id == parameter);
                if (card == null || !card.Upgrade()) return false;
                effect = CardData.Find(card.Kind).Name + " #" + card.Id + " 비용 -1";
            }
            else if (_eventId == "E09" && choice == 2)
            {
                if (parameter < 0 || parameter >= _villageStats.Length) return false;
                int[] requested = new int[_villageStats.Length];
                requested[parameter] = 1;
                effect = FormatVillageStatChanges(ApplyVillageStatChanges(requested));
            }
            else effect = ApplyEventValue(choice);
            ApplyEventFact(choice);
            _eventResult = EventTitle() + " · " + EventChoiceText(choice) + " → " + effect;
            _chronicle.Add("사건: " + _eventReason + " " + _eventResult);
            _phase = FirstStagePhase.EventResult;
            return true;
        }

        /// <summary>사건의 수치 선택을 적용하고 결과 문장을 반환한다.</summary>
        private string ApplyEventValue(int choice)
        {
            int stat = -1, faith = 0;
            switch (_eventId)
            {
                case "E01": if (choice == 0) stat = (int)VillageStat.Learning; else faith = 3; break;
                case "E02": if (choice == 0) stat = (int)VillageStat.Security; else faith = 3; break;
                case "E03": if (choice == 0) stat = (int)VillageStat.Food; else faith = 3; break;
                case "E04": if (choice == 1) stat = (int)VillageStat.Learning; else faith = 3; break;
                case "E05": if (choice == 1) stat = (int)VillageStat.Military; else faith = 3; break;
                case "E06": if (choice == 1) stat = (int)VillageStat.Economy; else faith = 3; break;
                case "E07": if (choice == 0) faith = 2; else stat = (int)VillageStat.Security; break;
                case "E08": if (choice == 0) stat = (int)VillageStat.Learning; else faith = 3; break;
                case "E09": faith = 4; break;
            }
            if (stat >= 0)
            {
                int[] requested = new int[_villageStats.Length];
                requested[stat] = 1;
                return FormatVillageStatChanges(ApplyVillageStatChanges(requested));
            }
            int restored = Math.Min(MAX_FAITH - _faith, faith);
            _faith += restored;
            return "신앙 +" + restored;
        }

        /// <summary>사건 선택에 대응하는 세계 사실을 기록한다.</summary>
        private void ApplyEventFact(int choice)
        {
            switch (_eventId)
            {
                case "E01": _facts["school.access"] = new[] { "public", "research", "deferred" }[choice]; break;
                case "E02": _facts["patrol.rule"] = new[] { "community", "army", "volunteer" }[choice]; break;
                case "E03": _facts["harvest.rule"] = new[] { "shared", "stored", "relief" }[choice]; break;
                case "E04": _facts["invention.policy"] = new[] { "open", "guild", "delayed" }[choice]; break;
                case "E05": _facts["guard.policy"] = new[] { "professional", "militia", "watch" }[choice]; break;
                case "E06": _facts["trade.policy"] = new[] { "guild", "free", "feast" }[choice]; break;
                case "E07": if (choice == 0) _facts["evacuation.plan"] = "true"; else if (!HasFact("evacuation.plan", "true")) _facts["evacuation.plan"] = "false"; break;
                case "E08": if (choice == 0) _facts["rain.researched"] = "true"; else if (!HasFact("rain.researched", "true")) _facts["rain.researched"] = "false"; break;
            }
        }

        /// <summary>현재 사건의 표시 이름을 반환한다.</summary>
        public string EventTitle()
        {
            switch (_eventId)
            {
                case "E01": return "첫 학교"; case "E02": return "해방된 길"; case "E03": return "수확의 분배";
                case "E04": return "발명가의 청원"; case "E05": return "경비대의 요구"; case "E06": return "상인들의 축제";
                case "E07": return "함께 피할 길"; case "E08": return "비를 읽는 사람"; default: return "기적을 기억하는 밤";
            }
        }

        /// <summary>선행 조건에 근거해 사건의 상황을 설명한다.</summary>
        public string EventStory()
        {
            switch (_eventId)
            {
                case "E01": return "주민들이 배움을 이어갈 학교의 운영 방식을 의논합니다.";
                case "E02": return "자유를 얻은 주민들이 앞으로 길을 지킬 방법을 논의합니다.";
                case "E03": return "마을 사람들이 수확을 어떻게 나눌지 모였습니다.";
                case "E04": return "발명가들이 새 지식을 다룰 방식을 정해 달라고 청합니다.";
                case "E05": return "경비대가 마을을 지킬 조직의 방향을 묻습니다.";
                case "E06": return "상인들이 번성한 마을의 거래와 축제를 제안합니다.";
                case "E07": return "도움을 받은 주민들이 함께 피할 길을 마련하자고 합니다.";
                case "E08": return "마을 사람들이 다가올 비를 연구할 방법을 논의합니다.";
                default: return "주민들이 방금 해결된 기도를 이야기하며 기적을 기념합니다.";
            }
        }

        /// <summary>사건의 선택 번호에 따른 효과와 세계 결과를 사용자에게 보여준다.</summary>
        public string EventChoiceText(int choice)
        {
            if (choice < 0 || choice > 2) return "";
            switch (_eventId)
            {
                case "E01": return new[] { "공립 학교 · 배움 +1", "연구 학교 · 지혜 카드 강화", "학교 보류 · 신앙 +3" }[choice];
                case "E02": return new[] { "주민 순찰 · 치안 +1", "군대 순찰 · 파괴 카드 강화", "자원 순찰 · 신앙 +3" }[choice];
                case "E03": return new[] { "수확 공유 · 식량 +1", "수확 비축 · 풍요 카드 강화", "긴급 구호 · 신앙 +3" }[choice];
                case "E04": return new[] { "발명 공개 · 지혜 카드 강화", "발명 조합 · 배움 +1", "발명 보류 · 신앙 +3" }[choice];
                case "E05": return new[] { "전문 경비대 · 파괴 카드 강화", "시민 경비 · 군사 +1", "마을 감시 · 신앙 +3" }[choice];
                case "E06": return new[] { "상인 조합 · 풍요 카드 강화", "자유 무역 · 경제 +1", "축제 · 신앙 +3" }[choice];
                case "E07": return new[] { "대피 계획 마련 · 신앙 +2", "대피 계획 없음 · 공용 카드 강화", "대피 계획 없음 · 치안 +1" }[choice];
                case "E08": return new[] { "비 연구 완료 · 배움 +1", "비 연구 보류 · 지혜 카드 강화", "비 연구 보류 · 신앙 +3" }[choice];
                default: return new[] { "소유 카드 한 장 강화", "신앙 +4", "마을 지표 하나 +1" }[choice];
            }
        }

        /// <summary>손패 한도와 남은 카드 수를 지키며 count장까지 뽑는다.</summary>
        private void Draw(int count)
        {
            for (int i = 0; i < count && _hand.Count < HAND_LIMIT; i++)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0) break;
                    _draw.AddRange(_discard);
                    _discard.Clear();
                    Shuffle(_draw);
                }
                int last = _draw.Count - 1;
                _hand.Add(_draw[last]);
                _draw.RemoveAt(last);
            }
        }

        /// <summary>주어진 카드 목록을 런 시드의 난수로 제자리 셔플한다.</summary>
        private void Shuffle<T>(List<T> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int other = _random.Next(i + 1);
                T card = cards[i];
                cards[i] = cards[other];
                cards[other] = card;
            }
        }
    }
}
