# R110 Office-installed Windows acceptance (release blocking)

**Status:** Development build source tests / WPF tests are not sufficient to approve this feature.

## Reference workbook
Use the owner's `Book1(4).xlsx` (on the Windows test computer), always on **disposable copies**. Source Sheet1 contains:
- `Table1`: a genuine Excel ListObject, `F7:J11`, `TableStyleMedium2`, five headers `a / b / c / d / Total super fantastic`.
- `E7:E11`: one merged area, outside the table.
- Fonts: single underline and double underline; notably `G11:H11` double underlined.
- Relative/formula cells including `J8:J10` and `F11:J11`; the totals are not an Excel ShowTotals=true row.
- Entire capture selection: `E7:J11`, five rows by six columns (30 physical cells, with underlying merged blanks).

## Tests to execute in real Microsoft Excel COM
1. Capture `E7:J11` from an OPEN copy of the original, save and reload DeskSpace mapping. Verify 30/30 positions and the actual `ListObject` + merge + two underline variants.
2. Select an entirely empty 5×6 rectangle on a NEW disposable workbook. Preview; verify it reports explicit table, merge, five structural headers, formula count, and no source business values. Apply only after confirmation.
3. Read back `ListObjects`, `ListColumns.Name`, `TableStyle`, ShowHeaders, ShowTotals=false, RowStripes, AutoFilter, `MergeArea`, Font.Underline, FormulaR1C1, row heights, native ColumnWidth, and individual cell number formats, direct fills, borders. Compare against the captured reference.
4. Repeat with a fully populated matching destination but NO Excel Table object; exact header strings must remain unchanged while DeskSpace creates a real Excel Table. Existing business numbers/formulas must have byte-for-byte matching semantic values after application.
5. Repeat with one missing input (partial target) and with one value moved into the merged non-anchor region. Both must be BLOCKED before any write: no new ListObjects/merges/formulas; no changed workbook styles.
6. Repeat with one header mismatch, mismatched array formula, protected worksheet, and a source Table name collision. All must be BLOCKED with a specific explanation and unchanged workbook.
7. Force a write failure in a test workbook (e.g. fail a specific table style property) and verify backup exists, rollback is attempted, failure is reported rather than success, and recovery from SaveCopyAs is possible.
8. Inspect the large WPF preview: cells retain independent formatting and actual table identity.
9. Install the candidate on a separate machine and confirm restart, preserved mappings, auto-update notification and channel selection.

**Critical:** CI hosted Windows compilation does NOT verify Microsoft Excel COM behavior unless Microsoft Excel is actually installed and these tests execute. Do not publish to Stable and Tester based solely on source-string checks, build success, WPF screenshots, or OOXML file inspection. Record a copy of the test log/screenshots and the exact DeskSpace candidate hash before release.
