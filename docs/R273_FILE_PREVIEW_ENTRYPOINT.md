# R273 — directly accessible file and folder previews

The existing DeskSpace file preview already supports bounded PDF, Word DOCX, ZIP, text/code, images, and document metadata, but it is mainly reached from the Deck. R273 adds a user-invoked **Preview file or folder** option to the Workspace Features tab and the Action Palette. It uses the existing native file/folder selectors and delegates to the same vetted `DeskSpaceFilePreviewV85.Create` handlers. There is no new parser, automatic scan, file write, network upload, extraction, shell execution, or background clipboard capture.

The selection gate rejects absent paths, reparse-point or system-protected targets, and files bigger than 256MB; user can clear the preview. The existing engine only exposes its explicit **Open file or folder** button for actual external opening. On failure a bounded error appears in the preview window instead of closing Workspace.

Native Windows tests use disposable files inside the Downloads folder to verify ordinary-file/folder acceptance, missing-path rejection, large-file preflight, and real chooser controls. Source parity and .NET build must pass before marking development verified. Actual installed-device preview checks remain pending. Stable/Tester are unchanged.
