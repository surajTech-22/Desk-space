# DeskSpace Excel formatting capture and human-like application

**Corrected requirement:** This is NOT a macro recorder, a replay system, or VBA. Macro behavior was cited for reference only. DeskSpace inspects the user's CURRENTLY SELECTED Excel cells, saves a complete address-specific formatting and structure template, and applies the selected properties to the CURRENT destination selection through Excel's object model as a human would.

## Capture
- Each physical cell has its own independent formatting; selected blank cells are important.
- Gather Excel's actual storage/expected type and semantic shape (text, words, digits-as-text, date/time serial, percentage, formula, errors).
- Gather all supported fonts including exact underline kind, rich text runs, formats, fills, theme/tint, borders, merges, dimensions, validations, conditional-formatting rules, workbook-supported styles, formulas and references.
- Detect real Excel ListObject tables separately from bordered ranges: table names, header/total rows, style, filters, calculated columns, area and dependencies.
- Save a *declarative* cell-and-structure snapshot. Do not save a sequence of clicks, shortcuts, VBA or recorded actions.
- An unsupported/unreadable property is recorded as `Not captured`. Never silently claim `100% complete`.

## Destination preflight — absolute no-partial-input rule
Run this check across the **ENTIRE** corresponding destination rectangle, including the cell addresses that will receive formatting, not just a subset of mapped value cells:

1. **Completely empty destination**: every cell blank with no formulas. Eligible for formatting when the structure itself can be safely applied.
2. **Completely populated according to the captured template's data layout**: every cell that contained an input/formula in the captured source has a corresponding input/formula at the destination, of compatible stored kind, and each recorded structural blank remains blank. Merged-cell non-anchor cells are structural blanks. Different business values are okay; do not demand literal source values.
3. **Anything else**: BLOCK. Even one missing input, an unexpected value in a recorded blank, a formula/value mismatch, incompatible cell kind, changed selection, wrong range dimensions, protected sheet or unrecognized table structure must block the ENTIRE application. Never apply to only the ready half, even if some cells are individually compatible.

Example: A 5x5 source block contains 23 populated cells and 2 structural blanks. A target with all 25 blank is eligible. A target with 23 compatible inputs in exactly those source-populated positions and 2 matching structural blanks is eligible. A target with 22 inputs, a value in a structural blank, or 12 inputs and 13 blanks is REJECTED (not partially formatted).

Before any write, read ALL target cells and show one of `Empty / Full matching layout / Partial — blocked / Mismatch — blocked`. Recheck immediately before applying. Do not use per-cell eligible subsets to bypass the range gate.

## Apply
- User chooses saved format and current Excel target. Respect their selected start cell or exactly sized selected destination range.
- Preserve existing business values. Formatting operations are applied through live Excel COM individually per cell; formula changes require explicit selection and separate approval.
- Genuine Excel tables must be managed as Excel ListObjects, **not** faked by painting their header colours.
- Save a workbook backup. If the feature set cannot be faithfully applied, block, do not silently downgrade to a partial format.
- Read back each changed property and make a successful final report only on complete verification. Keep a backup/rollback route for failure.
- Do not record or replay user activity, send keyboard macros or execute VBProject scripts.

## Readiness
The pure destination eligibility gate is staged in `r107/DeskSpaceExcelFullRangeGateV107.cs`. The all-features Excel apply engine and readback are not yet approved. **Do not publish an update claiming this behavior while only the gate exists.**
