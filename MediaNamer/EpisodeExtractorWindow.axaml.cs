using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaNamer
{
    public partial class EpisodeExtractorWindow : Window
    {
        public MediaDictionary MediaDataDict { get; set; }
        public string ShowName { get; set; }
        public string Season { get; set; }
        public bool ReverseEpisodeOrder { get; set; }

        public EpisodeExtractorWindow()
        {
            InitializeComponent();
        }

        public EpisodeExtractorWindow(MediaDictionary mediaDataDict, string showName, string season, bool reverseEpisodeOrder)
        {
            InitializeComponent();
            MediaDataDict = mediaDataDict;
            ShowName = showName;
            Season = season;
            ReverseEpisodeOrder = reverseEpisodeOrder;
        }

        private void ProcessAndSave_Click(object sender, RoutedEventArgs e)
        {
            var rawText = RawEpisodeInput.Text ?? string.Empty;
            var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim());

            List<string> titles = new List<string>();
            foreach (var ln in lines)
            {
                if (EpisodeExtractorLogic.IsEpisodeTitleLine(ln))
                {
                    string safe = EpisodeExtractorLogic.MakeWindowsSafe(ln);
                    if (!string.IsNullOrEmpty(safe))
                    {
                        titles.Add(safe);
                    }
                }
            }

            if (!ReverseEpisodeOrder)
            {
                titles.Reverse();
            }

            UpdateEpisodeList(titles);

            string result = string.Join(",", titles);
            OutputText.Text = result;
            UpdateStatusLabel(titles, false);
        }

        private void UseManualEpisodeNames_Click(object sender, RoutedEventArgs e)
        {
            var rawText = OutputText.Text ?? string.Empty;
            var titles = EpisodeExtractorLogic.ParseManualEpisodeTitles(rawText);

            UpdateEpisodeList(titles);
            UpdateStatusLabel(titles, true);

            if (titles.Count > 0)
            {
                Console.WriteLine("Episode list overridden using manual comma-delimited input.");
            }
            else
            {
                Console.WriteLine("Manual override cleared the episode list.");
            }
        }

        private async void FetchEpisodeNamesOnline_Click(object sender, RoutedEventArgs e)
        {
            string showName = GetShowName();
            int? seasonNumber = GetSeasonNumber();

            if (string.IsNullOrEmpty(showName) || !seasonNumber.HasValue)
            {
                return;
            }

            try
            {
                var titles = await EpisodeExtractorLogic.FetchEpisodeTitlesFromTvmazeAsync(showName, seasonNumber.Value);
                if (titles.Count == 0)
                {
                    SetStatusError($"No episodes found for {showName} season {seasonNumber.Value}.");
                    return;
                }

                if (!ReverseEpisodeOrder)
                {
                    titles.Reverse();
                }

                UpdateEpisodeList(titles);

                string result = string.Join(",", titles);
                OutputText.Text = result;
                CountLabel.Text = $"Episodes fetched: {titles.Count}";
                CountLabel.Foreground = Brushes.Green;
            }
            catch (Exception exc)
            {
                SetStatusError($"Online lookup failed: {exc.Message}");
            }
        }

        private void UpdateEpisodeList(List<string> titles)
        {
            if (MediaDataDict != null)
            {
                MediaDataDict.EpisodeList = titles;
            }
        }

        private void UpdateStatusLabel(List<string> titles, bool manualOverride)
        {
            if (titles.Count > 0)
            {
                if (manualOverride)
                {
                    CountLabel.Text = $"Episodes ready (manual): {titles.Count}";
                }
                else
                {
                    CountLabel.Text = $"Episodes extracted: {titles.Count}";
                }
                CountLabel.Foreground = Brushes.Green;
            }
            else
            {
                CountLabel.Text = "Episodes extracted: 0";
                CountLabel.Foreground = Brushes.Red;
            }
        }

        private string GetShowName()
        {
            string showName = ShowName?.Trim() ?? "";
            if (string.IsNullOrEmpty(showName) || showName == "Enter show name:")
            {
                SetStatusError("Enter a show name before fetching episodes.");
                return "";
            }
            return showName;
        }

        private int? GetSeasonNumber()
        {
            string seasonText = Season?.Trim() ?? "";
            if (string.IsNullOrEmpty(seasonText) || seasonText == "Enter Season:")
            {
                SetStatusError("Enter a season number before fetching episodes.");
                return null;
            }

            if (!int.TryParse(seasonText, out int seasonNumber))
            {
                SetStatusError("Season must be a number.");
                return null;
            }

            if (seasonNumber < 0)
            {
                SetStatusError("Season must be a positive number.");
                return null;
            }

            return seasonNumber;
        }

        private void SetStatusError(string message)
        {
            CountLabel.Text = message;
            CountLabel.Foreground = Brushes.Red;
            Console.WriteLine(message);
        }
    }
}
