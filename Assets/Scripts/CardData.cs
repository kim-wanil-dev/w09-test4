namespace NamelessGod
{
    public sealed class CardInstance
    {
        private readonly int _id;
        private readonly StartingCard _kind;
        private bool _upgraded;

        public int Id => _id;
        public StartingCard Kind => _kind;
        public bool IsUpgraded => _upgraded;
        public int Cost => CardData.Find(_kind).Cost - (_upgraded ? 1 : 0);

        /// <summary>고유 ID와 카드 정의를 가진 런 전용 카드를 만든다.</summary>
        internal CardInstance(int id, StartingCard kind) { _id = id; _kind = kind; }

        /// <summary>비용 1 이상인 미강화 카드의 비용을 1 낮춘다.</summary>
        internal bool Upgrade()
        {
            if (_upgraded || Cost < 1) return false;
            _upgraded = true;
            return true;
        }
    }

    internal sealed class CardData
    {
        private static readonly CardData[] CARDS =
        {
            new CardData(StartingCard.W08,"계시의 손길",0,1,"단일 고난 2 감소, 남으면 해법 2 설치"),
            new CardData(StartingCard.D08,"신벌의 벼락",1,1,"단일 고난 6 감소, 남으면 이번 턴 반동 +1"),
            new CardData(StartingCard.P08,"풍요의 축복",2,1,"단일 고난 3 감소, 해결 시 은총 +2"),
            new CardData(StartingCard.Comfort,"위안",-1,1,"위안 +3"),
            new CardData(StartingCard.Consideration,"헤아림",-1,0,"다른 손패 0~2장 교환, 소진"),
            new CardData(StartingCard.Insight,"원인 규명",0,2,"해법 없는 기도에 해법 2 설치"),
            new CardData(StartingCard.Sharing,"나눔",2,1,"서로 다른 기도 최대 2개에 각각 2 감소, 실제 감소 시 은총 +1"),
            new CardData(StartingCard.Sever,"단절",1,1,"단일 5 감소, 남으면 이번 턴 손실 +1"),
            new CardData(StartingCard.W01,"통찰",0,1,"단일 2 감소, 해법이 있으면 추가 4 감소"),
            new CardData(StartingCard.W02,"지식 전수",0,1,"해법 보유 기도가 있을 때 다른 기도에 해법 2 설치"),
            new CardData(StartingCard.W03,"빠른 깨달음",0,1,"대상 해법을 즉시 2회 발동"),
            new CardData(StartingCard.W04,"원리의 적용",0,1,"단일 3 감소, 해법이 있었다면 위안 +2"),
            new CardData(StartingCard.W05,"깊은 사색",0,1,"3장 드로우, 소진"),
            new CardData(StartingCard.W06,"지식의 집대성",0,2,"단일 4 + 해법 보유 기도 수당 2 감소, 최대 10"),
            new CardData(StartingCard.W07,"완성된 해법",0,2,"기존 해법의 발동량을 4로 변경"),
            new CardData(StartingCard.D01,"붕괴의 연쇄",1,2,"단일 8 감소, 초과량을 다른 기도 하나에 전달"),
            new CardData(StartingCard.D02,"해방",1,1,"단일 3 감소, 해결 시 행사력 +1"),
            new CardData(StartingCard.D03,"강행",1,1,"단일 7 감소, 남으면 이번 턴 손실 +2"),
            new CardData(StartingCard.D04,"쇄도",1,1,"이번 턴 다른 기도 해결 시 7, 아니면 3 감소"),
            new CardData(StartingCard.D05,"신성의 희생",1,0,"신앙 3 소비 후 행사력 +1, 소진; 신앙 3 이하면 사용 불가"),
            new CardData(StartingCard.D06,"균열",1,1,"단일 3 감소, 남으면 균열 설치"),
            new CardData(StartingCard.D07,"종언",1,3,"단일 14 감소, 남으면 즉시 신앙 -3"),
            new CardData(StartingCard.Harvest,"결실",2,1,"은총 0이면 단일 2 감소 후 은총 +2, 아니면 0~3 소비해 추가 감소"),
            new CardData(StartingCard.P02,"평안",2,0,"은총 2 소비, 신앙 +4, 소진"),
            new CardData(StartingCard.P03,"공동의 축복",2,2,"모든 활성 기도 2 감소, 감소한 기도 수만큼 은총 획득"),
            new CardData(StartingCard.P04,"봉헌",2,1,"다른 손패 1장 버리고 은총 +3, 소진"),
            new CardData(StartingCard.P05,"은혜의 순환",2,1,"단일 3 감소, 해결 시 은총 +2"),
            new CardData(StartingCard.P06,"풍요의 기적",2,2,"모든 기도 3 감소, 은총 3 추가 소비 시 6 감소"),
            new CardData(StartingCard.P07,"한 사람을 위한 기적",2,2,"은총 0~6 소비, 단일 2+2×소비량 감소"),
            new CardData(StartingCard.N01,"응답의 약속",-1,1,"대상의 이번 턴 신앙 감소를 반동 포함 0으로 설정"),
            new CardData(StartingCard.N02,"신앙의 불씨",-1,1,"신앙 +4, 소진"),
            new CardData(StartingCard.N03,"재정비",-1,0,"2장 드로우 후 다른 손패 1장 버림, 소진")
        };

        public StartingCard Kind { get; private set; }
        public string Name { get; private set; }
        public int Family { get; private set; }
        public int Cost { get; private set; }
        public string Description { get; private set; }

        /// <summary>카드 식별자, 계열, 비용과 표시 설명을 설정한다.</summary>
        private CardData(StartingCard kind, string name, int family, int cost, string description)
        {
            Kind = kind; Name = name; Family = family; Cost = cost; Description = description;
        }

        /// <summary>카드 종류에 대응하는 정의를 반환한다.</summary>
        public static CardData Find(StartingCard kind)
        {
            foreach (CardData data in CARDS) if (data.Kind == kind) return data;
            return null;
        }

        /// <summary>시작 덱 세 종류를 제외한 모든 보상 정의를 반환한다.</summary>
        public static System.Collections.Generic.List<StartingCard> RewardPool()
        {
            var result = new System.Collections.Generic.List<StartingCard>();
            foreach (CardData card in CARDS)
                if (card.Kind != StartingCard.W08 && card.Kind != StartingCard.D08 && card.Kind != StartingCard.P08 && card.Kind != StartingCard.Comfort && card.Kind != StartingCard.Consideration) result.Add(card.Kind);
            return result;
        }
    }
}
