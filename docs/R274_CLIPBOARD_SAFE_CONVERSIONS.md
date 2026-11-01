# R274 — Clipboard conversions: bad input recovery and safe exports

DeskSpace Clipboard conversions previously caught only `ArgumentException` during preview, while user-entered malformed JSON typically throws `JsonException` and malformed CSV throws `FormatException`. This could propagate from a selection change in the WPF UI. The existing preview now catches all three expected input failures and displays a useful message while leaving copy/save disabled. The Windows clipboard is not changed on invalid input.

Added three on-demand transformations to the existing menu: **Minify JSON**, **Sort lines** (case-insensitive deterministic order), and **Trim lines** (preserves line structure). Preexisting Format JSON, Unique lines and CSV→Markdown modes remain unchanged. All transforms are in-memory and use their existing bounded character budgets. No passive clipboard monitor, cloud access, silent paste or storage of clipboard contents.

Save As now writes UTF-8 to a distinct temporary file in the chosen destination directory, then atomically moves it into place after the Windows save confirmation, cleaning up a failed staging file. It no longer truncates an existing export if a write fails halfway through. The output file is created only by an explicit user action.

Native Windows tests cover JSON pretty/minify, sorting, trimming, unique lines regression, malformed JSON/CSV exceptions, and actual clipboard-tool UI controls. Static checks require the error handler and atomic Save As. The source hash-parity check and WPF compilation gate remain mandatory. Still not shipped until installed-PC acceptance and the consolidated release gate.
