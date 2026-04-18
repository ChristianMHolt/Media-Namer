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
                BasePath = GetBasePath(MediaType);
            }

            MediaPath = CreateMediaPath();
            SeasonPath = CreateSeasonPath(MediaDictionary);

            EvaluateExistingShow();

            DestinationDirectory = CreateDestinationDirectory();
        }

		private void EvaluateExistingShow()
        {
            if (string.IsNullOrEmpty(BasePath) || !Directory.Exists(BasePath) || string.IsNullOrEmpty(MediaDictionary.ShowName))
            {
                return;
            }

            string existingPath = FindExistingShowDirectory(BasePath, MediaDictionary.ShowName);
            if (!string.IsNullOrEmpty(existingPath))
            {
                string existingFolderName = Path.GetFileName(existingPath);

                string newTags = "";
                int bracketIndex = MediaPath.IndexOf(" [");
                if (bracketIndex != -1)
                {
                    // Use Trim() to temporarily remove the leading space for clean splitting
                    newTags = MediaPath.Substring(bracketIndex).Trim();
                }

                string existingTags = "";
                int existingBracketIndex = existingFolderName.IndexOf(" [");
                if (existingBracketIndex != -1)
                {
                    existingTags = existingFolderName.Substring(existingBracketIndex).Trim();
                }

                MediaPath = existingFolderName;

                if (MediaType != "Movie" && !string.IsNullOrEmpty(newTags) && newTags != existingTags)
                {
                    // Split the tag strings into arrays, discarding the brackets
                    var newTagArray = newTags.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    var existingTagArray = existingTags.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);

                    string differingTags = "";

                    // Loop through the new tags and compare them to the existing tags at the same position
                    for (int i = 0; i < newTagArray.Length; i++)
                    {
                        if (i >= existingTagArray.Length || newTagArray[i] != existingTagArray[i])
                        {
                            // If the tag is different, or the existing array is shorter, keep this tag
                            differingTags += $"[{newTagArray[i]}]";
                        }
                    }

                    // Append only the differing tags to the SeasonPath with a leading space
                    if (!string.IsNullOrEmpty(differingTags))
                    {
                        SeasonPath += $" {differingTags}";
                    }
                }
            }
        }

        public static string FindExistingShowDirectory(string basePath, string showName)
        {
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath) || string.IsNullOrEmpty(showName))
            {
                return "";
            }

            try
            {
                var directories = Directory.GetDirectories(basePath);
                foreach (var dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName == showName || dirName.StartsWith(showName + " ["))
                    {
                        return dir;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding existing show directory: {ex.Message}");
            }
            return "";
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
            string basePath = "";
            if (mediaType == "TV")
            {
                basePath = @"X:\TV Shows";
            }
            else if (mediaType == "Anime")
            {
                basePath = @"X:\Anime\Shows";
            }
            else if (mediaType == "Movie")
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
