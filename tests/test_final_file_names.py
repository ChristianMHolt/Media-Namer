import os

from final_file_names import FinalFileNames


def test_final_file_names_handles_missing_dual_audio(tmp_path):
    media_dictionary = {
        "Show Name": "Sample Show",
        "Media Type": "TV",
        "Episode Offset": "0",
        "Episode List": ["Episode 1", "Episode 2"],
    }

    final_file_names = FinalFileNames(str(tmp_path), media_dictionary)

    expected_files = [
        os.path.join(str(tmp_path), "Sample Show - 01 - Episode 1.mkv"),
        os.path.join(str(tmp_path), "Sample Show - 02 - Episode 2.mkv"),
    ]

    assert final_file_names.final_file_names == expected_files
