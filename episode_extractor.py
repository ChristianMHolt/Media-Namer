import re
import tkinter as tk
import ttkbootstrap as ttk
from tkinter import scrolledtext
from ttkbootstrap.constants import *

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

        ttk.Button(self.window, text="Process & Save", bootstyle=SUCCESS, command=self.process_and_save).pack(pady=10)

        ttk.Label(self.window, text="Comma-delimited output:", font=("Segoe UI", 12, "bold")).pack(pady=5)
        self.output_text = scrolledtext.ScrolledText(self.window, width=110, height=8, font=("Consolas", 10), state="disabled")
        self.output_text.pack(padx=10, pady=5, fill="both", expand=True)

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

        # Save to main app
        self.parent_app.media_data_dict["Episode List"] = titles

        # Show output
        result = ",".join(titles)
        self.output_text.config(state="normal")
        self.output_text.delete("1.0", tk.END)
        self.output_text.insert(tk.END, result)
        self.output_text.config(state="disabled")

        if titles:
            self.count_label.config(text=f"Episodes extracted: {len(titles)}", bootstyle=SUCCESS)
        else:
            self.count_label.config(text="Episodes extracted: 0", bootstyle=DANGER)
