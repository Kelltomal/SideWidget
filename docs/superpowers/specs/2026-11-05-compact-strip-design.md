# Compact strip

User request: detect a maximized/fullscreen application on the widget's monitor,
shrink the widget, retain time and date, ignore hover and single click, and open
only on double click. Alpha is 30% opacity.

Use a 23-DIP native layered strip, at the existing collapsed width and position,
with time and date in bundled Medeo. Keep the normal panel opaque. A separate
small native HWND makes true desktop transparency possible without changing
WinUI/WebView composition. Reuse the existing panel reveal after double click.
An alternative of clipping the WinUI header loses text and does not provide
desktop transparency; hiding the widget entirely loses access.

The strip is WS_EX_TRANSPARENT. A low-level mouse observer installed only while the strip is visible passes the first click through and consumes the second down/up pair of a double click. Use Windows double-click time
and distance settings. Collapse returns to strip while the covering window
remains active; restoring/switching away from it restores the ordinary header.
Native dialogs and active drag/resize are protected. Other monitors do not
trigger this mode. No new decorative animation or dependency.

