namespace Oloraculo.Web.Models
{
    public sealed record MatchSnapshotLoadResult(MatchPredictionResult? Prediction, string? Error)
    {
        public bool IsValid => Prediction is not null && string.IsNullOrWhiteSpace(Error);
    }
}
