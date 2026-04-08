using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MediaNamer
{
    public class FinalFileNamesClass
    {
        public string DestinationDirectory { get; private set; }
        public MediaDictionary MediaDictionary { get; private set; }
        public string DualAudio { get; private set; }
        public string ShowName { get; private set; }
        public string MediaType { get; private set; }
        public string Offset { get; private set; }
        public List<string> EpisodeList { get; private set; }
        public List<string> FinalEpisodeNames { get; private set; }
        public List<string> FinalFileNames { get; private set; }

        public FinalFileNamesClass(string destinationDirectory, MediaDictionary mediaDictionary)
        {
            DestinationDirectory = destinationDirectory;
            MediaDictionary = mediaDictionary;
            DualAudio = MediaDictionary.DualAudio ?? "";
            ShowName = MediaDictionary.ShowName;
            MediaType = MediaDictionary.MediaType;
            Offset = MediaDictionary.EpisodeOffset;
            EpisodeList = MediaDictionary.EpisodeList;
            FinalEpisodeNames = new List<string>();
            FinalFileNames = new List<string>();

            SanitizeAllEpisodes();
            CreateDetailedEpisodeNames();
            CreateFinalFileNames();
        }

        private string SanitizeEpisodeName(string episode)
        {
            char[] illegalChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*', '#', '%', '!', '@' };
            foreach (char c in illegalChars)
            {
                episode = episode.Replace(c.ToString(), "");
            }
            return episode;
        }

        private void SanitizeAllEpisodes()
        {
            for (int i = 0; i < EpisodeList.Count; i++)
            {
                EpisodeList[i] = SanitizeEpisodeName(EpisodeList[i]);
            }
        }

        private void CreateDetailedEpisodeNames()
        {
            int offset = DetermineEpisode0Offset();
            for (int i = 0; i < EpisodeList.Count; i++)
            {
                string detailedEpisodeName = DetermineDualAudio();
                detailedEpisodeName = DetermineShowName(detailedEpisodeName);
                detailedEpisodeName = Add0sToProperName(offset, i, MediaType, detailedEpisodeName);
                FinalEpisodeNames.Add($"{detailedEpisodeName}{EpisodeList[i]}");
            }
        }

        private int DetermineEpisode0Offset()
        {
            int offsetInt = 0;
            int.TryParse(Offset, out offsetInt);
            int numbersOfEpisodes = EpisodeList.Count + offsetInt;

            if (numbersOfEpisodes <= 99)
                return 0;
            else if (numbersOfEpisodes > 99 && numbersOfEpisodes <= 999)
                return 1;
            else if (numbersOfEpisodes > 999)
                return 2;

            return 0;
        }

        private string Add0sToProperName(int episode0Offset, int count, string mediaType, string detailedEpisodeName)
        {
            int offsetInt = 0;
            int.TryParse(Offset, out offsetInt);
            count += offsetInt;

            if (mediaType == "Movie")
            {
                return detailedEpisodeName;
            }

            if (episode0Offset == 0 && count + 1 < 10)
            {
                detailedEpisodeName += $" - 0{count + 1} - ";
            }
            else if (episode0Offset == 0 && count + 1 >= 10)
            {
                detailedEpisodeName += $" - {count + 1} - ";
            }
            else if (episode0Offset == 1 && count + 1 < 10)
            {
                detailedEpisodeName += $" - 00{count + 1} - ";
            }
            else if (episode0Offset == 1 && count + 1 < 100)
            {
                detailedEpisodeName += $" - 0{count + 1} - ";
            }
            else if (episode0Offset == 1 && count + 1 >= 100)
            {
                detailedEpisodeName += $" - {count + 1} - ";
            }
            else if (episode0Offset == 2 && count + 1 < 10)
            {
                detailedEpisodeName += $" - 000{count + 1} - ";
            }
            else if (episode0Offset == 2 && count + 1 < 100)
            {
                detailedEpisodeName += $" - 00{count + 1} - ";
            }
            else if (episode0Offset == 2 && count + 1 < 1000)
            {
                detailedEpisodeName += $" - 0{count + 1} - ";
            }
            else if (episode0Offset == 2 && count + 1 >= 1000)
            {
                detailedEpisodeName += $" - {count + 1} - ";
            }

            return detailedEpisodeName;
        }

        private string DetermineShowName(string detailedEpisodeNamePart2)
        {
            if (MediaType == "Movie")
            {
                return detailedEpisodeNamePart2;
            }
            else if (MediaType != "Movie")
            {
                detailedEpisodeNamePart2 += $"{ShowName}";
                return detailedEpisodeNamePart2;
            }
            else
            {
                Console.WriteLine("Final Files, Determine Media-Type, Error");
                detailedEpisodeNamePart2 += $"{ShowName}";
                return detailedEpisodeNamePart2;
            }
        }

        private string DetermineDualAudio()
        {
            if (DualAudio == "Dual Audio")
            {
                return "[Dual Audio] ";
            }
            else if (DualAudio == "")
            {
                return "";
            }
            else
            {
                Console.WriteLine("Final Files Dual Audio Error");
                return "";
            }
        }

        private void CreateFinalFileNames()
        {
            for (int i = 0; i < EpisodeList.Count; i++)
            {
                FinalFileNames.Add(Path.Combine(DestinationDirectory, $"{FinalEpisodeNames[i]}.mkv"));
            }

            Console.WriteLine(string.Join(", ", FinalFileNames.Select(x => $"'{x}'").ToArray()));
        }
    }
}
