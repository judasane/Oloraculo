namespace Oloraculo.Web.Models
{
    public sealed record FullFixtureSnapshotSummary(
        int Id,
        DateTimeOffset CreatedAt,
        string ModelName,
        string InputSummaryHash,
        int FixtureCount,
        string? Error)
    {
        public bool IsValid => string.IsNullOrWhiteSpace(Error);
    }
}
