import unittest

from episode_extractor import parse_manual_episode_titles


class ParseManualEpisodeTitlesTests(unittest.TestCase):
    def test_basic_split_and_strip(self):
        titles = parse_manual_episode_titles("Episode One, Episode Two ,Episode Three")
        self.assertEqual(titles, ["Episode One", "Episode Two", "Episode Three"])

    def test_ignores_empty_segments(self):
        titles = parse_manual_episode_titles("Episode One,, ,Episode Two")
        self.assertEqual(titles, ["Episode One", "Episode Two"])

    def test_invalid_characters_removed(self):
        titles = parse_manual_episode_titles("Ep<One>,Ep:Two?")
        self.assertEqual(titles, ["EpOne", "EpTwo"])


if __name__ == "__main__":
    unittest.main()
