using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MediaNamer
{
    public class SourceDirectoryFileListClass
    {
        public MediaDictionary MediaDictionary { get; private set; }

        public SourceDirectoryFileListClass(MediaDictionary mediaDictionary)
        {
            MediaDictionary = mediaDictionary;
            CreateSourceDirectoryFileList();
        }

        private void CreateSourceDirectoryFileList()
        {
            string seedingFolderPath = MediaDictionary.SourceDirectory;
            if (string.IsNullOrEmpty(seedingFolderPath) || !Directory.Exists(seedingFolderPath))
            {
                Console.WriteLine("Source directory is empty or does not exist.");
                return;
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4" };

            List<string> files = new List<string>();
            try
            {
                foreach (string filePath in Directory.GetFiles(seedingFolderPath))
                {
                    string extension = Path.GetExtension(filePath);
                    if (allowedExtensions.Contains(extension))
                    {
                        files.Add(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading source directory: {ex.Message}");
            }

            MediaDictionary.SourceFiles = files;
        }
    }
}
