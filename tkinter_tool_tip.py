import tkinter as tk


class ToolTip:
    def __init__(self, widget, text, delay=500):
        self.widget = widget
        self.text = text
        self.delay = delay  # Delay in milliseconds before showing the tooltip
        self.tooltip_window = None
        self.id = None  # Identifier for the after method to manage the delay

        # Bind events to the widget
        self.widget.bind("<Enter>", self.schedule_tooltip)
        self.widget.bind("<Leave>", self.hide_tooltip)

    def schedule_tooltip(self, event=None):
        """Schedules the tooltip to show after the given delay."""
        self.id = self.widget.after(self.delay, self.show_tooltip)

    def show_tooltip(self, event=None):
        """Displays the tooltip window."""
        if self.tooltip_window is not None:
            return

        # Get widget coordinates
        x, y, _, _ = self.widget.bbox("insert")
        x += self.widget.winfo_rootx() + 20
        y += self.widget.winfo_rooty() + 20

        # Create a top-level window for the tooltip
        self.tooltip_window = tk.Toplevel(self.widget)
        self.tooltip_window.wm_overrideredirect(True)  # Remove window decorations
        self.tooltip_window.wm_geometry(f"+{x}+{y}")

        # Create a label inside the tooltip window
        label = tk.Label(self.tooltip_window, text=self.text, background="lightyellow", borderwidth=1, relief="solid")
        label.pack()

    def hide_tooltip(self, event=None):
        """Hides the tooltip and cancels any scheduled tooltips."""
        if self.tooltip_window:
            self.tooltip_window.destroy()
            self.tooltip_window = None

        # Cancel any scheduled tooltip if the mouse leaves before the tooltip appears
        if self.id:
            self.widget.after_cancel(self.id)
            self.id = None
