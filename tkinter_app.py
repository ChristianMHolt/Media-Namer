import os
import sys
import tkinter as tk
import ttkbootstrap as ttk
from tkinter import filedialog, scrolledtext
from tkinter_tool_tip import ToolTip
import destination_directory
from destination_directory import DestinationDirectory
from final_file_names import FinalFileNames
from source_directory_file_list import SourceDirectoryFileList
from episode_extractor import EpisodeExtractor


def add_placeholder(entry_widget: tk.Widget, placeholder_text: str):
    """
    Simulated placeholder for ttk.Entry widgets.
    Shows grey placeholder text until the field is focused; clears on focus-in; restores on focus-out if empty.
    """
    # If the widget already has the placeholder text (via StringVar default), set it grey.
    try:
        current = entry_widget.get()
    except Exception:
        current = ""

    def on_focus_in(_evt):
        if entry_widget.get() == placeholder_text:
            entry_widget.delete(0, tk.END)
            entry_widget.config(foreground="black")

    def on_focus_out(_evt):
        if not entry_widget.get().strip():
            entry_widget.delete(0, tk.END)
            entry_widget.insert(0, placeholder_text)
            entry_widget.config(foreground="gray")

    # If current matches our placeholder, color grey; else leave as-is
    if current == placeholder_text:
        entry_widget.config(foreground="gray")

    entry_widget.bind("<FocusIn>", on_focus_in)
    entry_widget.bind("<FocusOut>", on_focus_out)


class TextRedirector:
    def __init__(self, text_widget: tk.Text, fallback_stream):
        self.text_widget = text_widget
        self._fallback_stream = fallback_stream

    def _write_to_widget(self, text: str):
        self.text_widget.configure(state="normal")
        self.text_widget.insert(tk.END, text)
        self.text_widget.see(tk.END)
        self.text_widget.configure(state="disabled")

    def write(self, text: str):
        if not text:
            return
        try:
            self._write_to_widget(text)
        except (tk.TclError, RuntimeError):
            # The widget has likely been destroyed; fall back to the original stream.
            if self._fallback_stream is not None:
                self._fallback_stream.write(text)

    def flush(self):
        try:
            self.text_widget.update_idletasks()
        except (tk.TclError, RuntimeError):
            if self._fallback_stream is not None:
                self._fallback_stream.flush()


class TkinterApp:

    def __init__(self):
        # Window
        self.screen = ttk.Window(themename='journal')
        self.screen.title('Format Episode Names')
        self.screen.geometry('1250x400')

        # Notebook and frames
        self.notebook = ttk.Notebook(self.screen)
        self.main_frame = ttk.Frame(self.notebook)
        self.terminal_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.main_frame, text="Main")
        self.notebook.add(self.terminal_frame, text="Terminal")
        self.notebook.pack(fill=tk.BOTH, expand=True)

        # Terminal output widget
        self.terminal_output = scrolledtext.ScrolledText(self.terminal_frame, wrap=tk.WORD)
        self.terminal_output.pack(fill=tk.BOTH, expand=True)
        self.terminal_output.configure(state="disabled")
        self._original_stdout = sys.stdout
        self._original_stderr = sys.stderr
        self.stdout_redirector = TextRedirector(self.terminal_output, self._original_stdout)
        self.stderr_redirector = TextRedirector(self.terminal_output, self._original_stderr)
        sys.stdout = self.stdout_redirector
        sys.stderr = self.stderr_redirector

        # Ensure stdout/stderr are restored when the window closes.
        self.screen.protocol("WM_DELETE_WINDOW", self.on_close)

        # variables for buttons and entries
        self.directory_var = tk.StringVar(value="Select Episode Directory Path")
        self.tkinter_offset = tk.IntVar(value=0)
        self.tkinter_show_name = tk.StringVar(value='Enter show name:')
        self.enter_episode_names = None
        self.media_data_dict = {}
        # Default to no dual audio so downstream consumers don't fail before the
        # checkbox is interacted with.
        self.media_data_dict["Dual Audio"] = ""
        self.dual_audio_var = tk.IntVar(value=0)
        self.flipped_var = tk.IntVar(value=0)  # leave as requested
        self.audio_format_var = tk.StringVar(value='Enter Audio Format:')
        self.video_format_var = tk.StringVar(value='Enter Video Format:')
        self.source_var = tk.StringVar(value='Enter Source:')
        self.resolution_var = tk.StringVar(value='Enter Resolution:')
        self.media_type_var = tk.StringVar(value='Enter Media Type:')
        self.scene_var = tk.StringVar(value='Enter Scene:')
        self.episode_offset_var = tk.StringVar(value='Enter Episode Offset:')
        self.season_var = tk.StringVar(value='Enter Season:')

        # Widgets
        self.directory_entry = ttk.Entry(master=self.main_frame, textvariable=self.directory_var, state='disabled')
        self.select_directory_button = ttk.Button(master=self.main_frame, text='Select Episode Directory',
                                                  command=self.select_directory)

        self.episode_name_popup_button = ttk.Button(master=self.main_frame, text='Input Episode Names',
                                                    command=self.popup_ep_names_input_window)

        self.show_name_entry = ttk.Entry(master=self.main_frame, textvariable=self.tkinter_show_name)
        self.dual_audio_checkbox = ttk.Checkbutton(master=self.main_frame, text='Dual Audio?', variable=self.dual_audio_var,
                                                   command=self.determine_dual_audio)

        self.flipped_checkbox = ttk.Checkbutton(master=self.main_frame, text='Reverse episode order?',
                                                variable=self.flipped_var)

        self.hardlink_button = ttk.Button(master=self.main_frame, text="Hardlink",
                                          command=lambda: self.run_script(mode="Hardlink"))
        self.rename_button = ttk.Button(master=self.main_frame, text="Rename",
                                        command=lambda: self.run_script(mode="Rename"))
        self.preview_button = ttk.Button(master=self.main_frame, text="Preview",
                                         command=lambda: self.run_script(mode="Preview"))
        self.audio_format_entry = ttk.Entry(master=self.main_frame, textvariable=self.audio_format_var)
        self.video_format_entry = ttk.Entry(master=self.main_frame, textvariable=self.video_format_var)
        self.source_entry = ttk.Entry(master=self.main_frame, textvariable=self.source_var)
        self.resolution_entry = ttk.Entry(master=self.main_frame, textvariable=self.resolution_var)
        self.media_type_entry = ttk.Entry(master=self.main_frame, textvariable=self.media_type_var)
        self.scene_entry = ttk.Entry(master=self.main_frame, textvariable=self.scene_var)
        self.episode_offset_entry = ttk.Entry(master=self.main_frame, textvariable=self.episode_offset_var)
        self.season_entry = ttk.Entry(master=self.main_frame, textvariable=self.season_var)

        # Display
        self.display_widgets()

        # Apply placeholders (keep your StringVars; just style as placeholder + clear/restore on focus)
        add_placeholder(self.show_name_entry, 'Enter show name:')
        add_placeholder(self.audio_format_entry, 'Enter Audio Format:')
        add_placeholder(self.video_format_entry, 'Enter Video Format:')
        add_placeholder(self.source_entry, 'Enter Source:')
        add_placeholder(self.resolution_entry, 'Enter Resolution:')
        add_placeholder(self.media_type_entry, 'Enter Media Type:')
        add_placeholder(self.scene_entry, 'Enter Scene:')
        add_placeholder(self.episode_offset_entry, 'Enter Episode Offset:')
        add_placeholder(self.season_entry, 'Enter Season:')

        # Tool Tips
        ToolTip(self.show_name_entry, "The name of the show.")
        ToolTip(self.audio_format_entry, "e.g., FLAC, DTS, OPUS, AAC.")
        ToolTip(self.video_format_entry, "e.g., AVC, H.264, H.265.")
        ToolTip(self.source_entry, "e.g., BluRay, Disk, WEB-DL.")
        ToolTip(self.resolution_entry, "e.g., 720p, 800p, 1080p, 2160p.")
        ToolTip(self.media_type_entry, "e.g., Movie, TV, or Anime.")
        ToolTip(self.scene_entry, "e.g., Zaki, SubsPlease, Beatrice-Raws.")
        ToolTip(self.episode_offset_entry, "The number for the first episode -1.")
        ToolTip(self.season_entry, "The season the episodes are apart of.")

        # Custom Tab order (left-to-right, row-by-row as requested)
        self.tab_order = [
            self.show_name_entry,     # 1
            self.source_entry,        # 2
            self.season_entry,        # 3
            self.scene_entry,         # 4
            self.video_format_entry,  # 5
            self.episode_offset_entry,# 6
            self.resolution_entry,    # 7
            self.audio_format_entry,  # 8
            self.media_type_entry,    # 9
        ]
        for i, widget in enumerate(self.tab_order):
            widget.bind("<Tab>", lambda e, idx=i: self.focus_next(e, idx))
            widget.bind("<Shift-Tab>", lambda e, idx=i: self.focus_prev(e, idx))

    def save_labels(self, *args):
        # Update the label with the current entry value
        self.media_data_dict["Audio Format"] = self.audio_format_var.get()
        self.media_data_dict["Video Format"] = self.video_format_var.get()
        self.media_data_dict["Source"] = self.source_var.get()
        self.media_data_dict["Resolution"] = self.resolution_var.get()
        self.media_data_dict["Media Type"] = self.media_type_var.get()
        self.media_data_dict["Scene"] = self.scene_var.get()
        self.media_data_dict["Episode Offset"] = self.episode_offset_var.get()
        self.media_data_dict["Show Name"] = self.tkinter_show_name.get()
        self.media_data_dict["Season"] = self.season_var.get()
        # print(self.media_data_dict["Episode Offset"])
        # print(self.media_data_dict["Audio Format"])

    def select_directory(self):
        selected_directory = filedialog.askdirectory(initialdir=r"X:\Downloads")
        if selected_directory:
            self.directory_var.set(selected_directory)
            self.media_data_dict["Source Directory"] = selected_directory
            print(selected_directory, flush=True)

    def popup_ep_names_input_window(self):
        # Opens the extractor window; it will write back to self.media_data_dict["Episode List"]
        EpisodeExtractor(self)

    def display_widgets(self):
        # Display widgets
        # Row 0
        self.directory_entry.grid(row=0, column=0, padx=5, pady=10)
        self.show_name_entry.grid(row=0, column=1, padx=5, pady=10)
        self.source_entry.grid(row=0, column=2, padx=5, pady=10)
        self.season_entry.grid(row=0, column=3, padx=5, pady=10)

        # Row 1
        self.select_directory_button.grid(row=1, column=0, padx=5, pady=10)
        self.scene_entry.grid(row=1, column=1, padx=5, pady=10)
        self.video_format_entry.grid(row=1, column=2, padx=5, pady=10)
        self.episode_offset_entry.grid(row=1, column=3, padx=5, pady=10)

        # Row 2
        self.episode_name_popup_button.grid(row=2, column=0, padx=5, pady=10)
        self.resolution_entry.grid(row=2, column=1, padx=5, pady=10)
        self.audio_format_entry.grid(row=2, column=2, padx=5, pady=10)
        self.media_type_entry.grid(row=2, column=3, padx=5, pady=10)

        # Row 3
        self.dual_audio_checkbox.grid(row=3, column=0, padx=5, pady=10)
        self.flipped_checkbox.grid(row=3, column=1, padx=5, pady=10)
        self.hardlink_button.grid(row=3, column=2, padx=5, pady=10)
        self.rename_button.grid(row=3, column=3, padx=5, pady=10)

        # Row 4
        self.preview_button.grid(row=4, column=2, padx=5, pady=10)

    def run_script(self, mode):
        self.save_labels()

        # Creates the destination directory and assigns its path value to the media data dictionary.
        destination_directory = DestinationDirectory(True, self.media_data_dict, mode)
        self.media_data_dict["DestinationDirectory"] = destination_directory.destination_directory
        # print(self.media_data_dict["DestinationDirectory"])

        # Creates the episodes final names as a list and assigns that value to the media data dictionary
        final_files = FinalFileNames(self.media_data_dict["DestinationDirectory"], self.media_data_dict)
        self.media_data_dict["Final Files"] = final_files.final_file_names
        # print(self.media_data_dict["Final Files"])

        # Creates a list of all the files in the source directory and assigns that value to the media data dictionary
        source_file_list = SourceDirectoryFileList(self.media_data_dict)
        self.media_data_dict["Source Files"] = source_file_list.media_dictionary["Source Files"]
        # print(self.media_data_dict["Source Files"])

        # Ensures the destination directory exists and creates it if not when performing actions
        # that modify the filesystem. Preview mode should only report what would happen.
        if mode != "Preview":
            self.check_directory_exists(self.media_data_dict["DestinationDirectory"])

        if mode == "Hardlink":
            self.hardlink_files()
        elif mode == "Rename":
            self.rename_files()
        elif mode == "Preview":
            self.preview_files()

    def preview_files(self):
        print("These are the episode names:")
        for episode in self.media_data_dict["Episode List"]:
            print(episode)
        print("These are the final files:")
        for file in self.media_data_dict["Final Files"]:
            print(file)
        print("\nThese are the source files:")
        for file in self.media_data_dict["Source Files"]:
            print(file)

    def rename_files(self):
        md = self.media_data_dict
        show_source_path = os.path.dirname(md["Source Directory"])
        media_source_path = os.path.dirname(show_source_path)
        renamed_show_source_path = os.path.join(media_source_path, f"{md['Show Name']} [{md['Scene']}][{md['Resolution']}][{md['Source']}][{md['Video Format']}][{md['Audio Format']}]")
        season_path = destination_directory.create_season_path(self.media_data_dict)
        new_source_directory = os.path.join(show_source_path, season_path)
        # print(f"This is the show source path: {show_source_path}")
        # print(f"This is the media source path: {media_source_path}")
        # print(f"This is the renamed show source path: {renamed_show_source_path}")
        for i in range(len(md["Source Files"])):
            os.rename(md["Source Files"][i], md["Final Files"][i])
        os.rename(md["Source Directory"], new_source_directory)
        self.media_data_dict["Source Directory"] = new_source_directory
        show_source_path = os.path.dirname(new_source_directory)
        os.rename(show_source_path, renamed_show_source_path)

    def hardlink_files(self):
        for i in range(len(self.media_data_dict["Source Files"])):
            os.link(self.media_data_dict["Source Files"][i], self.media_data_dict["Final Files"][i])

    def check_directory_exists(self, directory):
        if os.path.exists(directory) and os.path.isdir(directory):
            print(f"\nDirectory '{self}' already exists.")
        else:
            os.makedirs(directory)

    def determine_dual_audio(self):
        # print(self.dual_audio_var.get())
        if self.dual_audio_var.get() == 1:
            self.media_data_dict["Dual Audio"] = "Dual Audio"
            # print(self.media_data_dict)
        elif self.dual_audio_var.get() == 0:
            self.media_data_dict["Dual Audio"] = ""
            # print(self.media_data_dict)
        else:
            print("Error")
            self.media_data_dict["Dual Audio"] = ""

    # ---- Custom Tab Navigation ----
    def focus_next(self, event, idx):
        next_idx = (idx + 1) % len(self.tab_order)
        self.tab_order[next_idx].focus_set()
        return "break"

    def focus_prev(self, event, idx):
        prev_idx = (idx - 1) % len(self.tab_order)
        self.tab_order[prev_idx].focus_set()
        return "break"

    def restore_streams(self):
        if sys.stdout is self.stdout_redirector:
            sys.stdout = self._original_stdout
        if sys.stderr is self.stderr_redirector:
            sys.stderr = self._original_stderr

    def on_close(self):
        self.restore_streams()
        self.screen.destroy()

    def __del__(self):
        # In case the widget is garbage-collected without the close handler running.
        self.restore_streams()
