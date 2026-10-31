# R252 — Excel safety copy SHA-256 receipt before mutation

The existing DeskSpace Excel direct format/formula apply pipeline calls Excel COM SaveCopyAs and checks that a nonzero backup exists before changing tables, merges, styles or empty-cell formulas. R252 strengthens that pre-write gate: it streams SHA-256 of the **actual pre-change backup**, creates a same-folder sidecar receipt without replacing an existing receipt, and verifies both bytes and the exact sidecar filename before proceeding. If receipt generation or verification fails, no Excel destination changes begin. Backup data remains under the existing DeskSpace ExcelBackups directory; no real workbook source is modified by the receipt code.

Pure native WPF tests use disposable synthetic workbook bytes to prove genuine SHA-256 verification, invalid receipt path rejection, backup-content tamper detection, receipt tamper detection, and collision safety. No Excel COM invocation occurs in those tests. Full Office workbook readback and failed-install rollback require separate physical/Office acceptance and remain marked pending.

The new receipt is a data-integrity check, not a signed cryptographic attestation or guarantee that the destination was fully rolled back. Publication remains blocked by the consolidated release gate.
