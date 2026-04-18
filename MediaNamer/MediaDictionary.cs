using System;
using System.Collections.Generic;

namespace MediaNamer
{
    public class MediaDictionary
    {
        public string AudioFormat { get; set; } = string.Empty;
        public string VideoFormat { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string Scene { get; set; } = string.Empty;
        public string EpisodeOffset { get; set; } = string.Empty;
        public string ShowName { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;

        public string SourceDirectory { get; set; } = string.Empty;
        public string DestinationDirectory { get; set; } = string.Empty;
        public string DualAudio { get; set; } = string.Empty;
        public string MediaPath { get; set; } = string.Empty;
        public string SeasonPath { get; set; } = string.Empty;

        public List<string> EpisodeList { get; set; } = new List<string>();
        public List<string> FinalFiles { get; set; } = new List<string>();
        public List<string> SourceFiles { get; set; } = new List<string>();
    }
}
