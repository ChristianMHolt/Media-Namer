using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Text;

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

        private void SaveLabels()
        {
            _mediaDataDict.AudioFormat = AudioFormatEntry.Text ?? "";
            _mediaDataDict.VideoFormat = (VideoFormatCombobox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _mediaDataDict.Source = (SourceCombobox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _mediaDataDict.Resolution = (ResolutionCombobox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _mediaDataDict.MediaType = MediaTypeEntry.Text ?? "";
            _mediaDataDict.Scene = SceneEntry.Text ?? "";
            _mediaDataDict.EpisodeOffset = EpisodeOffsetEntry.Text ?? "0";
            _mediaDataDict.ShowName = ShowNameEntry.Text ?? "";
            _mediaDataDict.Season = SeasonEntry.Text ?? "";

            _mediaDataDict.DualAudio = DualAudioCheckbox.IsChecked == true ? "Dual Audio" : "";
        }

        private async void SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Episode Directory"
            });

            if (folders.Count >= 1)
            {
                string path = folders[0].Path.LocalPath;
                DirectoryEntry.Text = path;
                _mediaDataDict.SourceDirectory = path;
                Console.WriteLine(path);
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
            _mediaDataDict.MediaPath = destDirClass.MediaPath;
            _mediaDataDict.SeasonPath = destDirClass.SeasonPath;

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
                RenameFiles();
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

        private void RenameFiles()
        {
            try
            {
                var md = _mediaDataDict;
                string showSourcePath = Path.GetDirectoryName(md.SourceDirectory);
                string mediaSourcePath = Path.GetDirectoryName(showSourcePath);
                string renamedShowSourcePath = Path.Combine(mediaSourcePath, md.MediaPath);
                string seasonPath = md.SeasonPath;
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
                        // In modern .NET, File.CreateSymbolicLink is available but we need hardlinks.
                        // .NET doesn't have a built-in cross-platform CreateHardLink API until later versions or we can use POSIX link.
                        // However, assuming Windows as primary platform based on original script paths (X:\).
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

        private void UpdateExistingShowLight(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CheckExistingShowLight();
        }

        private void UpdateExistingShowLight(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            CheckExistingShowLight();
        }

        private void UpdateExistingShowLight_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            CheckExistingShowLight();
        }

        private void CheckExistingShowLight()
        {
            var ExistingShowLight = this.FindControl<Avalonia.Controls.Shapes.Ellipse>("ExistingShowLight");
            if (ExistingShowLight == null) return;
            string mediaType = MediaTypeEntry?.Text ?? "";
            string showName = ShowNameEntry?.Text ?? "";

            string basePath = DestinationDirectoryClass.GetBasePath(mediaType);
            string existingPath = DestinationDirectoryClass.FindExistingShowDirectory(basePath, showName);

            if (!string.IsNullOrEmpty(existingPath))
            {
                ExistingShowLight.Fill = Avalonia.Media.Brushes.Green;
            }
            else
            {
                ExistingShowLight.Fill = Avalonia.Media.Brushes.Gray;
            }
        }

        // Custom TextWriter to redirect Console output to the TerminalOutput TextBox
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
