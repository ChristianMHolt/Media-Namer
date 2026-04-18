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
            
            _mediaDataDict.MediaType = (MediaTypeEntry.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            _mediaDataDict.DualAudio = DualAudioCheckbox.IsChecked == true ? "Dual Audio" : "";
        }

        private async void SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            string mediaType = (MediaTypeEntry.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "";

            string suggestedPath = @"X:\SeedingTorrents"; 

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
            try
            {
                // Safely get folder name even if a trailing slash exists
                string folderName = new DirectoryInfo(dirPath).Name;
                Console.WriteLine($"\n--- Auto-Parser ---");
                Console.WriteLine($"Target Directory: {folderName}");

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

                string lowerFolder = folderName.ToLower();

                // Extract Scene/Release Group
                var sceneMatchStart = Regex.Match(folderName, @"^\[(.*?)\]");
                var sceneMatchEnd = Regex.Match(folderName, @"-([^-]+)$");

                if (sceneMatchStart.Success)
                {
                    SceneEntry.Text = sceneMatchStart.Groups[1].Value.Trim();
                    folderName = folderName.Substring(sceneMatchStart.Length);
                    Console.WriteLine($"Detected Scene: {SceneEntry.Text}");
                }
                else if (sceneMatchEnd.Success)
                {
                    SceneEntry.Text = sceneMatchEnd.Groups[1].Value.Trim();
                    folderName = folderName.Substring(0, folderName.Length - sceneMatchEnd.Length);
                    Console.WriteLine($"Detected Scene: {SceneEntry.Text}");
                }

                // Dual Audio
                if (lowerFolder.Contains("dual audio") || lowerFolder.Contains("dual-audio") || lowerFolder.Contains(".dual.") || lowerFolder.Contains(" dual "))
                {
                    DualAudioCheckbox.IsChecked = true;
                    Console.WriteLine("Detected Dual Audio: Yes");
                }

                // Resolution
                if (lowerFolder.Contains("2160p") || lowerFolder.Contains("4k")) { SetComboBoxByContent(ResolutionCombobox, "2160p"); Console.WriteLine("Resolution: 2160p"); }
                else if (lowerFolder.Contains("1080p")) { SetComboBoxByContent(ResolutionCombobox, "1080p"); Console.WriteLine("Resolution: 1080p"); }
                else if (lowerFolder.Contains("800p")) { SetComboBoxByContent(ResolutionCombobox, "800p"); Console.WriteLine("Resolution: 800p"); }
                else if (lowerFolder.Contains("720p")) { SetComboBoxByContent(ResolutionCombobox, "720p"); Console.WriteLine("Resolution: 720p"); }
                else if (lowerFolder.Contains("480p")) { SetComboBoxByContent(ResolutionCombobox, "480p"); Console.WriteLine("Resolution: 480p"); }

                // Source
                if (lowerFolder.Contains("bd") && lowerFolder.Contains("remux")) { SetComboBoxByContent(SourceCombobox, "BD Remux"); Console.WriteLine("Source: BD Remux"); }
                else if (lowerFolder.Contains("bd") || lowerFolder.Contains("bluray")) { SetComboBoxByContent(SourceCombobox, "BD Encode"); Console.WriteLine("Source: BD Encode"); }
                else if (lowerFolder.Contains("dvd") && lowerFolder.Contains("remux")) { SetComboBoxByContent(SourceCombobox, "DVD Remux"); Console.WriteLine("Source: DVD Remux"); }
                else if (lowerFolder.Contains("dvd")) { SetComboBoxByContent(SourceCombobox, "DVD Encode"); Console.WriteLine("Source: DVD Encode"); }
                else if (lowerFolder.Contains("web-dl") || lowerFolder.Contains("webdl") || lowerFolder.Contains(".web.")) { SetComboBoxByContent(SourceCombobox, "WEB-DL"); Console.WriteLine("Source: WEB-DL"); }
                else if (lowerFolder.Contains("web-rip") || lowerFolder.Contains("webrip")) { SetComboBoxByContent(SourceCombobox, "WEB-RIP"); Console.WriteLine("Source: WEB-RIP"); }

                // Video Format
                if (lowerFolder.Contains("h.265") || lowerFolder.Contains("h265") || lowerFolder.Contains("x265") || lowerFolder.Contains("hevc")) { SetComboBoxByContent(VideoFormatCombobox, "H.265"); Console.WriteLine("Video: H.265"); }
                else if (lowerFolder.Contains("h.264") || lowerFolder.Contains("h264") || lowerFolder.Contains("x264") || lowerFolder.Contains("avc")) { SetComboBoxByContent(VideoFormatCombobox, "H.264"); Console.WriteLine("Video: H.264"); }
                else if (lowerFolder.Contains("svt-av1")) { SetComboBoxByContent(VideoFormatCombobox, "SVT-AV1"); Console.WriteLine("Video: SVT-AV1"); }
                else if (lowerFolder.Contains("av1")) { SetComboBoxByContent(VideoFormatCombobox, "AV1"); Console.WriteLine("Video: AV1"); }

                // Audio Format
                if (lowerFolder.Contains("flac")) { AudioFormatEntry.Text = "FLAC"; Console.WriteLine("Audio: FLAC"); }
                else if (lowerFolder.Contains("dts")) { AudioFormatEntry.Text = "DTS"; Console.WriteLine("Audio: DTS"); }
                else if (lowerFolder.Contains("aac")) { AudioFormatEntry.Text = "AAC"; Console.WriteLine("Audio: AAC"); }
                else if (lowerFolder.Contains("opus")) { AudioFormatEntry.Text = "OPUS"; Console.WriteLine("Audio: OPUS"); }
                else if (lowerFolder.Contains("eac3") || lowerFolder.Contains("ddp")) { AudioFormatEntry.Text = "EAC3"; Console.WriteLine("Audio: EAC3"); }
                else if (lowerFolder.Contains("ac3")) { AudioFormatEntry.Text = "AC3"; Console.WriteLine("Audio: AC3"); }

                // Extract Show Name and Season
                string cleanName = Regex.Replace(folderName, @"\[.*?\]|\(.*?\)", "");

                // Convert periods and underscores to spaces for standard releases
                cleanName = cleanName.Replace(".", " ").Replace("_", " ");

                var seasonMatch = Regex.Match(cleanName, @"(?i)\b(?:Season\s+|S)(\d+)\b");
                if (seasonMatch.Success)
                {
                    string seasonStr = seasonMatch.Groups[1].Value.Trim();
                    // Strip leading zeros for a cleaner UI output
                    if (seasonStr.StartsWith("0") && seasonStr.Length > 1) seasonStr = seasonStr.TrimStart('0');

                    SeasonEntry.Text = seasonStr;
                    Console.WriteLine($"Detected Season: {SeasonEntry.Text}");

                    cleanName = cleanName.Substring(0, seasonMatch.Index);
                }
                else
                {
                    string[] knownTags = { "2160p", "1080p", "800p", "720p", "480p", "4k", "repack", "cr", "web-dl", "webdl", "dual", "h 264", "h264", "x264", "avc", "h 265", "h265", "x265", "hevc", "svt-av1", "av1", "flac", "dts", "aac", "opus", "eac3", "ddp2 0", "ddp5 1", "ac3", "bluray", "bd", "remux", "web-rip", "webrip" };
                    foreach (string tag in knownTags)
                    {
                        cleanName = Regex.Replace(cleanName, $@"(?i)\b{tag}\b", "");
                    }
                }

                cleanName = cleanName.Trim();
                if (cleanName.EndsWith("-"))
                {
                    cleanName = cleanName.Substring(0, cleanName.Length - 1).Trim();
                }

                if (!string.IsNullOrEmpty(cleanName))
                {
                    ShowNameEntry.Text = cleanName;
                    Console.WriteLine($"Detected Show Name: {ShowNameEntry.Text}");
                }
                Console.WriteLine($"--- Parse Complete ---\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Parser Error] {ex.Message}");
            }
        }   

        private void SetComboBoxByContent(ComboBox comboBox, string content)
        {
            if (comboBox == null || comboBox.Items == null) return;
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i];
                if (item is ComboBoxItem cbItem && cbItem.Content?.ToString() == content)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
                else if (item is string strItem && strItem == content)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            Console.WriteLine($"[Warning] Could not match combobox tag for: {content}");
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

        [System.Runtime.InteropServices.DllImport("Kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private void HardlinkFiles()
        {
            try
            {
                for (int i = 0; i < Math.Min(_mediaDataDict.SourceFiles.Count, _mediaDataDict.FinalFiles.Count); i++)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        string finalFile = _mediaDataDict.FinalFiles[i];
                        string sourceFile = _mediaDataDict.SourceFiles[i];

                        // Prepend \\?\ to bypass the 260 character MAX_PATH limitation
                        string formattedFinal = finalFile.StartsWith(@"\\?\") ? finalFile : @"\\?\" + finalFile;
                        string formattedSource = sourceFile.StartsWith(@"\\?\") ? sourceFile : @"\\?\" + sourceFile;

                        bool success = CreateHardLink(formattedFinal, formattedSource, IntPtr.Zero);
                        if (!success)
                        {
                            int errorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                            Console.WriteLine($"Failed to create hard link for {_mediaDataDict.FinalFiles[i]} (Error Code: {errorCode})");
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