namespace Oloraculo.Web.Models.CsvModels
{
    public class KnockoutResultCsvRow
    {
        public int MatchNumber { get; set; }
        public string Stage { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public int? HomePenaltyGoals { get; set; }
        public int? AwayPenaltyGoals { get; set; }
        public string WinnerTeam { get; set; } = "";
        public string Status { get; set; } = "Finished";
        public string Source { get; set; } = "";
        public string SourceUpdatedAt { get; set; } = "";
    }
}
