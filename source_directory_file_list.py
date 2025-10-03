import os

class SourceDirectoryFileList:

    def __init__(self, media_dictionary):
        self.media_dictionary = media_dictionary
        self.create_source_directory_file_list()

    def create_source_directory_file_list(self):
        # Gets all file names in the folder as a list.
        seeding_folder_path = self.media_dictionary["Source Directory"]
        #
        files = [os.path.normpath(os.path.join(seeding_folder_path, f)) for f in os.listdir(seeding_folder_path) if
                 os.path.isfile(os.path.join(seeding_folder_path, f))]
        # print(f"This is the first file in the SeedingTorrents directory: {files[0]}")
        self.media_dictionary["Source Files"] = files
