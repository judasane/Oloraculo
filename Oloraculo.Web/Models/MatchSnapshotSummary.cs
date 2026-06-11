namespace Oloraculo.Web.Models
{
    public sealed record MatchSnapshotSummary(
        int Id,
        DateTimeOffset CreatedAt,
        string ModelName,
        string FixtureId,
        string InputSummaryHash,
        int? BatchId,
        string? Error)
    {
        public bool IsValid => string.IsNullOrWhiteSpace(Error);
    }
}
