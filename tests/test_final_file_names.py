import os

from final_file_names import FinalFileNames


def test_final_file_names_handles_absent_dual_audio(tmp_path):
    destination = tmp_path / "output"
    destination.mkdir()

    media_dictionary = {
        "Show Name": "Example Show",
        "Media Type": "TV",
        "Episode Offset": "0",
        "Episode List": ["Episode 1"],
    }

    final_names = FinalFileNames(str(destination), media_dictionary)

    assert final_names.final_file_names == [
        os.path.join(str(destination), "Example Show - 01 - Episode 1.mkv")
    ]
    assert "[Dual Audio]" not in final_names.final_episode_names[0]
