import os

from source_directory_file_list import SourceDirectoryFileList


def test_filters_to_mkv_and_mp4(tmp_path):
    source_dir = tmp_path / "episodes"
    source_dir.mkdir()

    mkv_file = source_dir / "episode1.mkv"
    mp4_file = source_dir / "episode2.MP4"
    avi_file = source_dir / "episode3.avi"

    mkv_file.write_text("video1")
    mp4_file.write_text("video2")
    avi_file.write_text("video3")

    media_dictionary = {"Source Directory": str(source_dir)}

    SourceDirectoryFileList(media_dictionary)

    assert set(media_dictionary["Source Files"]) == {
        os.path.normpath(str(mkv_file)),
        os.path.normpath(str(mp4_file)),
    }
