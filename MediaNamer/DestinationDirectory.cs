using System;
using System.IO;

namespace MediaNamer
{
    public class DestinationDirectoryClass
    {
        public string DestinationDirectory { get; private set; }
        public string BasePath { get; private set; }
        public string MediaPath { get; private set; }
        public string SeasonPath { get; private set; }
        public MediaDictionary MediaDictionary { get; private set; }
        public string MediaType { get; private set; }
        public string Mode { get; private set; }

        public DestinationDirectoryClass(bool basePathFlag, MediaDictionary mediaDictionary, string mode)
        {
            DestinationDirectory = "";
            BasePath = basePathFlag ? "temp" : ""; // base_path true in original, create_new_base_path sets it
            MediaPath = "";
            SeasonPath = "";
            MediaDictionary = mediaDictionary;
            MediaType = MediaDictionary.MediaType;
            Mode = mode;

            if (basePathFlag)
            {
                BasePath = CreateNewBasePath();
            }

            MediaPath = CreateMediaPath();
            SeasonPath = CreateSeasonPath(MediaDictionary);

            DestinationDirectory = CreateDestinationDirectory();
        }

        public static string CreateSeasonPath(MediaDictionary md)
        {
            string seasonPath = "";
            if (md.MediaType == "Movie")
            {
                seasonPath = "";
            }
            else if (md.MediaType != "Movie")
            {
                if (int.TryParse(md.Season, out int seasonInt))
                {
                    if (seasonInt < 10) seasonPath = $"Season 0{seasonInt}";
                    if (seasonInt > 9) seasonPath = $"Season {seasonInt}";
                }
                else
                {
                    Console.WriteLine("Season path creation error: Season is not a valid integer.");
                }
            }
            else
            {
                Console.WriteLine("Season path creation error.");
            }
            return seasonPath;
        }

        private string CreateNewBasePath()
        {
            string basePath = "";
            if (MediaType == "TV")
            {
                basePath = @"X:\TV Shows";
            }
            else if (MediaType == "Anime")
            {
                basePath = @"X:\Anime\Shows";
            }
            else if (MediaType == "Movie")
            {
                basePath = @"X:\Movies";
            }
            else
            {
                Console.WriteLine("Error");
            }
            return basePath;
        }

        private string CreateMediaPath()
        {
            var md = MediaDictionary;
            string mediaPath = $"{md.ShowName} [{md.Scene}][{md.Resolution}][{md.Source}][{md.VideoFormat}][{md.AudioFormat}]";
            return mediaPath;
        }

        private string CreateDestinationDirectory()
        {
            string destinationDirectory = "";
            if (Mode == "Rename")
            {
                destinationDirectory = MediaDictionary.SourceDirectory;
                Console.WriteLine(destinationDirectory);
                return destinationDirectory;
            }
            else if (Mode == "Hardlink" || Mode == "Preview")
            {
                destinationDirectory = Path.Combine(BasePath, MediaPath, SeasonPath);
                Console.WriteLine(destinationDirectory);
                return destinationDirectory;
            }
            return destinationDirectory;
        }
    }
}
