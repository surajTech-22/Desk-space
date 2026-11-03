# R365 — Direct application opening only (DeskSpace)

This supersedes R363's optional **Open with** suggestions after a file drop.

## Exact user behavior
Drag **one file** onto the DeskSpace circular widget:
- Detect its file type, consult the actual signed-in Windows `open` file association, and immediately open that **same source file** in its associated installed application.
- Never stage, copy, move, rename, ZIP, import, restore, parse, rewrite, reindex, or upload the file.
- Do not show a new application-choice panel. No 'launch an unrelated app without its file' fallback.
- Existing ZIP/Copy drag workflow remains for **unrecognized ordinary folders only**.
- For a strongly recognized project/Winman folder, open its **unambiguous recognized source file** using the matching application's supported open path. Do not select one arbitrary file from several.
- When Windows has no installed registered handler, or an accounting product has no supported direct-open method, **do not claim the file opened or perform an alternate action**.

## Accounting file exclusions
- `Company.900` / `Company.1800` are Tally's multi-file company database, **not** shell documents. Tally requires its own Select Company / data path flow; do not launch `Tally.exe` and say this opened a specific company. Never call import, API, repair, or migrate.
- Winman CA ERP `.tax`, GST `.wgst` and TDS `.7EQ/.24Q/.26Q/.27Q/.27EQ`: recognize and use **only their verified Windows registered vendor open handler** to open that exact file. Do not call guessed WinTax/WinTDS command-line options.
- Winman `.tax.wb`, `.wgst.wb`, `.wt` backup files are **not** direct-open source files; no automatic restoration, copying, or backup handling.
- Generic XML/JSON is a Windows-associated document, **never** interpreted as a Tally or Winman import instruction.

## General files
- Documents, images, audio, video and custom safe extensions use Windows' current per-user registered file opener. File type recognition is not built on guesses about one specific Office or media installation.
- Executable/script-like extensions (`.exe/.bat/.cmd/.ps1/.js/.py/.msi/.lnk/.url/.hta/.dll/.reg`, etc.) are not executed via the widget.
- The source file must still exist and not be a reparse point when opening starts.

## Verification and publication
- R364 native Windows vendor-association, Tally/Winman backup, unsafe-file tests.
- Existing R363 project recognition regression.
- Direct checked-in WPF compilation.
- Shipping R232/R363/R359/R361 reconstructed WPF/Deck/Search/updater regression using R364/R365 guarded patch.
- **No installer is published by these test workflows**. A real machine with Winman installed and its native file association must still verify opening its own .tax/.wgst file; Tally company database direct-open is not promised.
