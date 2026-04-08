using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MediaNamer
{
    public static class EpisodeExtractorLogic
    {
        private static readonly Regex InvalidCharsRegex = new Regex(@"[<>:""/\\|?*]");
        private static readonly Regex ReservedBasenamesRegex = new Regex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$", RegexOptions.IgnoreCase);
        private static readonly Regex DateLineRegex = new Regex(@"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)[a-z]*\s+\d{1,2}\s+\d{4}$", RegexOptions.IgnoreCase);
        private static readonly Regex IgnoreNotesRegex = new Regex(@"(Finale|Remux|HDTV|Blu-?ray|WEB(?:[- ]DL)?|x26[45]|1080p|720p|2160p|4K)", RegexOptions.IgnoreCase);
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public static string MakeWindowsSafe(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            string clean = InvalidCharsRegex.Replace(name, "").Replace(".", "");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            clean = clean.TrimEnd(' ');

            if (ReservedBasenamesRegex.IsMatch(clean))
            {
                clean += "_";
            }
            return clean;
        }

        public static bool IsEpisodeTitleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;

            if (Regex.IsMatch(line, @"^\d+$")) return false;

            if (DateLineRegex.IsMatch(line)) return false;

            if (IgnoreNotesRegex.IsMatch(line)) return false;

            return true;
        }

        public static List<string> ParseManualEpisodeTitles(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return new List<string>();

            List<string> titles = new List<string>();
            foreach (var part in rawText.Split(','))
            {
                string cleanedPart = MakeWindowsSafe(part.Trim());
                if (!string.IsNullOrEmpty(cleanedPart))
                {
                    titles.Add(cleanedPart);
                }
            }
            return titles;
        }

        public static async Task<List<string>> FetchEpisodeTitlesFromTvmazeAsync(string showName, int seasonNumber)
        {
            if (string.IsNullOrWhiteSpace(showName))
            {
                throw new ArgumentException("Show name is required.");
            }

            string encodedShowName = HttpUtility.UrlEncode(showName);
            string url = $"https://api.tvmaze.com/singlesearch/shows?q={encodedShowName}&embed=episodes";

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(json);

            var root = document.RootElement;
            if (!root.TryGetProperty("_embedded", out JsonElement embedded) ||
                !embedded.TryGetProperty("episodes", out JsonElement episodes))
            {
                return new List<string>();
            }

            var seasonMatches = new List<JsonElement>();
            foreach (var episode in episodes.EnumerateArray())
            {
                if (episode.TryGetProperty("season", out JsonElement seasonElement) &&
                    seasonElement.ValueKind == JsonValueKind.Number &&
                    seasonElement.GetInt32() == seasonNumber)
                {
                    seasonMatches.Add(episode);
                }
            }

            // Sort by episode number
            seasonMatches.Sort((a, b) =>
            {
                int numA = a.TryGetProperty("number", out var nA) && nA.ValueKind == JsonValueKind.Number ? nA.GetInt32() : 0;
                int numB = b.TryGetProperty("number", out var nB) && nB.ValueKind == JsonValueKind.Number ? nB.GetInt32() : 0;
                return numA.CompareTo(numB);
            });

            List<string> titles = new List<string>();
            foreach (var episode in seasonMatches)
            {
                if (episode.TryGetProperty("name", out JsonElement nameElement))
                {
                    string name = MakeWindowsSafe(nameElement.GetString()?.Trim() ?? "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        titles.Add(name);
                    }
                }
            }
            return titles;
        }
    }
}
