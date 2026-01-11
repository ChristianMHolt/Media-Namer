import json
import re
import tkinter as tk
from urllib.error import HTTPError, URLError
from urllib.parse import quote_plus
from urllib.request import urlopen
try:
    import ttkbootstrap as ttk
    from ttkbootstrap.constants import DANGER, INFO, SUCCESS
except ImportError:  # pragma: no cover - used only when ttkbootstrap isn't installed
    ttk = None
    DANGER = INFO = SUCCESS = None

from tkinter import scrolledtext

INVALID_CHARS = r'[<>:"/\\|?*]'
RESERVED_BASENAMES = re.compile(r'^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$', re.I)
DATE_LINE = re.compile(r'^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)[a-z]*\s+\d{1,2}\s+\d{4}$', re.I)
IGNORE_NOTES = re.compile(r'(Finale|Remux|HDTV|Blu-?ray|WEB(?:[- ]DL)?|x26[45]|1080p|720p|2160p|4K)', re.I)

def make_windows_safe(name: str) -> str:
    clean = re.sub(INVALID_CHARS, '', name).replace('.', '')
    clean = re.sub(r'\s+', ' ', clean).strip()
    clean = clean.rstrip(' ')
    if RESERVED_BASENAMES.match(clean):
        clean += '_'
    return clean

def is_episode_title_line(line: str) -> bool:
    if not line:
        return False
    if re.fullmatch(r'\d+', line):
        return False
    if DATE_LINE.match(line):
        return False
    if IGNORE_NOTES.search(line):
        return False
    return True

class EpisodeExtractor:
    def __init__(self, parent_app):
        if ttk is None:
            raise ImportError(
                "ttkbootstrap is required to create the EpisodeExtractor UI."
            )
        self.parent_app = parent_app
        self.window = tk.Toplevel(self.parent_app.screen)
        self.window.title("Episode Name Extractor")
        self.window.geometry("1000x700")
        self.window.minsize(900, 600)

        header_frame = ttk.Frame(self.window)
        header_frame.pack(fill="x", padx=10, pady=(10, 0))

        self.count_label = ttk.Label(
            header_frame,
            text="Episodes extracted: 0",
            font=("Segoe UI", 11),
        )
        self.count_label.pack(side="left")

        ttk.Label(
            self.window,
            text="Paste your raw episode list:",
            font=("Segoe UI", 12, "bold"),
        ).pack(pady=5)
        self.text_input = scrolledtext.ScrolledText(self.window, width=110, height=20, font=("Consolas", 10))
        self.text_input.pack(padx=10, pady=5, fill="both", expand=True)

        button_frame = ttk.Frame(self.window)
        button_frame.pack(pady=10)

        ttk.Button(
            button_frame,
            text="Fetch Episode Names Online",
            bootstyle=INFO,
            command=self.fetch_episode_names_online,
        ).pack(side="left", padx=5)
        ttk.Button(
            button_frame,
            text="Process & Save",
            bootstyle=SUCCESS,
            command=self.process_and_save,
        ).pack(side="left", padx=5)

        ttk.Label(self.window, text="Comma-delimited output:", font=("Segoe UI", 12, "bold")).pack(pady=5)
        self.output_text = scrolledtext.ScrolledText(
            self.window,
            width=110,
            height=8,
            font=("Consolas", 10),
        )
        self.output_text.pack(padx=10, pady=5, fill="both", expand=True)

        ttk.Button(
            self.window,
            text="Use Manual Episode Names",
            bootstyle=INFO,
            command=self.override_episode_names,
        ).pack(pady=(0, 10))

    def process_and_save(self):
        raw_text = self.text_input.get("1.0", tk.END)
        lines = [ln.strip() for ln in raw_text.splitlines()]

        titles = []
        for ln in lines:
            if is_episode_title_line(ln):
                safe = make_windows_safe(ln)
                if safe:
                    titles.append(safe)

        if self.parent_app.flipped_var.get() == 0:
            titles.reverse()

        # Update state
        self._update_episode_list(titles)

        # Show output
        result = ",".join(titles)
        self.output_text.delete("1.0", tk.END)
        self.output_text.insert(tk.END, result)
        self._update_status_label(titles, manual_override=False)

    def override_episode_names(self):
        raw_text = self.output_text.get("1.0", tk.END)
        titles = parse_manual_episode_titles(raw_text)

        self._update_episode_list(titles)
        self._update_status_label(titles, manual_override=True)

        if titles:
            print("Episode list overridden using manual comma-delimited input.", flush=True)
        else:
            print("Manual override cleared the episode list.", flush=True)

    def _update_episode_list(self, titles):
        # Persist the list back to the parent app.
        self.parent_app.media_data_dict["Episode List"] = titles

    def _update_status_label(self, titles, manual_override: bool):
        if titles:
            if manual_override:
                status_text = f"Episodes ready (manual): {len(titles)}"
            else:
                status_text = f"Episodes extracted: {len(titles)}"
            self.count_label.config(text=status_text, bootstyle=SUCCESS)
        else:
            self.count_label.config(text="Episodes extracted: 0", bootstyle=DANGER)

    def fetch_episode_names_online(self):
        show_name = self._get_show_name()
        season_number = self._get_season_number()

        if not show_name or season_number is None:
            return

        try:
            titles = fetch_episode_titles_from_tvmaze(show_name, season_number)
        except (HTTPError, URLError, ValueError) as exc:
            self._set_status_error(f"Online lookup failed: {exc}")
            return

        if not titles:
            self._set_status_error(f"No episodes found for {show_name} season {season_number}.")
            return

        if self.parent_app.flipped_var.get() == 0:
            titles.reverse()

        self._update_episode_list(titles)

        result = ",".join(titles)
        self.output_text.delete("1.0", tk.END)
        self.output_text.insert(tk.END, result)
        self.count_label.config(text=f"Episodes fetched: {len(titles)}", bootstyle=SUCCESS)

    def _get_show_name(self):
        raw_name = ""
        if hasattr(self.parent_app, "tkinter_show_name"):
            raw_name = self.parent_app.tkinter_show_name.get()
        if not raw_name and self.parent_app.media_data_dict.get("Show Name"):
            raw_name = self.parent_app.media_data_dict["Show Name"]
        show_name = raw_name.strip()
        if not show_name or show_name == "Enter show name:":
            self._set_status_error("Enter a show name before fetching episodes.")
            return ""
        return show_name

    def _get_season_number(self):
        raw_season = ""
        if hasattr(self.parent_app, "season_var"):
            raw_season = self.parent_app.season_var.get()
        if not raw_season and self.parent_app.media_data_dict.get("Season"):
            raw_season = self.parent_app.media_data_dict["Season"]
        season_text = raw_season.strip()
        if not season_text or season_text == "Enter Season:":
            self._set_status_error("Enter a season number before fetching episodes.")
            return None
        try:
            season_number = int(season_text)
        except ValueError:
            self._set_status_error("Season must be a number.")
            return None
        if season_number < 0:
            self._set_status_error("Season must be a positive number.")
            return None
        return season_number

    def _set_status_error(self, message: str):
        self.count_label.config(text=message, bootstyle=DANGER)
        print(message, flush=True)


def parse_manual_episode_titles(raw_text: str):
    """Parse a comma-delimited string into sanitized episode titles."""
    if not raw_text:
        return []

    titles = []
    for part in raw_text.split(","):
        cleaned_part = make_windows_safe(part.strip())
        if cleaned_part:
            titles.append(cleaned_part)
    return titles


def fetch_episode_titles_from_tvmaze(show_name: str, season_number: int):
    """Fetch episode titles from TVMaze for the requested season."""
    if not show_name:
        raise ValueError("Show name is required.")

    url = f"https://api.tvmaze.com/singlesearch/shows?q={quote_plus(show_name)}&embed=episodes"
    with urlopen(url, timeout=10) as response:
        payload = json.load(response)

    episodes = payload.get("_embedded", {}).get("episodes", [])
    season_matches = [
        episode for episode in episodes if episode.get("season") == season_number
    ]
    season_matches.sort(key=lambda episode: episode.get("number") or 0)

    titles = []
    for episode in season_matches:
        name = make_windows_safe(str(episode.get("name", "")).strip())
        if name:
            titles.append(name)
    return titles
