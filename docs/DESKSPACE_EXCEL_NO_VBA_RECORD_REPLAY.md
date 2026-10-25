# DeskSpace Excel Capture & Replay — macro-like, **not** an Excel macro
Status: researched architecture; **not an approved Windows release**.
Reference: Microsoft Excel Macro Recorder, Excel Range.Copy/PasteSpecial, FormulaR1C1, ListObjects, Font.Underline and Range.Validation documentation.

## Goal
A user selects a range or performs a repeatable Excel workflow. DeskSpace **inspects** the live Excel workbook through Windows Excel COM, saves an independent declarative recipe in DeskSpace's encrypted local mapping store, previews a faithful sheet, and can later **reapply supported instructions** to a user-selected workbook/range. No VBA, VBProject, .xlsm requirement, injected script, automatic event macro, or arbitrary executable code.

Excel's ordinary macro recorder saves a sequence of actions as VBA code; it can record clicks/formatting/pasting, but fixed-address recordings can fail when range size changes. DeskSpace instead stores a typed, address-based sequence of named operations and per-cell state. This requires precise feature-specific capture, NOT just copying one global Range.Font or one global Range.NumberFormat.

## Two distinct ways to capture
1. **Snapshot of current selection**: enumerate individual physical cells (including blank cells), their address and merge membership, styles, formulas, input shape and table membership. Capture range-level geometry separately. Reapply state to a corresponding target range. A snapshot **cannot prove which actions the user performed in the past**.
2. **Record changes** (later): user explicitly presses Start recording, DeskSpace stores before/after states of monitored selection(s) and infers typed operations. Note that Excel SheetChange events do not cover every formatting change; a reliable recorder must compare snapshots when the user explicitly checkpoints/stops recording. Order/intent of actions cannot always be reconstructed and must be reviewable.
Never claim that reading a formatted selection is equivalent to recording all mouse clicks.

## Per-cell snapshot schema
Key every source cell by `SourceSheet + absolute Row/Column` plus selection-relative offsets.
- Cell value storage kind (blank, number, text, boolean, Excel error, formula) separate from expected input semantics (number, currency, percent, short/long date, time, text, identifiers). Differentiate numeric-looking text with leading zeroes.
- Non-identifying display placeholder or input-type label; **do not copy existing private cell content by default**.
- Formula1, FormulaR1C1 and Formula2/dynamic/array status. Relative/absolute reference behavior must be preserved, **not** precomputed or replaced by current results.
- Font: family, size, weight, italic, underline **style** (single, double, accounting), strike-through, superscript, subscript, font colour and theme/tint. Include rich text runs where formatting differs within a cell.
- Number format, fill/gradient/theme, patterns, each border separately (style, weight, colour; diagonal borders), alignment, text rotation, wrap, shrink-to-fit, indent, merged areas.
- Cell protection, hyperlinks, comments/notes, data validation (including rule type and formulas), conditional formatting (actual rules and priorities, not merely the current displayed colour), named styles and related workbook resources.
- Row heights, column widths, hidden rows/columns and structural relationships stored independently of cells.
- Where Excel refuses a property, save `Unsupported/Unavailable`, **never** silently substitute defaults and call capture complete.

## Distinguish actual Excel tables from visual grids
Excel structured tables are **ListObjects**, with name, range, column names, header and totals rows, style, stripes, structured references, filter state and calculated columns. A bordered rectangle is not automatically a ListObject. Capture each supported table's object metadata and flag partial selections. During replay, create/modify actual ListObject tables only with explicit approval and destination validation; otherwise apply only safe cell formats, without inventing a table.

## Declarative instruction model, no VBA
```text
Recipe {
  SchemaVersion; SourceSelection; Name; Mode=Snapshot|RecordedActions;
  WorksheetDependencies; ExpectedWorkbookDateSystem;
  Cells[]; Tables[]; NamedStyles[]; Geometry[];
  Operations[]; UnsupportedFeatures[]; SavedUtc
}
Operation {
  Type=SetCellFormat|SetNumberFormat|SetFormula|SetValidation|
       MergeCells|SetDimensions|CreateOrUpdateTable|...
  RelativeTarget; BeforeFingerprint; Payload; RiskLevel
}
```
Operations are whitelisted data structures, **not script strings**, file commands, VBA or arbitrary PowerShell. Unimplemented features remain inspect-only and cannot be silently applied. Preserve schema migrations and old mapping IDs.

## Replay engine
1. Select exact workbook/sheet/range and read live before-state. Confirm address or label alignment, workbook date system, protected sheets, required tables, source dependencies and size.
2. Preview differences **per cell** with colour-coded before/after, formulas, tables, conflicts and unsupported features. No permission to overwrite by mere hover.
3. Decide mode explicitly: Format only; Formulas only; Formatting + formulas; full supported structure. Keep incoming business data separate from recorded source examples.
4. Create Excel workbook copy/backup and an in-memory rollback plan. Apply geometry/merge/table changes in a dependency-safe order, then per-cell formatting, validation and formulas; recheck the final state from Excel COM. If any step fails, report exactly which properties failed and restore with an explicit rollback path.
5. Save recipe version and verified applied result only after readback. Never label partial application `Complete`.
6. Respect security controls: don't read VBProject, don't enable or run Office macros, don't execute external connections or workbook automation without explicit approval.

## UI
Large zoomable real sheet preview, cells shown in their own addresses and proportions. Clicking a cell opens an inspector:
- Source address, stored kind, expected/input shape, complete style property list.
- Formula source plus translated destination example without exposing old values.
- Table membership (table name / header / data / totals).
- State `Captured`, `Inspect only`, `Unsupported`, or `Mismatch`.
At top: counters such as `30/30 physically captured cells`, `25/30 fully supported for replay`, `2 tables`, `3 unsupported features`. These are separate; count alone must never imply fidelity.

## Verification matrix
- Individual cells with mutually different bold/italic/underline styles and mixed-character runs
- Formatted blank cells, all border edges/diagonals, merged regions crossing selection
- General, number, currency, percentage, scientific, identifier/text, short/long date/time
- Relative/absolute formulas and dynamic arrays
- True ListObject with headers, totals, custom table style, filter and calculated columns; visually bordered non-table
- Data validation lists/formulas and conditional formatting rules
- Save, reopen, append capture without overwriting unrelated source cells
- Replay against disposable workbook, read back each property, compare against original and restore on failure
- Windows self-contained build, WPF screenshots, stable + tester release publishing only **after** acceptance
Test on actual workbook and not merely synthetic screenshots. No release if unsupported data is silently dropped.

## Sources (public Microsoft references)
- https://support.microsoft.com/en-us/excel/automate-tasks-with-the-macro-recorder
- https://learn.microsoft.com/en-us/office/vba/api/excel.xlpastetype
- https://learn.microsoft.com/en-us/office/vba/api/excel.range.formular1c1
- https://learn.microsoft.com/en-us/office/vba/api/excel.listobjects
- https://learn.microsoft.com/en-us/office/vba/api/excel.font.underline
- https://learn.microsoft.com/en-us/office/vba/api/excel.validation
- https://learn.microsoft.com/en-us/office/vba/api/excel.font(object)
