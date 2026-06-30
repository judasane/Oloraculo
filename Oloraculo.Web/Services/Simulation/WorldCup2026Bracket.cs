using static Oloraculo.Web.Services.Simulation.SimulationService;

namespace Oloraculo.Web.Services.Simulation
{

    public class WorldCup2026Bracket
    {
        public sealed record KnockoutMatchSlot(
            int MatchNumber,
            KnockoutStageEnum Stage,
            DateTimeOffset KickoffUtc,
            string Venue);

        public static readonly IReadOnlyList<BracketTie> RoundOf32 =
[
        new(73, KnockoutStageEnum.RoundOf32, RunnerUp("A"), RunnerUp("B")),
        new(74, KnockoutStageEnum.RoundOf32, Winner("E"), Third("A", "B", "C", "D", "F")),
        new(75, KnockoutStageEnum.RoundOf32, Winner("F"), RunnerUp("C")),
        new(76, KnockoutStageEnum.RoundOf32, Winner("C"), RunnerUp("F")),
        new(77, KnockoutStageEnum.RoundOf32, Winner("I"), Third("C", "D", "F", "G", "H")),
        new(78, KnockoutStageEnum.RoundOf32, RunnerUp("E"), RunnerUp("I")),
        new(79, KnockoutStageEnum.RoundOf32, Winner("A"), Third("C", "E", "F", "H", "I")),
        new(80, KnockoutStageEnum.RoundOf32, Winner("L"), Third("E", "H", "I", "J", "K")),
        new(81, KnockoutStageEnum.RoundOf32, Winner("D"), Third("B", "E", "F", "I", "J")),
        new(82, KnockoutStageEnum.RoundOf32, Winner("G"), Third("A", "E", "H", "I", "J")),
        new(83, KnockoutStageEnum.RoundOf32, RunnerUp("K"), RunnerUp("L")),
        new(84, KnockoutStageEnum.RoundOf32, Winner("H"), RunnerUp("J")),
        new(85, KnockoutStageEnum.RoundOf32, Winner("B"), Third("E", "F", "G", "I", "J")),
        new(86, KnockoutStageEnum.RoundOf32, Winner("J"), RunnerUp("H")),
        new(87, KnockoutStageEnum.RoundOf32, Winner("K"), Third("D", "E", "I", "J", "L")),
        new(88, KnockoutStageEnum.RoundOf32, RunnerUp("D"), RunnerUp("G"))
];

        public static readonly IReadOnlyList<BracketTie> RoundOf16 =
        [
            new(89, KnockoutStageEnum.RoundOf16, WinnerOf(74), WinnerOf(77)),
        new(90, KnockoutStageEnum.RoundOf16, WinnerOf(73), WinnerOf(75)),
        new(91, KnockoutStageEnum.RoundOf16, WinnerOf(76), WinnerOf(78)),
        new(92, KnockoutStageEnum.RoundOf16, WinnerOf(79), WinnerOf(80)),
        new(93, KnockoutStageEnum.RoundOf16, WinnerOf(83), WinnerOf(84)),
        new(94, KnockoutStageEnum.RoundOf16, WinnerOf(81), WinnerOf(82)),
        new(95, KnockoutStageEnum.RoundOf16, WinnerOf(86), WinnerOf(88)),
        new(96, KnockoutStageEnum.RoundOf16, WinnerOf(85), WinnerOf(87))
        ];

        public static readonly IReadOnlyList<BracketTie> QuarterFinals =
        [
            new(97, KnockoutStageEnum.QuarterFinal, WinnerOf(89), WinnerOf(90)),
        new(98, KnockoutStageEnum.QuarterFinal, WinnerOf(93), WinnerOf(94)),
        new(99, KnockoutStageEnum.QuarterFinal, WinnerOf(91), WinnerOf(92)),
        new(100, KnockoutStageEnum.QuarterFinal, WinnerOf(95), WinnerOf(96))
        ];

        public static readonly IReadOnlyList<BracketTie> SemiFinals =
        [
            new(101, KnockoutStageEnum.SemiFinal, WinnerOf(97), WinnerOf(98)),
        new(102, KnockoutStageEnum.SemiFinal, WinnerOf(99), WinnerOf(100))
        ];

        public static readonly BracketTie Final = new(104, KnockoutStageEnum.Final, WinnerOf(101), WinnerOf(102));

        public static IReadOnlyList<BracketTie> KnockoutTies =>
        [
            ..RoundOf32,
        ..RoundOf16,
        ..QuarterFinals,
        ..SemiFinals,
        Final
        ];

        // Official FIFA match numbers and schedule. Match 103 is the third-place match.
        public static readonly IReadOnlyList<KnockoutMatchSlot> KnockoutSchedule =
        [
            Slot(73, KnockoutStageEnum.RoundOf32, 6, 28, 19, 0, "Los Angeles Stadium"),
            Slot(74, KnockoutStageEnum.RoundOf32, 6, 29, 20, 30, "Boston Stadium"),
            Slot(75, KnockoutStageEnum.RoundOf32, 6, 30, 1, 0, "Estadio Monterrey"),
            Slot(76, KnockoutStageEnum.RoundOf32, 6, 29, 17, 0, "Houston Stadium"),
            Slot(77, KnockoutStageEnum.RoundOf32, 6, 30, 21, 0, "New York New Jersey Stadium"),
            Slot(78, KnockoutStageEnum.RoundOf32, 6, 30, 17, 0, "Dallas Stadium"),
            Slot(79, KnockoutStageEnum.RoundOf32, 7, 1, 1, 0, "Mexico City Stadium"),
            Slot(80, KnockoutStageEnum.RoundOf32, 7, 1, 16, 0, "Atlanta Stadium"),
            Slot(81, KnockoutStageEnum.RoundOf32, 7, 2, 0, 0, "San Francisco Bay Area Stadium"),
            Slot(82, KnockoutStageEnum.RoundOf32, 7, 1, 20, 0, "Seattle Stadium"),
            Slot(83, KnockoutStageEnum.RoundOf32, 7, 2, 23, 0, "Toronto Stadium"),
            Slot(84, KnockoutStageEnum.RoundOf32, 7, 2, 19, 0, "Los Angeles Stadium"),
            Slot(85, KnockoutStageEnum.RoundOf32, 7, 3, 3, 0, "BC Place Vancouver"),
            Slot(86, KnockoutStageEnum.RoundOf32, 7, 3, 18, 0, "Miami Stadium"),
            Slot(87, KnockoutStageEnum.RoundOf32, 7, 3, 22, 0, "Kansas City Stadium"),
            Slot(88, KnockoutStageEnum.RoundOf32, 7, 4, 1, 30, "Dallas Stadium"),
            Slot(89, KnockoutStageEnum.RoundOf16, 7, 4, 21, 0, "Philadelphia Stadium"),
            Slot(90, KnockoutStageEnum.RoundOf16, 7, 4, 17, 0, "Houston Stadium"),
            Slot(91, KnockoutStageEnum.RoundOf16, 7, 5, 20, 0, "New York New Jersey Stadium"),
            Slot(92, KnockoutStageEnum.RoundOf16, 7, 6, 0, 0, "Mexico City Stadium"),
            Slot(93, KnockoutStageEnum.RoundOf16, 7, 6, 19, 0, "Dallas Stadium"),
            Slot(94, KnockoutStageEnum.RoundOf16, 7, 7, 0, 0, "Seattle Stadium"),
            Slot(95, KnockoutStageEnum.RoundOf16, 7, 7, 16, 0, "Atlanta Stadium"),
            Slot(96, KnockoutStageEnum.RoundOf16, 7, 7, 20, 0, "BC Place Vancouver"),
            Slot(97, KnockoutStageEnum.QuarterFinal, 7, 9, 20, 0, "Boston Stadium"),
            Slot(98, KnockoutStageEnum.QuarterFinal, 7, 10, 19, 0, "Los Angeles Stadium"),
            Slot(99, KnockoutStageEnum.QuarterFinal, 7, 11, 21, 0, "Miami Stadium"),
            Slot(100, KnockoutStageEnum.QuarterFinal, 7, 12, 1, 0, "Kansas City Stadium"),
            Slot(101, KnockoutStageEnum.SemiFinal, 7, 14, 19, 0, "Dallas Stadium"),
            Slot(102, KnockoutStageEnum.SemiFinal, 7, 15, 19, 0, "Atlanta Stadium"),
            Slot(104, KnockoutStageEnum.Final, 7, 19, 19, 0, "New York New Jersey Stadium")
        ];

        private static readonly IReadOnlyDictionary<string, int> OfficialRoundOf32Pairs =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [Pair("south-africa", "canada")] = 73,
                [Pair("germany", "paraguay")] = 74,
                [Pair("netherlands", "morocco")] = 75,
                [Pair("brazil", "japan")] = 76,
                [Pair("france", "sweden")] = 77,
                [Pair("ivory-coast", "norway")] = 78,
                [Pair("mexico", "ecuador")] = 79,
                [Pair("england", "congo-dr")] = 80,
                [Pair("united-states", "bosnia-and-herzegovina")] = 81,
                [Pair("belgium", "senegal")] = 82,
                [Pair("portugal", "croatia")] = 83,
                [Pair("spain", "austria")] = 84,
                [Pair("switzerland", "algeria")] = 85,
                [Pair("argentina", "cape-verde")] = 86,
                [Pair("colombia", "ghana")] = 87,
                [Pair("australia", "egypt")] = 88
            };

        public static int? ResolveMatchNumber(
            KnockoutStageEnum stage,
            DateTimeOffset? kickoffUtc,
            string? venue,
            string homeTeamId,
            string awayTeamId)
        {
            if (stage == KnockoutStageEnum.RoundOf32 && OfficialRoundOf32Pairs.TryGetValue(Pair(homeTeamId, awayTeamId), out var roundOf32Match))
                return roundOf32Match;

            var candidates = KnockoutSchedule.Where(slot => slot.Stage == stage).ToList();
            if (kickoffUtc.HasValue)
            {
                var byKickoff = candidates
                    .Where(slot => Math.Abs((slot.KickoffUtc - kickoffUtc.Value.ToUniversalTime()).TotalMinutes) <= 5)
                    .ToList();
                if (byKickoff.Count == 1)
                    return byKickoff[0].MatchNumber;
            }

            if (!string.IsNullOrWhiteSpace(venue))
            {
                var normalizedVenue = Normalize(venue);
                var byVenue = candidates.Where(slot =>
                    Normalize(slot.Venue).Contains(normalizedVenue, StringComparison.Ordinal) ||
                    normalizedVenue.Contains(Normalize(slot.Venue), StringComparison.Ordinal)).ToList();
                if (byVenue.Count == 1)
                    return byVenue[0].MatchNumber;
            }

            return null;
        }

        public static IReadOnlyDictionary<int, string> AssignThirdPlaceGroups(IReadOnlyCollection<string> qualifiedThirdGroups)
        {
            if (qualifiedThirdGroups.Count != 8)
                throw new InvalidOperationException($"El cuadro 2026 requiere exactamente ocho grupos con terceros clasificados, pero recibió {qualifiedThirdGroups.Count}.");

            var qualified = qualifiedThirdGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var slots = RoundOf32
                .Where(t => t.Home.Kind == BracketSlotKindEnum.GroupThird || t.Away.Kind == BracketSlotKindEnum.GroupThird)
                .Select(t => new ThirdSlot(t.Id, ThirdOptions(t)))
                .OrderBy(s => s.Options.Count(g => qualified.Contains(g)))
                .ThenBy(s => s.TieId)
                .ToList();

            var assigned = new Dictionary<int, string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TryAssign(0))
                throw new InvalidOperationException($"No se pudieron asignar los grupos de terceros {string.Join(",", qualifiedThirdGroups.Order())} a los cruces oficiales de 2026.");

            return assigned;

            bool TryAssign(int index)
            {
                if (index == slots.Count)
                    return true;

                var slot = slots[index];
                foreach (var group in slot.Options.Where(qualified.Contains).OrderBy(GroupOrder))
                {
                    if (!used.Add(group))
                        continue;

                    assigned[slot.TieId] = group;
                    if (TryAssign(index + 1))
                        return true;

                    assigned.Remove(slot.TieId);
                    used.Remove(group);
                }

                return false;
            }
        }

        private static BracketSlot Winner(string group) => new(BracketSlotKindEnum.GroupWinner, Group: group);
        private static BracketSlot RunnerUp(string group) => new(BracketSlotKindEnum.GroupRunnerUp, Group: group);
        private static BracketSlot Third(params string[] groups) => new(BracketSlotKindEnum.GroupThird, ThirdPlaceGroupOptions: groups);
        private static BracketSlot WinnerOf(int tieId) => new(BracketSlotKindEnum.WinnerOfTie, TieId: tieId);

        private static IReadOnlyList<string> ThirdOptions(BracketTie tie) =>
            tie.Home.Kind == BracketSlotKindEnum.GroupThird ? tie.Home.ThirdPlaceGroupOptions ?? [] : tie.Away.ThirdPlaceGroupOptions ?? [];

        private static int GroupOrder(string group) => group.Length == 0 ? int.MaxValue : group[0] - 'A';

        private sealed record ThirdSlot(int TieId, IReadOnlyList<string> Options);

        private static KnockoutMatchSlot Slot(int matchNumber, KnockoutStageEnum stage, int month, int day, int hour, int minute, string venue) =>
            new(matchNumber, stage, new DateTimeOffset(2026, month, day, hour, minute, 0, TimeSpan.Zero), venue);

        private static string Pair(string left, string right) =>
            string.CompareOrdinal(left, right) <= 0 ? $"{left}|{right}" : $"{right}|{left}";

        private static string Normalize(string value) =>
            new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
