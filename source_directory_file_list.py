import os

class SourceDirectoryFileList:

    def __init__(self, media_dictionary):
        self.media_dictionary = media_dictionary
        self.create_source_directory_file_list()

    def create_source_directory_file_list(self):
        # Gets all mkv or mp4 file names in the folder as a list.
        seeding_folder_path = self.media_dictionary["Source Directory"]
        allowed_extensions = {".mkv", ".mp4"}

        files = [
            os.path.normpath(os.path.join(seeding_folder_path, file_name))
            for file_name in os.listdir(seeding_folder_path)
            if (
                os.path.isfile(os.path.join(seeding_folder_path, file_name))
                and os.path.splitext(file_name)[1].lower() in allowed_extensions
            )
        ]
        self.media_dictionary["Source Files"] = files
