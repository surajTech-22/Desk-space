# R275 — major file-operations milestone: safe Batch Rename Apply and Undo

The previous R254 module was **preview-only**. R275 adds an explicitly confirmed **Apply approved names**, a same-window **Undo this batch** and an in-progress **Cancel operation** button.

The new `DeskSpaceBatchRenameTransactionV275` is separate from the existing read-only preview. It re-scans the selected folder at the moment of Apply and compares every source+destination name with the preview; user edits, added/removed visible files, unsafe attributes or already-occupied destination names stop the whole operation **before any file is moved**. Files are limited to 128 direct visible regular files. A per-folder named OS mutex prevents concurrent DeskSpace operations. Every rename uses `File.Move(source,destination,overwrite:false)`; originals are never deleted, compressed or read as contents.

After an IO error or cancellation mid-operation, the engine attempts to roll back completed renames in reverse order, without overwriting any file. If something blocks rollback, the UI retains an explicit receipt for the remaining moves and offers Undo. Undo re-checks every renamed file's length, write time and destination/original paths; if a file was edited externally it refuses to overwrite or destroy that edit. Undo is intentionally **available only while the rename window remains open**, and closing a window with outstanding undo requires confirmation. There is no hidden background rename, silent deletion or automatic overwrite.

Windows CI runs a real disposable two-file Apply/Undo on a temporary folder, tests destination collision and modified file rejection, preview drift, cancellation, and locks the second file with an exclusive Windows file handle to force failure after the first rename and verify rollback. Source hash-parity and rebuild gates are maintained.

**Limitation:** a power loss/process crash during rename has no durable cross-restart undo journal yet; the workflow should remain unreleased until a recovery plan and installed-PC acceptance are complete. Real network-share and antivirus interactions are unverified.
