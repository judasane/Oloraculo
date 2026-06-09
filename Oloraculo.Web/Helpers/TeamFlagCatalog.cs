using System.Globalization;

namespace Oloraculo.Web.Helpers
{
    public static class TeamFlagCatalog
    {
        private static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bosnia-and-herzegovina"] = "ba",
            ["cape-verde"] = "cv",
            ["congo-dr"] = "cd",
            ["curacao"] = "cw",
            ["czechia"] = "cz",
            ["england"] = "gb-eng",
            ["ivory-coast"] = "ci",
            ["south-korea"] = "kr",
            ["scotland"] = "gb-sct",
            ["turkey"] = "tr",
            ["united-states"] = "us"
        };

        private static readonly Lazy<IReadOnlyDictionary<string, string>> RegionCodesByTeamId = new(CreateRegionCodeLookup);

        public static string? CodeFor(string? teamId, string? teamName = null)
        {
            foreach (var candidate in Candidates(teamId, teamName))
            {
                if (Overrides.TryGetValue(candidate, out var code))
                    return code;

                if (RegionCodesByTeamId.Value.TryGetValue(candidate, out code))
                    return code;
            }

            return null;
        }

        private static IEnumerable<string> Candidates(string? teamId, string? teamName)
        {
            if (!string.IsNullOrWhiteSpace(teamId))
                yield return teamId.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(teamName))
                yield return TeamNameNormalizer.ToId(teamName);
        }

        private static IReadOnlyDictionary<string, string> CreateRegionCodeLookup()
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var regions = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Select(culture =>
                {
                    try
                    {
                        return new RegionInfo(culture.Name);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                })
                .OfType<RegionInfo>()
                .DistinctBy(region => region.TwoLetterISORegionName);

            foreach (var region in regions)
            {
                Add(region.EnglishName, region.TwoLetterISORegionName);
                Add(region.NativeName, region.TwoLetterISORegionName);
                Add(region.DisplayName, region.TwoLetterISORegionName);
            }

            return lookup;

            void Add(string name, string code)
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code) || code.Length != 2)
                    return;

                lookup.TryAdd(TeamNameNormalizer.ToId(name), code.ToLowerInvariant());
            }
        }
    }
}
