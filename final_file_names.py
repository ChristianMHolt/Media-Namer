import os

class FinalFileNames:

    def __init__(self, destination_directory, media_dictionary):
        self.destination_directory = destination_directory
        self.media_dictionary = media_dictionary
        # When the GUI hasn't toggled the dual-audio checkbox yet, the
        # media dictionary won't have a "Dual Audio" entry. Default to an empty
        # string so callers that don't care about dual audio can still generate
        # file names without crashing.
        self.dual_audio = self.media_dictionary.get("Dual Audio", "")
        self.show_name = self.media_dictionary["Show Name"]
        self.media_type = self.media_dictionary["Media Type"]
        self.offset = self.media_dictionary["Episode Offset"]
        self.episode_list = self.media_dictionary["Episode List"]
        self.final_episode_names = []
        self.final_file_names = []

        self.sanitize_all_episodes()
        self.create_detailed_episode_names()
        self.create_final_file_names()

    def sanitize_episode_name(self, episode):
        illegal_chars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '#', '%', "!", "@"]
        for char in illegal_chars:
            if char in episode:
                episode = episode.replace(char, "")
        # print(episode_name)
        return episode

    def sanitize_all_episodes(self):
        # print(self.episode_list)
        for i in range(len(self.episode_list)):
            # print(self.episode_list[i])
            self.episode_list[i] = self.sanitize_episode_name(self.episode_list[i])

    def create_detailed_episode_names(self):
        offset = self.determine_episode_0_offset()
        for i in range(len(self.episode_list)):
            detailed_episode_name = self.determine_dual_audio()
            detailed_episode_name = self.determine_show_name(detailed_episode_name)
            detailed_episode_name = self.add_0s_to_proper_name(offset, i, self.media_type, detailed_episode_name)
            self.final_episode_names.append(f"{detailed_episode_name}{self.episode_list[i]}")
        # print(f"These are the detailed episode names:\n{self.episode_list}")

    def determine_episode_0_offset(self):
        numbers_of_episodes = len(self.episode_list) + int(self.offset)
        if numbers_of_episodes <= 99:
            zero_offset = 0
        elif numbers_of_episodes > 99:
            zero_offset = 1
        elif numbers_of_episodes > 999:
            zero_offset = 2
        return zero_offset

    def add_0s_to_proper_name(self, episode_0_offset, count, MediaType, detailed_episode_name):
        count += int(self.offset)
        # Add nothing if it's a movie
        if MediaType == 'Movie':
            return detailed_episode_name

        # If episode count is less than 100
        if episode_0_offset == 0 and count+1 < 10:
            detailed_episode_name += f" - 0{count+1} - "  # Add zero in front of episode number if the episode number is < than 10
        elif episode_0_offset == 0 and count+1 >= 10:
            detailed_episode_name += f" - {count+1} - "  # Add nothing in front of episode number if the episode number is > than 10

        # If episode count is less than 1000
        elif episode_0_offset == 1 and count+1 < 10:
            detailed_episode_name += f" - 00{count+1} - "
        elif episode_0_offset == 1 and count+1 < 100:
            detailed_episode_name += f" - 0{count+1} - "
        elif episode_0_offset == 1 and count+1 >= 100:
            detailed_episode_name += f" - {count+1} - "

        # If episode count is less than 10000
        elif episode_0_offset == 2 and count+1 < 10:
            detailed_episode_name += f" - 000{count+1} - "
        elif episode_0_offset == 2 and count+1 < 100:
            detailed_episode_name += f" - 00{count+1} - "
        elif episode_0_offset == 2 and count+1 < 1000:
            detailed_episode_name += f" - 0{count+1} - "
        elif episode_0_offset == 2 and count+1 >= 1000:
            detailed_episode_name += f" - {count+1} - "

        return detailed_episode_name

    def determine_show_name(self, detailed_episode_name_part_2):
        if self.media_type == "Movie":
            return detailed_episode_name_part_2
        elif self.media_type != "Movie":
            detailed_episode_name_part_2 += f"{self.show_name}"
            return detailed_episode_name_part_2
        else:
            print("Final Files, Determine Media-Type, Error")
            detailed_episode_name_part_2 += f"{self.show_name}"
            return detailed_episode_name_part_2

    def determine_dual_audio(self):
        if self.dual_audio == "Dual Audio":
            detailed_episode_names_part_1 = "[Dual Audio] "
        elif self.dual_audio == "":
            detailed_episode_names_part_1 = ""
        else:
            print("Final Files Dual Audio Error")
            detailed_episode_names_part_1 = ""
        return detailed_episode_names_part_1

    def create_final_file_names(self):
        for i in range(len(self.episode_list)):
            self.final_file_names.append(os.path.join(self.destination_directory, f"{self.final_episode_names[i]}.mkv"))
        print(self.final_file_names)
