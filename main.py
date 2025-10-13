import os
import sys
import ctypes

from tkinter_app import TkinterApp
from destination_directory import DestinationDirectory

if sys.platform == "win32":
    ctypes.windll.kernel32.FreeConsole()

app = TkinterApp()

app.screen.mainloop()

