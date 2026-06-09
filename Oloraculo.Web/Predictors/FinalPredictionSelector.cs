using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public static class FinalPredictionSelector
    {
        private const double RankingBiasWeight = 0.15;
        private const string EloPredictorName = "Elo";
        private const string FifaPredictorName = "FIFA ranking";

        public static MatchPrediction Select(IReadOnlyList<MatchPrediction> ladder)
        {
            if (ladder.Count == 0)
                return EmptyFinal();

            var ordered = ladder.OrderBy(p => p.PredictorPriority).ToList();
            var selected = ordered.LastOrDefault(p => !p.Degraded) ?? ordered.First();
            var skippedHigher = ordered
                .Where(p => p.PredictorPriority > selected.PredictorPriority && p.Degraded)
                .OrderByDescending(p => p.PredictorPriority)
                .ToList();
            var rankingBias = TryBuildRankingBias(ordered, selected);

            var drivers = new List<string>
            {
                $"Selected {selected.PredictorName} as the highest usable rung."
            };
            drivers.AddRange(skippedHigher.Select(p => $"Skipped {p.PredictorName}: {Reason(p)}"));
            drivers.AddRange(selected.Drivers);
            if (rankingBias is not null)
            {
                drivers.Add(
                    $"Applied {RankingBiasWeight:P0} Elo/FIFA calibration toward {rankingBias.ConsensusTopPick} because both ranking models agreed against {selected.PredictorName}.");
            }

            var sources = selected.Sources
                .Concat(rankingBias?.Sources ?? [])
                .Concat([new SourceMetadata("model ladder", "derived", Notes: selected.PredictorName)])
                .Distinct()
                .ToList();

            return new MatchPrediction
            {
                PredictorName = "Final Oracle",
                PredictorPriority = selected.PredictorPriority,
                FixtureId = selected.FixtureId,
                HomeTeamId = selected.HomeTeamId,
                AwayTeamId = selected.AwayTeamId,
                Outcome = rankingBias?.Outcome ?? selected.Outcome,
                ExpectedHomeGoals = selected.ExpectedHomeGoals,
                ExpectedAwayGoals = selected.ExpectedAwayGoals,
                Scoreline = selected.Scoreline,
                MostLikelyScore = selected.MostLikelyScore,
                Explanation = BuildExplanation(selected, skippedHigher, rankingBias),
                Drivers = drivers,
                FeaturesUsed = selected.FeaturesUsed,
                FeaturesMissing = selected.FeaturesMissing,
                Sources = sources,
                Degraded = selected.Degraded
            };
        }

        private static RankingBias? TryBuildRankingBias(IReadOnlyList<MatchPrediction> ordered, MatchPrediction selected)
        {
            var elo = ordered.LastOrDefault(p => p.PredictorName == EloPredictorName && !p.Degraded);
            var fifa = ordered.LastOrDefault(p => p.PredictorName == FifaPredictorName && !p.Degraded);
            if (elo is null || fifa is null)
                return null;

            var consensusTopPick = elo.Outcome.TopPick;
            if (consensusTopPick != fifa.Outcome.TopPick || consensusTopPick == selected.Outcome.TopPick)
                return null;

            var consensus = new OutcomeProbabilities(
                (elo.Outcome.HomeWin + fifa.Outcome.HomeWin) / 2.0,
                (elo.Outcome.Draw + fifa.Outcome.Draw) / 2.0,
                (elo.Outcome.AwayWin + fifa.Outcome.AwayWin) / 2.0).Normalize();

            var selectedWeight = 1.0 - RankingBiasWeight;
            var outcome = new OutcomeProbabilities(
                selected.Outcome.HomeWin * selectedWeight + consensus.HomeWin * RankingBiasWeight,
                selected.Outcome.Draw * selectedWeight + consensus.Draw * RankingBiasWeight,
                selected.Outcome.AwayWin * selectedWeight + consensus.AwayWin * RankingBiasWeight).Normalize();

            return new RankingBias(
                outcome,
                consensusTopPick,
                elo.Sources.Concat(fifa.Sources).ToList());
        }

        private static string BuildExplanation(
            MatchPrediction selected,
            IReadOnlyList<MatchPrediction> skippedHigher,
            RankingBias? rankingBias)
        {
            var rankingSentence = rankingBias is null
                ? ""
                : $" Applied a {RankingBiasWeight:P0} Elo/FIFA calibration toward {rankingBias.ConsensusTopPick}.";

            if (skippedHigher.Count == 0)
                return $"Final Oracle selected {selected.PredictorName}, the highest usable rung. {selected.Explanation}{rankingSentence}";

            var skipped = string.Join("; ", skippedHigher.Select(p => $"{p.PredictorName} {Reason(p)}"));
            return $"Final Oracle selected {selected.PredictorName} because {skipped}. {selected.Explanation}{rankingSentence}";
        }

        private static string Reason(MatchPrediction prediction)
        {
            if (prediction.FeaturesMissing.Count == 0)
                return "was degraded";

            return $"was degraded: missing {string.Join(", ", prediction.FeaturesMissing)}";
        }

        private static MatchPrediction EmptyFinal() => new()
        {
            PredictorName = "Final Oracle",
            PredictorPriority = 0,
            Outcome = OutcomeProbabilities.Uniform,
            Explanation = "Final Oracle had no ladder predictions, so it returned the baseline.",
            Drivers = ["No ladder predictions were available."],
            FeaturesMissing = ["ladder predictions"],
            Sources = [new SourceMetadata("model ladder", "derived")],
            Degraded = true
        };

        private sealed record RankingBias(
            OutcomeProbabilities Outcome,
            string ConsensusTopPick,
            IReadOnlyList<SourceMetadata> Sources);
    }
}
