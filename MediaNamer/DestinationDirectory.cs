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

            if (!string.IsNullOrEmpty(BasePath) && !string.IsNullOrEmpty(MediaDictionary.ShowName))
            {
                string standardTag = $"[{MediaDictionary.Scene}][{MediaDictionary.Resolution}][{MediaDictionary.Source}][{MediaDictionary.VideoFormat}][{MediaDictionary.AudioFormat}]";

                if (Directory.Exists(BasePath))
                {
                    var dirs = Directory.GetDirectories(BasePath, $"{MediaDictionary.ShowName} [*");
                    if (dirs.Length > 0)
                    {
                        string existingDirName = Path.GetFileName(dirs[0]);
                        MediaPath = existingDirName;
                        if (!existingDirName.Contains(standardTag) && MediaDictionary.MediaType != "Movie")
                        {
                            SeasonPath += $" {standardTag}";
                        }
                    }
                }
            }

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

        public static string GetBasePath(string mediaType)
        {
            if (mediaType == "TV") return @"X:\TV Shows";
            if (mediaType == "Anime") return @"X:\Anime\Shows";
            if (mediaType == "Movie") return @"X:\Movies";
            return "";
        }

        private string CreateNewBasePath()
        {
            string basePath = GetBasePath(MediaType);
            if (string.IsNullOrEmpty(basePath))
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
