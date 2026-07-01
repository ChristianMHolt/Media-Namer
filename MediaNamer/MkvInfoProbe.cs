using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MediaNamer
{
    /// <summary>Container metadata read back from mkvinfo. Any property may be null/blank if
    /// mkvinfo did not report it (or the codec is unmapped).</summary>
    public class MkvProbeResult
    {
        public string? Resolution { get; set; }   // e.g. "1080p"
        public string? VideoFormat { get; set; }  // e.g. "H.265"
        public string? AudioFormat { get; set; }  // base codec label, e.g. "DDP"
        public string? AudioTag { get; set; }     // full audio tag, e.g. "DDP2.0" or "DDP2.0&5.1"
        public bool IsDualAudio { get; set; }      // 2+ audio tracks with distinct languages
    }

    /// <summary>Runs mkvinfo on a single .mkv and parses out the fields the auto-parser can
    /// fall back on when the folder name doesn't supply them. No UI dependencies.</summary>
    public static class MkvInfoProbe
    {
        // Tried in order; the first one that launches wins.
        private static readonly string[] CandidateExePaths =
        {
            "mkvinfo", // on PATH
            @"C:\Program Files\MKVToolNix\mkvinfo.exe",
            @"C:\Program Files (x86)\MKVToolNix\mkvinfo.exe"
        };

        // Resolution buckets the UI supports (match the ResolutionCombobox items).
        private static readonly int[] ResolutionBuckets = { 2160, 1080, 800, 720, 480 };

        /// <summary>The alphabetically-first .mkv in the folder, or null if there is none.
        /// mkvinfo is Matroska-only, so .mp4 files are intentionally ignored here.</summary>
        public static string? FindFirstMkv(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return null;

            return Directory.GetFiles(dirPath)
                .Where(f => string.Equals(Path.GetExtension(f), ".mkv", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>Runs mkvinfo on the file and parses the result. Returns null if mkvinfo
        /// could not be run at all (e.g. MKVToolNix is not installed).</summary>
        public static async Task<MkvProbeResult?> ProbeAsync(string filePath, string mediaType)
        {
            string output = await RunMkvInfoAsync(filePath);
            if (string.IsNullOrEmpty(output))
                return null;

            return Parse(output, mediaType);
        }

        private static async Task<string> RunMkvInfoAsync(string filePath)
        {
            foreach (string exe in CandidateExePaths)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };
                    psi.ArgumentList.Add(filePath);

                    using var proc = Process.Start(psi);
                    if (proc == null)
                        continue;

                    // Output is small (track headers only); read fully, then wait. stderr is
                    // left un-redirected so it cannot fill a buffer and deadlock us.
                    string stdout = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    return stdout;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Executable not present at this candidate path; try the next.
                }
            }

            Console.WriteLine("[mkvinfo] Executable not found. Install MKVToolNix or add it to PATH.");
            return string.Empty;
        }

        // One track's relevant fields, accumulated while scanning its block. mkvinfo lists a
        // track's lines in the order: Language -> Codec ID -> Track type -> (Video/Audio sub-
        // section with Pixel height), so we cannot classify on the fly — we buffer per track
        // and classify once the whole block is read.
        private sealed class TrackInfo
        {
            public string Type = "";       // "video" | "audio" | "subtitles"
            public string CodecId = "";
            public int? PixelHeight;
            public int? AudioChannels;
            public string? Language;
            public string? Name;
        }

        private static MkvProbeResult Parse(string output, string mediaType)
        {
            var tracks = new List<TrackInfo>();
            TrackInfo? current = null;

            foreach (string rawLine in output.Split('\n'))
            {
                // Strip mkvinfo's tree prefix (e.g. "|  + ") from the left.
                string line = rawLine.TrimEnd('\r').TrimStart('+', '|', ' ');

                if (line.StartsWith("Track number:", StringComparison.OrdinalIgnoreCase))
                {
                    current = new TrackInfo();
                    tracks.Add(current);
                    continue;
                }
                if (current == null)
                    continue; // not inside a track yet (EBML head / segment info)

                if (line.StartsWith("Track type:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Type = ValueAfterColon(line).ToLowerInvariant();
                }
                else if (line.StartsWith("Codec ID:", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(current.CodecId))
                        current.CodecId = ValueAfterColon(line);
                }
                else if (line.StartsWith("Pixel height:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(ValueAfterColon(line), out int h))
                        current.PixelHeight = h;
                }
                else if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                {
                    // Some tracks lack a Language field but identify via Name (e.g. "English Stereo").
                    if (string.IsNullOrEmpty(current.Name))
                        current.Name = ValueAfterColon(line);
                }
                else if (line.StartsWith("Channels:", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Audio channels:", StringComparison.OrdinalIgnoreCase))
                {
                    if (current.AudioChannels == null && int.TryParse(ValueAfterColon(line), out int ch))
                        current.AudioChannels = ch;
                }
                else if (line.StartsWith("Language", StringComparison.OrdinalIgnoreCase))
                {
                    // Matches both "Language:" and "Language (IETF BCP 47):". Keep the first
                    // seen per track (the legacy ISO code is printed first).
                    if (string.IsNullOrEmpty(current.Language))
                    {
                        string lang = ValueAfterColon(line).ToLowerInvariant();
                        if (!string.IsNullOrEmpty(lang))
                            current.Language = lang;
                    }
                }
            }

            var result = new MkvProbeResult();

            var firstVideo = tracks.FirstOrDefault(t => t.Type == "video");
            if (firstVideo != null)
            {
                result.VideoFormat = MapVideoCodec(firstVideo.CodecId);
                if (firstVideo.PixelHeight is int height)
                    result.Resolution = MapResolution(height);
            }

            var audioTracks = tracks.Where(t => t.Type == "audio").ToList();
            if (audioTracks.Count > 0)
            {
                result.AudioFormat = MapAudioCodec(audioTracks[0].CodecId);
                result.AudioTag = BuildAudioTag(audioTracks, mediaType);
            }

            int distinctLanguages = audioTracks
                .Select(t => t.Language)
                .Where(l => !string.IsNullOrEmpty(l) && l != "und")
                .Distinct()
                .Count();
            result.IsDualAudio = distinctLanguages >= 2;

            return result;
        }

        private static string ValueAfterColon(string line)
        {
            int idx = line.IndexOf(':');
            return idx >= 0 ? line.Substring(idx + 1).Trim() : string.Empty;
        }

        private static string? MapVideoCodec(string codecId)
        {
            string c = codecId.ToUpperInvariant();
            if (c.Contains("HEVC") || c.Contains("MPEGH")) return "H.265";
            if (c.Contains("AVC")) return "H.264";              // V_MPEG4/ISO/AVC
            if (c.Contains("AV1")) return "AV1";                // SVT-AV1 is indistinguishable here
            return null;
        }

        private static string? MapAudioCodec(string codecId)
        {
            string c = codecId.ToUpperInvariant();
            if (c.Contains("FLAC")) return "FLAC";
            if (c.Contains("OPUS")) return "OPUS";
            if (c.Contains("EAC3")) return "DDP";               // Dolby Digital Plus; check before AC3
            if (c.Contains("AC3")) return "DD";                 // Dolby Digital
            if (c.Contains("DTS")) return "DTS";
            if (c.Contains("PCM")) return "PCM";
            if (c.Contains("TRUEHD") || c.Contains("MLP")) return "TrueHD";
            if (c.Contains("AAC")) return "AAC";
            return null;
        }

        /// <summary>Maps a raw channel count to the standard release-name layout string.</summary>
        private static string MapChannels(int channels) => channels switch
        {
            1 => "1.0",
            2 => "2.0",
            6 => "5.1",
            8 => "7.1",
            _ => $"{channels}.0"
        };

        private static bool IsJapanese(TrackInfo t) =>
            t.Language == "jpn" || t.Language == "ja" ||
            (t.Name != null && t.Name.Contains("Japanese", StringComparison.OrdinalIgnoreCase));

        private static bool IsEnglish(TrackInfo t) =>
            t.Language == "eng" || t.Language == "en" ||
            (t.Name != null && t.Name.Contains("English", StringComparison.OrdinalIgnoreCase));

        /// <summary>Builds the full audio tag. For anime with separate Japanese and English
        /// audio tracks, both codec and layout are compared:</summary>
        private static string? BuildAudioTag(List<TrackInfo> audioTracks, string mediaType)
        {
            if (audioTracks.Count == 0) return null;

            if (mediaType == "Anime")
            {
                var jp = audioTracks.FirstOrDefault(IsJapanese);
                var en = audioTracks.FirstOrDefault(IsEnglish);
                if (jp != null && en != null)
                {
                    string? jpCodec = MapAudioCodec(jp.CodecId);
                    string? enCodec = MapAudioCodec(en.CodecId);
                    if (jpCodec == null || enCodec == null) return null;

                    string jpLayout = MapChannels(jp.AudioChannels ?? 2);
                    string enLayout = MapChannels(en.AudioChannels ?? 2);

                    if (jpCodec == enCodec)
                    {
                        // Same codec; append layouts only when they differ
                        if (jpLayout == enLayout)
                            return $"{jpCodec}{jpLayout}";
                        else
                            return $"{jpCodec}{jpLayout}&{enLayout}";
                    }
                    else
                    {
                        // Different codecs; always show both
                        if (jpLayout == enLayout)
                            return $"{jpCodec}&{enCodec}{jpLayout}";
                        else
                            return $"{jpCodec}{jpLayout}&{enCodec}{enLayout}";
                    }
                }
            }

            // Fallback: first audio track
            string? codec = MapAudioCodec(audioTracks[0].CodecId);
            if (codec == null) return null;
            return $"{codec}{MapChannels(audioTracks[0].AudioChannels ?? 2)}";
        }

        private static string MapResolution(int height)
        {
            int best = ResolutionBuckets[0];
            int bestDiff = int.MaxValue;
            foreach (int bucket in ResolutionBuckets)
            {
                int diff = Math.Abs(bucket - height);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = bucket;
                }
            }
            return best + "p";
        }
    }
}
