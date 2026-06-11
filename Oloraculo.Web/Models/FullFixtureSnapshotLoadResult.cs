namespace Oloraculo.Web.Models
{
    public sealed record FullFixtureSnapshotLoadResult(IReadOnlyList<MatchPredictionResult> Predictions, string? Error)
    {
        public bool IsValid => Predictions.Count > 0 && string.IsNullOrWhiteSpace(Error);
    }
}
