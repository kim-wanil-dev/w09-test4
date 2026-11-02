using System;
using System.Collections.Generic;

namespace NamelessGod
{
    internal sealed class PrayerData
    {
        private const int VILLAGE_STAT_COUNT = 5;
        private static readonly Dictionary<string, int[][]> STAT_CHANGES = new Dictionary<string, int[][]>
        {
            { "M01", new[] { new[] { 0, 1, 0, 0, 2 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 3 } } },
            { "M02", new[] { new[] { 2, 1, 0, 0, 0 }, new[] { 1, 0, 2, 0, 0 }, new[] { 0, 2, 0, 0, 0 } } },
            { "M03", new[] { new[] { 0, -1, 0, 3, 0 }, new[] { -1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 2, 0 } } },
            { "M04", new[] { new[] { 1, 1, 0, 0, 0 }, new[] { -1, 0, 2, 0, 0 }, new[] { 0, 2, -1, 0, 0 } } },
            { "M05", new[] { new[] { 0, 1, 0, 0, 3 }, new[] { 0, -2, 2, 0, 0 }, new[] { 0, 1, 0, 0, 3 } } },
            { "M06", new[] { new[] { 2, 0, 0, 0, 2 }, new[] { 2, 0, 2, 0, 0 }, new[] { 0, 2, 0, 0, 2 } } },
            { "M07", new[] { new[] { 0, 2, 0, 1, 0 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, -1, 0, 0, 2 } } },
            { "M08", new[] { new[] { 1, 0, 0, 2, 0 }, new[] { 1, 2, 0, 0, 0 }, new[] { 0, 3, 0, 0, 1 } } },
            { "M09", new[] { new[] { 2, 0, 0, 1, 0 }, new[] { 2, 0, 2, 0, 0 }, new[] { 1, 0, 0, 0, 2 } } },
            { "L01", new[] { new[] { 0, 0, 0, 2, 0 }, new[] { -1, 0, 1, 0, 0 }, new[] { 0, 0, 0, 1, 0 } } },
            { "L02", new[] { new[] { 0, 1, 0, 0, 2 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 2 } } },
            { "L03", new[] { new[] { 1, 0, 1, 0, 0 }, new[] { 1, 0, 2, 0, 0 }, new[] { 0, 1, -1, 0, 0 } } },
            { "L04", new[] { new[] { 0, 0, 0, 2, 0 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 0 } } },
            { "L05", new[] { new[] { 0, 0, 0, 2, 0 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 0 } } },
            { "L06", new[] { new[] { 0, 0, 0, 1, 2 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 2 } } },
            { "L07", new[] { new[] { 1, 1, 0, 0, 0 }, new[] { 2, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 1 } } },
            { "L08", new[] { new[] { 0, 1, 0, 2, 0 }, new[] { 1, 0, 2, 0, 0 }, new[] { 0, 1, 0, 0, 1 } } },
            { "L09", new[] { new[] { 1, 0, 0, 1, 0 }, new[] { 2, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 1 } } },
            { "L10", new[] { new[] { 0, 1, 0, 1, 0 }, new[] { 1, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 1 } } },
            { "L11", new[] { new[] { 2, 1, 0, 0, 0 }, new[] { 1, 0, 2, 0, 0 }, new[] { 0, 1, 0, 0, 1 } } },
            { "L12", new[] { new[] { 1, 1, 0, 0, 0 }, new[] { 2, -1, 0, 0, 0 }, new[] { 0, 1, 0, 0, 2 } } }
        };
        private static readonly Dictionary<string, int[]> IGNORED_STAT_CHANGES = new Dictionary<string, int[]>
        {
            { "M01", new[] { 0, -2, 0, 0, -2 } },
            { "M02", new[] { -2, -2, 0, 0, 0 } },
            { "M03", new[] { 0, 0, 0, -2, 0 } },
            { "M04", new[] { 0, 1, 1, 0, 0 } },
            { "M05", new[] { 0, -1, 0, 0, -2 } },
            { "M06", new[] { -2, -1, 0, 0, 0 } },
            { "M07", new[] { 0, -1, 0, 0, 0 } },
            { "M08", new[] { 0, -2, 0, 0, 0 } },
            { "M09", new[] { -1, -1, 0, 0, 0 } }
        };
        private static readonly PrayerData[] MAJOR =
        {
            new PrayerData("M01", "막힌 우물을 열어주세요", "안정", "water.supply", "scarce", "공동 우물 일부 사용 중단", "purified", "opened", "abundant", "우물을 정수함", "우물을 열어 물길을 되찾음", "우물의 물을 풍부하게 함"),
            new PrayerData("M02", "산적에게 길을 되찾아주세요", "악화", "road.control", "bandit", "산적 때문에 길 일부 폐쇄", "negotiated", "liberated", "supplied", "산적과 협상함", "길을 해방함", "길을 유지할 물자를 지원함"),
            new PrayerData("M03", "학교를 세워주세요", "안정", "education.plan", "deferred", "학교 설립 기회 보류", "public", "independent", "supported", "공립 교육을 마련함", "자율적인 교육을 엶", "교육에 필요한 자원을 지원함", "education.model"),
            new PrayerData("M04", "강제노역에서 풀려나게 해주세요", "기회", "labor.rule", "existing", "강제노역의 현상 유지", "reformed", "freed", "resettled", "노동 규칙을 개혁함", "감금 시설을 부수어 주민을 해방함", "주민의 정착을 도움"),
            new PrayerData("M05", "겨울 양식을 마련해주세요", "악화", "food.supply", "strained", "배급 축소", "stored", "requisitioned", "plentiful", "식량 저장법을 마련함", "창고 물자를 징발함", "먹을 것을 풍부하게 함"),
            new PrayerData("M06", "제방을 복구해주세요", "안정", "levee.state", "unrepaired", "제방 복구 기회 보류", "restored.W", "restored.D", "restored.P", "지혜로 제방을 복구함", "파괴의 권능으로 제방을 복구함", "풍요의 권능으로 제방을 복구함"),
            new PrayerData("M07", "부당한 세금을 조정해주세요", "기회", "tax.rule", "existing", "세금 규칙 현상 유지", "audited", "abolished", "subsidized", "세금을 조사함", "부당한 세금을 폐지함", "주민에게 보조금을 지원함"),
            new PrayerData("M08", "상인들의 시장을 열어주세요", "안정", "market.state", "deferred", "시장 개설 기회 보류", "regulated", "opened", "flourishing", "시장 규칙을 마련함", "막힌 시장을 개방함", "거래가 번성하게 도움"),
            new PrayerData("M09", "번지는 들불을 막아주세요", "악화", "forest.state", "damaged", "숲 일부 소실", "managed", "firebreak", "revived", "숲을 관리해 불길을 잡음", "방화선을 만듦", "숲을 되살림")
        };
        private static readonly PrayerData[] SMALL =
        {
            Small("L01", "아이에게 글을 알려주세요", 4, "child.literate", "읽는 원리를 가르침", "배움을 막는 봉인을 깸", "배움의 시간을 마련함"),
            Small("L02", "오래된 사과나무에 열매를", 5, "tree.fruit", "접목법을 전수함", "병든 가지를 제거함", "열매를 맺게 함"),
            Small("L03", "목동의 양을 찾아주세요", 4, "shepherd.reunited", "발자국을 해석함", "막힌 길을 엶", "먹이로 양을 돌아오게 함"),
            Small("L04", "노인의 기억을 지켜주세요", 6, "elder.memory", "기억을 기록함", "기억을 막는 저주를 해제함", "기력을 회복함"),
            Small("L05", "아이의 악기를 고쳐주세요", 4, "child.instrument", "조율법을 전수함", "뒤틀린 부분을 제거함", "재료를 되살림"),
            Small("L06", "병든 약초를 살려주세요", 5, "garden.herb", "병의 원인을 규명함", "오염을 제거함", "생명력을 공급함"),
            Small("L07", "이웃에게 전할 편지를", 4, "letter.delivered", "길을 안내함", "막힌 통로를 개방함", "여정을 지원함"),
            Small("L08", "부러진 물레를 고쳐주세요", 5, "wheel.repaired", "구조를 복원함", "고착된 축을 해제함", "재료를 공급함"),
            Small("L09", "돌아오지 않는 반딧불을", 4, "firefly.returned", "서식 환경을 회복함", "오염원을 제거함", "생태를 회복함"),
            Small("L10", "가족의 무덤을 돌봐주세요", 5, "grave.restored", "위치와 기록을 복원함", "덮인 잔해를 제거함", "주변 생명을 회복함"),
            Small("L11", "빗새는 지붕을 고쳐주세요", 6, "roof.repaired", "보수법을 전수함", "위험한 부분을 철거함", "자재를 제공함"),
            Small("L12", "길 잃은 여행자를 도와주세요", 5, "traveler.arrived", "길을 안내함", "장애물을 제거함", "식량과 쉼을 제공함")
        };

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Pattern { get; private set; }
        public string IgnoreKey { get; private set; }
        public string IgnoreValue { get; private set; }
        public string IgnoreText { get; private set; }
        public string ResultKey { get; private set; }
        public int Hardship { get; private set; }
        private readonly string[] _values;
        private readonly string[] _outcomes;

        /// <summary>기도의 선택 결과, 유형, 세계 상태와 세 응답 방식을 설정한다.</summary>
        private PrayerData(string id, string name, string pattern, string ignoreKey, string ignoreValue, string ignoreText, string wisdomValue, string destructionValue, string prosperityValue, string wisdom, string destruction, string prosperity, string resultKey = null, int hardship = 0)
        {
            Id = id; Name = name; Pattern = pattern; IgnoreKey = ignoreKey; IgnoreValue = ignoreValue; IgnoreText = ignoreText;
            ResultKey = resultKey ?? ignoreKey; Hardship = hardship;
            _values = new[] { wisdomValue, destructionValue, prosperityValue };
            _outcomes = new[] { wisdom, destruction, prosperity };
        }

        /// <summary>작은 기도의 고난, 주민 상태와 세 응답 설명을 정의한다.</summary>
        private static PrayerData Small(string id, string name, int hardship, string key, string wisdom, string destruction, string prosperity)
        {
            return new PrayerData(id, name, "", key, "", "", "true", "true", "true", wisdom, destruction, prosperity, key, hardship);
        }

        /// <summary>ID에 해당하는 기도 정의를 반환하고 없으면 null을 반환한다.</summary>
        public static PrayerData Find(string id)
        {
            return Array.Find(MAJOR, prayer => prayer.Id == id) ?? Array.Find(SMALL, prayer => prayer.Id == id);
        }

        /// <summary>응답 방식에 해당하는 상태 값을 반환한다.</summary>
        public string Value(Interpretation interpretation) => _values[(int)interpretation];

        /// <summary>응답 방식에 해당하는 설명을 반환한다.</summary>
        public string Outcome(Interpretation interpretation) => _outcomes[(int)interpretation];

        /// <summary>응답 방식에 따른 다섯 마을 지표 변화량을 반환한다.</summary>
        public int[] GetStatChanges(Interpretation interpretation)
        {
            return STAT_CHANGES.TryGetValue(Id, out int[][] changes) ? (int[])changes[(int)interpretation].Clone() : new int[VILLAGE_STAT_COUNT];
        }

        /// <summary>기도를 외면했을 때 적용할 마을 지표 변화량을 반환한다.</summary>
        public int[] GetIgnoredStatChanges()
        {
            return IGNORED_STAT_CHANGES.TryGetValue(Id, out int[] changes) ? (int[])changes.Clone() : new int[VILLAGE_STAT_COUNT];
        }
    }
}
