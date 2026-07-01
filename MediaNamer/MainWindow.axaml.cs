using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MediaNamer
{
    public partial class MainWindow : Window
    {
        private MediaDictionary _mediaDataDict = new MediaDictionary();
        private TextBoxWriter _terminalWriter;
        private readonly DispatcherTimer _statusHideTimer;

        public MainWindow()
        {
            InitializeComponent();
            _terminalWriter = new TextBoxWriter(TerminalOutput);
            Console.SetOut(_terminalWriter);
            Console.SetError(_terminalWriter);

            _statusHideTimer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, OnStatusHideTick);
        }

        private void OnStatusHideTick(object? sender, EventArgs e)
        {
            _statusHideTimer.Stop();
            EpisodeFetchStatus.IsVisible = false;
        }

        private void EpisodeFetchStatus_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
            {
                _statusHideTimer.Stop();
                EpisodeFetchStatus.IsVisible = false;
                e.Handled = true;
            }
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
                await Task.WhenAll(
                    BackfillMissingInfoFromMkvAsync(path),
                    AutoFetchEpisodeNamesAsync());
            }
        }

        // After the folder-name parse, fill any container-derivable field it left blank by
        // probing the first .mkv with mkvinfo. Name-based values always win; this only fills gaps.
        private async Task BackfillMissingInfoFromMkvAsync(string dirPath)
        {
            try
            {
                bool needRes = ResolutionCombobox.SelectedIndex == -1;
                bool needVideo = VideoFormatCombobox.SelectedIndex == -1;
                // Folder names only give the bare codec (e.g. "FLAC"); mkvinfo gives codec +
                // channel layout (e.g. "FLAC2.0"), so let it upgrade audio even when the name
                // parse already set a codec. Only skip when the field already has a full tag.
                bool needAudio = string.IsNullOrEmpty(AudioFormatEntry.Text)
                                 || !Regex.IsMatch(AudioFormatEntry.Text, @"\d");
                bool needDual = DualAudioCheckbox.IsChecked != true;

                if (!needRes && !needVideo && !needAudio && !needDual)
                    return; // nothing missing; don't bother running mkvinfo

                string? firstMkv = MkvInfoProbe.FindFirstMkv(dirPath);
                if (firstMkv == null)
                {
                    Console.WriteLine("[mkvinfo] No .mkv file to backfill from; skipping.");
                    return;
                }

                Console.WriteLine($"\n--- mkvinfo Backfill ---");
                Console.WriteLine($"Probing: {Path.GetFileName(firstMkv)}");

                // Read media type directly from the UI — SaveLabels hasn't run yet at this point
                // so _mediaDataDict.MediaType is still empty.
                string mediaType = (MediaTypeEntry.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                var probe = await MkvInfoProbe.ProbeAsync(firstMkv, mediaType);
                if (probe == null)
                    return; // ProbeAsync already logged the reason

                if (needRes && probe.Resolution != null)
                {
                    SetComboBoxByContent(ResolutionCombobox, probe.Resolution);
                    Console.WriteLine($"Resolution (mkvinfo): {probe.Resolution}");
                }
                if (needVideo && probe.VideoFormat != null)
                {
                    SetComboBoxByContent(VideoFormatCombobox, probe.VideoFormat);
                    Console.WriteLine($"Video (mkvinfo): {probe.VideoFormat}");
                }
                if (needAudio && probe.AudioTag != null)
                {
                    AudioFormatEntry.Text = probe.AudioTag;
                    Console.WriteLine($"Audio (mkvinfo): {probe.AudioTag}");
                }
                if (needDual && probe.IsDualAudio)
                {
                    DualAudioCheckbox.IsChecked = true; // fires DualAudio_Changed -> updates the dict
                    Console.WriteLine("Dual Audio (mkvinfo): Yes");
                }

                Console.WriteLine($"--- mkvinfo Complete ---\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mkvinfo Error] {ex.Message}");
            }
        }

        // After the folder-name parse, try to pull episode names from TVMaze so the user doesn't
        // have to open the Episode Extractor manually. Name/season come from the parse; if either
        // is missing or the lookup fails, the status box reports why instead of blocking anything.
        private async Task AutoFetchEpisodeNamesAsync()
        {
            string showName = (ShowNameEntry.Text ?? "").Trim();
            string seasonText = (SeasonEntry.Text ?? "").Trim();

            if (string.IsNullOrEmpty(showName) ||
                !int.TryParse(seasonText, out int seasonNumber) ||
                seasonNumber < 0)
            {
                SetEpisodeFetchStatus("Auto-fetch skipped: need a show name and season.", FetchStatus.Neutral);
                return;
            }

            SetEpisodeFetchStatus($"Fetching episode names for {showName} Season {seasonNumber}…", FetchStatus.Neutral, autoHide: false);

            try
            {
                var titles = await EpisodeExtractorLogic.FetchEpisodeTitlesFromTvmazeAsync(showName, seasonNumber);
                if (titles.Count == 0)
                {
                    SetEpisodeFetchStatus($"No episodes found for {showName} Season {seasonNumber}.", FetchStatus.Error);
                    return;
                }

                _mediaDataDict.EpisodeList = titles;
                SetEpisodeFetchStatus($"Found {titles.Count} episodes for {showName} Season {seasonNumber}.", FetchStatus.Success);
            }
            catch (Exception ex)
            {
                SetEpisodeFetchStatus($"Online lookup failed: {ex.Message}", FetchStatus.Error);
            }
        }

        private void SetEpisodeFetchStatus(string message, FetchStatus status, bool autoHide = true)
        {
            EpisodeFetchStatus.IsVisible = true;
            EpisodeFetchStatusText.Text = message;
            EpisodeFetchStatus.Background = status switch
            {
                FetchStatus.Success => Avalonia.Media.Brushes.DarkGreen,
                FetchStatus.Error => Avalonia.Media.Brushes.DarkRed,
                _ => Avalonia.Media.Brushes.Gray,
            };

            _statusHideTimer.Stop();
            if (autoHide)
            {
                _statusHideTimer.Start();
            }
        }

        private enum FetchStatus { Neutral, Success, Error }

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

                // Source — check WEB patterns first; "webdl" contains "bd" as a substring,
                // so the short BD/DVD tokens must come AFTER the longer, more-specific ones.
                bool sourceDetected = false;

                if (HasTag(lowerFolder, "web-dl") || HasTag(lowerFolder, "webdl")) { SetComboBoxByContent(SourceCombobox, "WEB-DL"); Console.WriteLine("Source: WEB-DL"); sourceDetected = true; }
                else if (HasTag(lowerFolder, "web-rip") || HasTag(lowerFolder, "webrip")) { SetComboBoxByContent(SourceCombobox, "WEB-RIP"); Console.WriteLine("Source: WEB-RIP"); sourceDetected = true; }
                else if (HasTag(lowerFolder, "web")) { SetComboBoxByContent(SourceCombobox, "WEB-DL"); Console.WriteLine("Source: WEB-DL"); sourceDetected = true; }
                else if ((HasTag(lowerFolder, "bd") || HasTag(lowerFolder, "bluray")) && HasTag(lowerFolder, "remux")) { SetComboBoxByContent(SourceCombobox, "BD Remux"); Console.WriteLine("Source: BD Remux"); sourceDetected = true; }
                else if (HasTag(lowerFolder, "bd") || HasTag(lowerFolder, "bluray")) { SetComboBoxByContent(SourceCombobox, "BD Encode"); Console.WriteLine("Source: BD Encode"); sourceDetected = true; }
                else if (HasTag(lowerFolder, "dvd") && HasTag(lowerFolder, "remux")) { SetComboBoxByContent(SourceCombobox, "DVD Remux"); Console.WriteLine("Source: DVD Remux"); sourceDetected = true; }
                else if (HasTag(lowerFolder, "dvd")) { SetComboBoxByContent(SourceCombobox, "DVD Encode"); Console.WriteLine("Source: DVD Encode"); sourceDetected = true; }

                // Remux-only fallback: folder says "remux" but no source tag — infer from file size
                if (!sourceDetected && HasTag(lowerFolder, "remux"))
                {
                    double avgGb = GetAverageEpisodeSizeGb(dirPath);
                    if (avgGb >= 5.0)
                    {
                        SetComboBoxByContent(SourceCombobox, "BD Remux");
                        Console.WriteLine($"Source (size-inferred): BD Remux (avg {avgGb:F1} GB)");
                    }
                    else
                    {
                        SetComboBoxByContent(SourceCombobox, "BD Encode");
                        Console.WriteLine($"Source (size-inferred): BD Encode (avg {avgGb:F1} GB)");
                    }
                }

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
                else if (lowerFolder.Contains("eac3") || lowerFolder.Contains("ddp")) { AudioFormatEntry.Text = "DDP"; Console.WriteLine("Audio: DDP"); }
                else if (lowerFolder.Contains("ac3") || lowerFolder.Contains("dd ") || lowerFolder.Contains("dd-")) { AudioFormatEntry.Text = "DD"; Console.WriteLine("Audio: DD"); }

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

        /// <summary>Matches a tag as a standalone token using \b word boundaries so that
        /// short tokens like "bd" don't false-match inside longer words like "webdl".</summary>
        private static bool HasTag(string text, string tag)
        {
            return Regex.IsMatch(text, $@"\b{Regex.Escape(tag)}\b", RegexOptions.IgnoreCase);
        }

        /// <summary>Average size in GB of .mkv / .mp4 files in the directory.</summary>
        private static double GetAverageEpisodeSizeGb(string dirPath)
        {
            try
            {
                var files = Directory.GetFiles(dirPath)
                    .Where(f => { string e = Path.GetExtension(f).ToLowerInvariant(); return e == ".mkv" || e == ".mp4"; })
                    .ToList();
                if (files.Count == 0) return 0;
                return files.Average(f => new FileInfo(f).Length) / (1024.0 * 1024.0 * 1024.0);
            }
            catch
            {
                return 0;
            }
        }

        private void InputEpisodeNames_Click(object sender, RoutedEventArgs e)
        {
            SaveLabels();
            var window = new EpisodeExtractorWindow(_mediaDataDict, _mediaDataDict.ShowName, _mediaDataDict.Season);
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
                bool success = HardlinkFiles();
                if (success)
                    SetEpisodeFetchStatus("Hardlinks created successfully.", FetchStatus.Success);
                else
                    SetEpisodeFetchStatus("Hardlink creation had errors. Check the Terminal tab.", FetchStatus.Error);
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

        private bool HardlinkFiles()
        {
            bool allOk = true;
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

                        bool ok = CreateHardLink(formattedFinal, formattedSource, IntPtr.Zero);
                        if (!ok)
                        {
                            int errorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                            Console.WriteLine($"Failed to create hard link for {_mediaDataDict.FinalFiles[i]} (Error Code: {errorCode})");
                            allOk = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Hardlinking is only fully supported on Windows in this implementation.");
                        allOk = false;
                    }
                }

                if (allOk)
                    Console.WriteLine("Hardlink creation completed successfully.");
                else
                    Console.WriteLine("Hardlink creation completed with errors (see above).");

                return allOk;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during hardlink: {ex.Message}");
                return false;
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
            private readonly TextBox _textBox;
            private readonly StringBuilder _buffer = new StringBuilder();
            private readonly object _lock = new object();
            private readonly DispatcherTimer _flushTimer;

            public TextBoxWriter(TextBox textBox)
            {
                _textBox = textBox;
                // Coalesce writes: drain the buffer to the TextBox on a short cadence so the
                // box re-renders a few times per second instead of once per character/line.
                _flushTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, Flush);
                _flushTimer.Start();
            }

            private void Flush(object? sender, EventArgs e)
            {
                string pending;
                lock (_lock)
                {
                    if (_buffer.Length == 0)
                        return;
                    pending = _buffer.ToString();
                    _buffer.Clear();
                }

                _textBox.Text += pending;
                _textBox.CaretIndex = _textBox.Text?.Length ?? 0;
            }

            public override void Write(char value)
            {
                lock (_lock) { _buffer.Append(value); }
            }

            public override void Write(string? value)
            {
                if (value != null)
                {
                    lock (_lock) { _buffer.Append(value); }
                }
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer == null) return;
                lock (_lock) { _buffer.Append(buffer, index, count); }
            }

            public override void WriteLine(string? value)
            {
                lock (_lock)
                {
                    if (value != null) _buffer.Append(value);
                    _buffer.Append(CoreNewLine);
                }
            }

            public override void WriteLine()
            {
                lock (_lock) { _buffer.Append(CoreNewLine); }
            }

            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}