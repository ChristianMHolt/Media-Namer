import os


def create_season_path(media_dictionary):
    md = media_dictionary
    if md["Media Type"] == "Movie":
        season_path = ''
    elif md["Media Type"] != "Movie":
        if int(md['Season']) < 10: season_path = f"Season 0{md['Season']}"
        if int(md['Season']) > 9: season_path = f"Season {md['Season']}"
    else:
        print("Season path creation error.")
    return season_path


class DestinationDirectory:

    def __init__(self, base_path, media_dictionary, mode):
        self.destination_directory = ''
        self.base_path = base_path
        self.media_path = ''
        self.season_path = ''
        self.media_dictionary = media_dictionary
        self.media_type = self.media_dictionary["Media Type"]
        self.mode = mode

        # print(media_dictionary)
        if self.base_path:
            self.base_path = self.create_new_base_path()

        self.media_path = self.create_media_path()
        self.season_path = create_season_path(self.media_dictionary)

        self.destination_directory = self.create_destination_directory()


    def create_new_base_path(self):
        if self.media_type == 'TV':
            base_path = 'X:\\TV Shows'
        elif self.media_type == 'Anime':
            base_path = 'X:\\Anime\\Shows'
        elif self.media_type == 'Movie':
            base_path = 'X:\\Movies'
        else:
            print("Error")
        return base_path

    def create_media_path(self):
        md = self.media_dictionary
        media_path = f"{md['Show Name']} [{md['Scene']}][{md['Resolution']}][{md['Source']}][{md['Video Format']}][{md['Audio Format']}]"
        return media_path

    def create_destination_directory(self):
        if self.mode == "Rename":
            destination_directory = self.media_dictionary["Source Directory"]
            print(destination_directory)
            return destination_directory
        elif self.mode == "Hardlink":
            destination_directory = os.path.join(self.base_path, self.media_path, self.season_path)
            print(destination_directory)
            return destination_directory
        elif self.mode == "Preview":
            destination_directory = os.path.join(self.base_path, self.media_path, self.season_path)
            print(destination_directory)
            return destination_directory
