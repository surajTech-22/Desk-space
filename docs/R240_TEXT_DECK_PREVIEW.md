# R240 — bounded file/code text preview in the existing DeskSpace Deck

This change adds a **user-opened, read-only** excerpt view to the existing Deck file preview for .txt, .md, .csv, .tsv, .json, .xml, .yaml/.yml, .log, .ini, .cs, .py, .ps1, .js, .css, .html and .sql. It does not add a new module or duplicate the Deck.

A source file must exist, be an ordinary non-reparse-point file, and be at most **2 MiB**. At most **3600 characters** are surfaced, decoded as validated UTF-8 or BOM-recognized Unicode. Binary/control character data is rejected; content is shown literally in a read-only WPF text area and never executed, evaluated, transformed, indexed, uploaded or copied automatically. No permanent text history or background watcher is created. The view loads only when WPF opens the Deck preview and clears plaintext on unload. Missing, invalid, oversized or locked files show a failure status rather than crashing the widget.

Existing native PDF and ZIP previews are retained. Strict static tests prevent execution/writes, and native Windows WPF smoke tests use disposable real files to verify allowlisted extension selection, ordinary content, truncation, null-byte blocking, oversize/missing rejection and WPF control integration.

**Release:** This is a development feature. A green GitHub Windows CI is necessary, not sufficient for the consolidated Stable + Tester publication; full release manifest gates and real-device UI acceptance remain mandatory.
