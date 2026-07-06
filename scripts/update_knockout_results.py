#!/usr/bin/env python3
import csv
import html
import re
import sys
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Optional


CBS_BRACKET_URL = "https://www.cbssports.com/soccer/news/2026-fifa-world-cup-bracket-round-of-32-results-round-of-16-matchups-final/"
SBNATION_QUARTERFINAL_URL = "https://www.sbnation.com/fifa-world-cup/1121530/world-cup-2026-quarterfinals-teams"
CSV_PATH = Path("Oloraculo.Web/Data/knockout_results.csv")

FIELDS = [
    "MatchNumber",
    "Stage",
    "HomeTeam",
    "AwayTeam",
    "HomeGoals",
    "AwayGoals",
    "HomePenaltyGoals",
    "AwayPenaltyGoals",
    "WinnerTeam",
    "Status",
    "Source",
    "SourceUpdatedAt",
]

PAIR_TO_MATCH = {
    frozenset(("Canada", "South Africa")): (73, "RoundOf32"),
    frozenset(("Germany", "Paraguay")): (74, "RoundOf32"),
    frozenset(("Netherlands", "Morocco")): (75, "RoundOf32"),
    frozenset(("Brazil", "Japan")): (76, "RoundOf32"),
    frozenset(("France", "Sweden")): (77, "RoundOf32"),
    frozenset(("Ivory Coast", "Norway")): (78, "RoundOf32"),
    frozenset(("Mexico", "Ecuador")): (79, "RoundOf32"),
    frozenset(("England", "Congo DR")): (80, "RoundOf32"),
    frozenset(("Belgium", "Senegal")): (81, "RoundOf32"),
    frozenset(("United States", "Bosnia and Herzegovina")): (82, "RoundOf32"),
    frozenset(("Spain", "Austria")): (83, "RoundOf32"),
    frozenset(("Portugal", "Croatia")): (84, "RoundOf32"),
    frozenset(("Switzerland", "Algeria")): (85, "RoundOf32"),
    frozenset(("Argentina", "Cabo Verde")): (86, "RoundOf32"),
    frozenset(("Colombia", "Ghana")): (87, "RoundOf32"),
    frozenset(("Australia", "Egypt")): (88, "RoundOf32"),
    frozenset(("Paraguay", "France")): (89, "RoundOf16"),
    frozenset(("Canada", "Morocco")): (90, "RoundOf16"),
    frozenset(("Brazil", "Norway")): (91, "RoundOf16"),
    frozenset(("Mexico", "England")): (92, "RoundOf16"),
    frozenset(("Portugal", "Spain")): (93, "RoundOf16"),
    frozenset(("United States", "Belgium")): (94, "RoundOf16"),
    frozenset(("Argentina", "Egypt")): (95, "RoundOf16"),
    frozenset(("Switzerland", "Colombia")): (96, "RoundOf16"),
}

ALIASES = {
    "Bosnia": "Bosnia and Herzegovina",
    "Bosnia & Herzegovina": "Bosnia and Herzegovina",
    "DR Congo": "Congo DR",
    "Cabo Verde": "Cabo Verde",
    "Cape Verde": "Cabo Verde",
    "USA": "United States",
    "USMNT": "United States",
}


@dataclass(frozen=True)
class Result:
    match_number: int
    stage: str
    home: str
    away: str
    home_goals: int
    away_goals: int
    winner: str
    source: str
    updated_at: str
    home_penalties: Optional[int] = None
    away_penalties: Optional[int] = None


def fetch_text(url: str) -> str:
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.9",
            "User-Agent": (
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
                "AppleWebKit/537.36 (KHTML, like Gecko) "
                "Chrome/126.0.0.0 Safari/537.36 Oloraculo"
            ),
        },
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        raw = response.read().decode("utf-8", errors="replace")
    raw = re.sub(r"<script[\s\S]*?</script>|<style[\s\S]*?</style>", " ", raw, flags=re.I)
    raw = re.sub(r"<[^>]+>", "\n", raw)
    text = html.unescape(raw)
    text = text.replace("\xa0", " ")
    return re.sub(r"\n\s*\n+", "\n", text)


def canonical(team: str) -> str:
    normalized = re.sub(r"\s+", " ", team.strip(" \t\n\r,.-"))
    return ALIASES.get(normalized, normalized)


def parse_eliminated(text: str) -> set[str]:
    match = re.search(
        r"Eliminated teams in the round of 32(?P<body>[\s\S]*?)Eliminated teams at the group stage",
        text,
        re.I,
    )
    if not match:
        return set()
    return {canonical(line) for line in match.group("body").splitlines() if canonical(line)}


def winner_from_score(home: str, away: str, hg: int, ag: int, eliminated: set[str]) -> str:
    if hg > ag:
        return home
    if ag > hg:
        return away
    if home in eliminated and away not in eliminated:
        return away
    if away in eliminated and home not in eliminated:
        return home
    raise ValueError(f"Cannot infer tied winner for {home} {hg}-{ag} {away}")


def result_for_pair(
    team_a: str,
    goals_a: int,
    team_b: str,
    goals_b: int,
    eliminated: set[str],
    source: str,
    updated_at: str,
    winner_first_penalties: Optional[tuple[int, int]] = None,
) -> Optional[Result]:
    team_a = canonical(team_a)
    team_b = canonical(team_b)
    meta = PAIR_TO_MATCH.get(frozenset((team_a, team_b)))
    if meta is None:
        return None

    match_number, stage = meta
    home, away = team_a, team_b
    home_goals, away_goals = goals_a, goals_b
    winner = winner_from_score(home, away, home_goals, away_goals, eliminated)
    home_penalties = away_penalties = None
    if winner_first_penalties is not None:
        win_pens, lose_pens = winner_first_penalties
        home_penalties = win_pens if winner == home else lose_pens
        away_penalties = win_pens if winner == away else lose_pens

    return Result(
        match_number=match_number,
        stage=stage,
        home=home,
        away=away,
        home_goals=home_goals,
        away_goals=away_goals,
        home_penalties=home_penalties,
        away_penalties=away_penalties,
        winner=winner,
        source=source,
        updated_at=updated_at,
    )


def parse_cbs_round_of_32(text: str) -> list[Result]:
    eliminated = parse_eliminated(text)
    if not eliminated:
        raise ValueError("CBS page did not expose eliminated Round of 32 teams")

    section = re.search(r"Current World Cup bracket[\s\S]*?Round of 32 results(?P<body>[\s\S]*?)Round of 16", text, re.I)
    if not section:
        raise ValueError("CBS page did not expose Round of 32 results")

    body = re.sub(r"\s+", " ", section.group("body"))
    pattern = re.compile(
        r"(?P<a>[A-Z][A-Za-z .&]+?)\s+(?P<ag>\d+)\s*(?:,|vs\.)\s*(?:vs\.\s*)?"
        r"(?P<b>[A-Z][A-Za-z .&]+?)\s+(?P<bg>\d+)"
        r"(?:\s*\((?P<wp>\d+)-(?P<lp>\d+)\s+on\s+pen(?:s|alties)\))?",
        re.I,
    )

    results: dict[int, Result] = {}
    for match in pattern.finditer(body):
        result = result_for_pair(
            match.group("a"),
            int(match.group("ag")),
            match.group("b"),
            int(match.group("bg")),
            eliminated,
            CBS_BRACKET_URL,
            "2026-07-04T00:00:00Z",
            (int(match.group("wp")), int(match.group("lp"))) if match.group("wp") else None,
        )
        if result is not None:
            results[result.match_number] = result

    # The article renders Colombia-Ghana with a leading date, which the generic pattern may skip.
    colombia = re.search(r"Colombia\s+(?P<hg>\d+),\s*Ghana\s+(?P<ag>\d+)", body, re.I)
    if colombia:
        result = result_for_pair(
            "Colombia",
            int(colombia.group("hg")),
            "Ghana",
            int(colombia.group("ag")),
            eliminated,
            CBS_BRACKET_URL,
            "2026-07-04T00:00:00Z",
        )
        if result is not None:
            results[result.match_number] = result

    if len([r for r in results.values() if r.stage == "RoundOf32"]) < 16:
        found = ", ".join(str(k) for k in sorted(results))
        raise ValueError(f"CBS parser found incomplete Round of 32 results: {found}")

    return list(results.values())


def parse_cbs_round_of_16(text: str) -> list[Result]:
    section = re.search(
        r"Current World Cup bracket[\s\S]*?Round of 16 bracket(?P<body>[\s\S]*?)Round of 32 results",
        text,
        re.I,
    )
    if not section:
        raise ValueError("CBS page did not expose Round of 16 bracket")

    body = re.sub(r"\s+", " ", section.group("body"))
    pattern = re.compile(
        r"(?P<a>[A-Z][A-Za-z .&]+?)\s+(?P<ag>\d+)\s*(?:,|vs\.)\s*(?:vs\.\s*)?"
        r"(?P<b>[A-Z][A-Za-z .&]+?)\s+(?P<bg>\d+)"
        r"(?:\s*\((?P<wp>\d+)-(?P<lp>\d+)\s+on\s+pen(?:s|alties)\))?",
        re.I,
    )

    results: dict[int, Result] = {}
    for match in pattern.finditer(body):
        result = result_for_pair(
            match.group("a"),
            int(match.group("ag")),
            match.group("b"),
            int(match.group("bg")),
            set(),
            CBS_BRACKET_URL,
            "2026-07-06T03:30:00Z",
            (int(match.group("wp")), int(match.group("lp"))) if match.group("wp") else None,
        )
        if result is not None and result.stage == "RoundOf16":
            results[result.match_number] = result

    eliminated = re.search(r"Eliminated teams in the round of 16(?P<body>[\s\S]*)", body, re.I)
    if eliminated:
        eliminated_teams = {
            canonical(line)
            for line in re.split(r"\s{2,}|(?<=[a-z])(?=[A-Z])", eliminated.group("body"))
            if canonical(line) in {team for pair in PAIR_TO_MATCH for team in pair}
        }
        found_losers = {
            result.away if result.winner == result.home else result.home
            for result in results.values()
            if result.stage == "RoundOf16"
        }
        missing = sorted(eliminated_teams - found_losers)
        if missing:
            raise ValueError(f"CBS parser missed eliminated Round of 16 teams: {', '.join(missing)}")

    return list(results.values())


def parse_sbnation_round_of_16(text: str) -> list[Result]:
    eliminated: set[str] = set()
    results: dict[int, Result] = {}
    pattern = re.compile(
        r"(?P<winner>[A-Z][A-Za-z .&]+?) became [^.]*? with a "
        r"(?P<wg>\d+)-(?P<lg>\d+) victory over (?P<loser>[A-Z][A-Za-z .&]+?)(?:\s+on\b|\.|,|\n)",
        re.I | re.S,
    )
    for match in pattern.finditer(text):
        winner = canonical(match.group("winner"))
        loser = canonical(match.group("loser"))
        result = result_for_pair(
            loser,
            int(match.group("lg")),
            winner,
            int(match.group("wg")),
            eliminated,
            SBNATION_QUARTERFINAL_URL,
            "2026-07-04T19:40:00Z",
        )
        if result is not None:
            results[result.match_number] = result
    return list(results.values())


def read_existing() -> dict[int, dict[str, str]]:
    if not CSV_PATH.exists():
        return {}
    with CSV_PATH.open(newline="") as handle:
        return {int(row["MatchNumber"]): row for row in csv.DictReader(handle)}


def row(result: Result) -> dict[str, str]:
    return {
        "MatchNumber": str(result.match_number),
        "Stage": result.stage,
        "HomeTeam": result.home,
        "AwayTeam": result.away,
        "HomeGoals": str(result.home_goals),
        "AwayGoals": str(result.away_goals),
        "HomePenaltyGoals": "" if result.home_penalties is None else str(result.home_penalties),
        "AwayPenaltyGoals": "" if result.away_penalties is None else str(result.away_penalties),
        "WinnerTeam": result.winner,
        "Status": "Finished",
        "Source": result.source,
        "SourceUpdatedAt": result.updated_at,
    }


def main() -> int:
    existing = read_existing()
    updates: dict[int, Result] = {}
    cbs_text = fetch_text(CBS_BRACKET_URL)
    updates.update({result.match_number: result for result in parse_cbs_round_of_32(cbs_text)})
    updates.update({result.match_number: result for result in parse_cbs_round_of_16(cbs_text)})
    updates.update({result.match_number: result for result in parse_sbnation_round_of_16(fetch_text(SBNATION_QUARTERFINAL_URL))})

    for result in updates.values():
        existing[result.match_number] = row(result)

    CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    with CSV_PATH.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDS, lineterminator="\n")
        writer.writeheader()
        for match_number in sorted(existing):
            writer.writerow({field: existing[match_number].get(field, "") for field in FIELDS})

    print(f"Updated {CSV_PATH} with {len(updates)} public knockout results.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"Failed to update public knockout results: {exc}", file=sys.stderr)
        raise SystemExit(1)
