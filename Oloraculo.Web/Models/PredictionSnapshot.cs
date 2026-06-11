namespace Oloraculo.Web.Models
{
    public class PredictionSnapshot
    {
        public int Id { get; set; }
        public string Kind { get; set; } = "match";
        public int? BatchId { get; set; }
        public string? FixtureId { get; set; }
        public string ModelName { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public string InputSummaryHash { get; set; }
        public string PayloadJson { get; set; }
        public string Explanation { get; set; }
        public double? HomeWin { get; set; }
        public double? Draw { get; set; }
        public double? AwayWin { get; set; }


    }
}
