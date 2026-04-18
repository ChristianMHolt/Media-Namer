using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MediaNamer
{
    public partial class MainWindow : Window
    {
        private MediaDictionary _mediaDataDict = new MediaDictionary();
        private TextBoxWriter _terminalWriter;

        public MainWindow()
        {
            InitializeComponent();
            _terminalWriter = new TextBoxWriter(TerminalOutput);
            Console.SetOut(_terminalWriter);
            Console.SetError(_terminalWriter);
        }

        private void UpdateExistingShowLight_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            UpdateExistingShowLight();
        }

        private void MediaType_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            UpdateExistingShowLight();
        }

        private void UpdateExistingShowLight()
        {
            // Now correctly references ExistingShowLight
            if (ExistingShowLight == null || ShowNameEntry == null || MediaTypeEntry == null) return;

            string showName = ShowNameEntry.Text ?? "";
            
            string mediaType = (MediaTypeEntry.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            if (string.IsNullOrEmpty(showName) || string.IsNullOrEmpty(mediaType))
            {
                ExistingShowLight.Fill = Avalonia.Media.Brushes.Gray;
                return;
            }

            string basePath = DestinationDirectoryClass.GetBasePath(mediaType);
            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                var dirs = Directory.GetDirectories(basePath, $"{showName} [*");
                if (dirs.Length > 0)
                {
                    ExistingShowLight.Fill = Avalonia.Media.Brushes.Green;
                    return;
                }
            }

            ExistingShowLight.Fill = Avalonia.Media.Brushes.Gray;
        }

        private void SaveLabels()
        {
            _mediaDataDict.AudioFormat = AudioFormatEntry.Text ?? "";
            _mediaDataDict.VideoFormat = (VideoFormatCombobox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _mediaDataDict.Source = (SourceCombobox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _mediaDataDict.Resolution = (ResolutionCombobox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _mediaDataDict.Scene = SceneEntry.Text ?? "";
            _mediaDataDict.EpisodeOffset = EpisodeOffsetEntry.Text ?? "0";
            _mediaDataDict.ShowName = ShowNameEntry.Text ?? "";
            _mediaDataDict.Season = SeasonEntry.Text ?? "";
            
            // FIX: Single line extraction
            _mediaDataDict.MediaType = (MediaTypeEntry.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            _mediaDataDict.DualAudio = DualAudioCheckbox.IsChecked == true ? "Dual Audio" : "";
        }

        private async void SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            // FIX: Single line extraction, converted to lower case for the logic below
            string mediaType = (MediaTypeEntry.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "";

            string suggestedPath = @"X:\SeedingTorrents"; // Fallback

            if (mediaType == "tv")
            {
                suggestedPath = @"X:\SeedingTorrents\TV Shows";
            }
            else if (mediaType == "anime")
            {
                suggestedPath = @"X:\SeedingTorrents\Anime";
            }
            else if (mediaType == "movie")
            {
                suggestedPath = @"X:\SeedingTorrents\Movies";
            }

            IStorageFolder? startLocation = null;
            try 
            {
                if (Directory.Exists(suggestedPath))
                {
                    startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not access path: {ex.Message}");
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Episode Directory",
                SuggestedStartLocation = startLocation
            });

            if (folders.Count >= 1)
            {
                string path = folders[0].Path.LocalPath;
                DirectoryEntry.Text = path;
                _mediaDataDict.SourceDirectory = path;
                Console.WriteLine(path);

                ParseTagsFromDirectory(path);
            }
        }

        private void ParseTagsFromDirectory(string dirPath)
        {
            string folderName = Path.GetFileName(dirPath);
            if (string.IsNullOrEmpty(folderName)) return;

            // Reset UI fields before processing
            SceneEntry.Text = "";
            ShowNameEntry.Text = "";
            SeasonEntry.Text = "";
            AudioFormatEntry.Text = "";
            DualAudioCheckbox.IsChecked = false;

            ResolutionCombobox.SelectedIndex = -1;
            SourceCombobox.SelectedIndex = -1;
            VideoFormatCombobox.SelectedIndex = -1;

            // Extract Scene if present in the beginning like [Scene]
            var sceneMatch = Regex.Match(folderName, @"^\[(.*?)\]");
            if (sceneMatch.Success)
            {
                SceneEntry.Text = sceneMatch.Groups[1].Value.Trim();
            }

            // Extract Show Name
            // Remove everything in [] and ()
            string cleanName = Regex.Replace(folderName, @"\[.*?\]|\(.*?\)", "");

            // Look for Season info
            var seasonMatch = Regex.Match(cleanName, @"(?i)(?:Season\s+|S)(\d+)");
            if (seasonMatch.Success)
            {
                SeasonEntry.Text = seasonMatch.Groups[1].Value.Trim();
                cleanName = cleanName.Replace(seasonMatch.Value, ""); // remove season from showname
            }

            // Clean up name
            cleanName = cleanName.Trim();
            if (cleanName.EndsWith("-"))
            {
                cleanName = cleanName.Substring(0, cleanName.Length - 1).Trim();
            }
            if (!string.IsNullOrEmpty(cleanName))
            {
                ShowNameEntry.Text = cleanName;
            }

            string lowerFolder = folderName.ToLower();

            // Resolution
            if (lowerFolder.Contains("2160p") || lowerFolder.Contains("4k")) SetComboBoxByContent(ResolutionCombobox, "2160p");
            else if (lowerFolder.Contains("1080p")) SetComboBoxByContent(ResolutionCombobox, "1080p");
            else if (lowerFolder.Contains("800p")) SetComboBoxByContent(ResolutionCombobox, "800p");
            else if (lowerFolder.Contains("720p")) SetComboBoxByContent(ResolutionCombobox, "720p");
            else if (lowerFolder.Contains("480p")) SetComboBoxByContent(ResolutionCombobox, "480p");

            // Source
            if (lowerFolder.Contains("bd") && lowerFolder.Contains("remux")) SetComboBoxByContent(SourceCombobox, "BD Remux");
            else if (lowerFolder.Contains("bd") || lowerFolder.Contains("bluray")) SetComboBoxByContent(SourceCombobox, "BD Encode");
            else if (lowerFolder.Contains("dvd") && lowerFolder.Contains("remux")) SetComboBoxByContent(SourceCombobox, "DVD Remux");
            else if (lowerFolder.Contains("dvd")) SetComboBoxByContent(SourceCombobox, "DVD Encode");
            else if (lowerFolder.Contains("web-dl") || lowerFolder.Contains("webdl")) SetComboBoxByContent(SourceCombobox, "WEB-DL");
            else if (lowerFolder.Contains("web-rip") || lowerFolder.Contains("webrip")) SetComboBoxByContent(SourceCombobox, "WEB-RIP");

            // Video Format
            if (lowerFolder.Contains("h.265") || lowerFolder.Contains("h265") || lowerFolder.Contains("x265") || lowerFolder.Contains("hevc")) SetComboBoxByContent(VideoFormatCombobox, "H.265");
            else if (lowerFolder.Contains("h.264") || lowerFolder.Contains("h264") || lowerFolder.Contains("x264") || lowerFolder.Contains("avc")) SetComboBoxByContent(VideoFormatCombobox, "H.264");
            else if (lowerFolder.Contains("svt-av1")) SetComboBoxByContent(VideoFormatCombobox, "SVT-AV1"); // must check before AV1
            else if (lowerFolder.Contains("av1")) SetComboBoxByContent(VideoFormatCombobox, "AV1");

            // Audio Format
            if (lowerFolder.Contains("flac")) AudioFormatEntry.Text = "FLAC";
            else if (lowerFolder.Contains("dts")) AudioFormatEntry.Text = "DTS";
            else if (lowerFolder.Contains("aac")) AudioFormatEntry.Text = "AAC";
            else if (lowerFolder.Contains("opus")) AudioFormatEntry.Text = "OPUS";
            else if (lowerFolder.Contains("eac3")) AudioFormatEntry.Text = "EAC3";
            else if (lowerFolder.Contains("ac3")) AudioFormatEntry.Text = "AC3";

            // Dual Audio
            if (lowerFolder.Contains("dual audio") || lowerFolder.Contains("dual-audio"))
            {
                DualAudioCheckbox.IsChecked = true;
            }
        }

        private void SetComboBoxByContent(ComboBox comboBox, string content)
        {
            if (comboBox == null || comboBox.Items == null) return;
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content?.ToString() == content)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void InputEpisodeNames_Click(object sender, RoutedEventArgs e)
        {
            SaveLabels();
            bool reverseEpisodeOrder = FlippedCheckbox.IsChecked ?? false;
            var window = new EpisodeExtractorWindow(_mediaDataDict, _mediaDataDict.ShowName, _mediaDataDict.Season, reverseEpisodeOrder);
            window.Show();
        }

        private void DualAudio_Changed(object sender, RoutedEventArgs e)
        {
            _mediaDataDict.DualAudio = DualAudioCheckbox.IsChecked == true ? "Dual Audio" : "";
        }

        private void RunScript(string mode)
        {
            SaveLabels();

            var destDirClass = new DestinationDirectoryClass(true, _mediaDataDict, mode);
            _mediaDataDict.DestinationDirectory = destDirClass.DestinationDirectory;

            var finalFilesClass = new FinalFileNamesClass(_mediaDataDict.DestinationDirectory, _mediaDataDict);
            _mediaDataDict.FinalFiles = finalFilesClass.FinalFileNames;

            var sourceDirClass = new SourceDirectoryFileListClass(_mediaDataDict);
            _mediaDataDict.SourceFiles = sourceDirClass.MediaDictionary.SourceFiles;

            if (mode != "Preview")
            {
                CheckDirectoryExists(_mediaDataDict.DestinationDirectory);
            }

            if (mode == "Hardlink")
            {
                HardlinkFiles();
            }
            else if (mode == "Rename")
            {
                RenameFiles(destDirClass);
            }
            else if (mode == "Preview")
            {
                PreviewFiles();
            }
        }

        private void PreviewFiles()
        {
            Console.WriteLine("These are the episode names:");
            foreach (var episode in _mediaDataDict.EpisodeList)
            {
                Console.WriteLine(episode);
            }
            Console.WriteLine("These are the final files:");
            foreach (var file in _mediaDataDict.FinalFiles)
            {
                Console.WriteLine(file);
            }
            Console.WriteLine("\nThese are the source files:");
            foreach (var file in _mediaDataDict.SourceFiles)
            {
                Console.WriteLine(file);
            }
        }

        private void RenameFiles(DestinationDirectoryClass destDirClass)
        {
            try
            {
                var md = _mediaDataDict;
                string showSourcePath = Path.GetDirectoryName(md.SourceDirectory);
                string mediaSourcePath = Path.GetDirectoryName(showSourcePath);
                string renamedShowSourcePath = Path.Combine(mediaSourcePath, destDirClass.MediaPath);
                string seasonPath = destDirClass.SeasonPath;
                string newSourceDirectory = Path.Combine(showSourcePath, seasonPath);

                for (int i = 0; i < Math.Min(md.SourceFiles.Count, md.FinalFiles.Count); i++)
                {
                    File.Move(md.SourceFiles[i], md.FinalFiles[i]);
                }

                Directory.Move(md.SourceDirectory, newSourceDirectory);
                _mediaDataDict.SourceDirectory = newSourceDirectory;
                showSourcePath = Path.GetDirectoryName(newSourceDirectory);
                Directory.Move(showSourcePath, renamedShowSourcePath);

                Console.WriteLine("Rename operation completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during rename: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("Kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private void HardlinkFiles()
        {
            try
            {
                for (int i = 0; i < Math.Min(_mediaDataDict.SourceFiles.Count, _mediaDataDict.FinalFiles.Count); i++)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        bool success = CreateHardLink(_mediaDataDict.FinalFiles[i], _mediaDataDict.SourceFiles[i], IntPtr.Zero);
                        if (!success)
                        {
                            Console.WriteLine($"Failed to create hard link for {_mediaDataDict.FinalFiles[i]}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Hardlinking is only fully supported on Windows in this implementation.");
                    }
                }
                Console.WriteLine("Hardlink creation completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during hardlink: {ex.Message}");
            }
        }

        private void CheckDirectoryExists(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;

            if (Directory.Exists(directory))
            {
                Console.WriteLine($"\nDirectory '{directory}' already exists.");
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"Created directory '{directory}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create directory '{directory}': {ex.Message}");
                }
            }
        }

        private void Hardlink_Click(object sender, RoutedEventArgs e) => RunScript("Hardlink");
        private void Rename_Click(object sender, RoutedEventArgs e) => RunScript("Rename");
        private void Preview_Click(object sender, RoutedEventArgs e) => RunScript("Preview");

        private class TextBoxWriter : TextWriter
        {
            private TextBox _textBox;

            public TextBoxWriter(TextBox textBox)
            {
                _textBox = textBox;
            }

            public override void Write(char value)
            {
                Dispatcher.UIThread.Post(() => {
                    _textBox.Text += value;
                    _textBox.CaretIndex = _textBox.Text?.Length ?? 0;
                });
            }

            public override void Write(string? value)
            {
                if (value != null)
                {
                    Dispatcher.UIThread.Post(() => {
                        _textBox.Text += value;
                        _textBox.CaretIndex = _textBox.Text?.Length ?? 0;
                    });
                }
            }

            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}