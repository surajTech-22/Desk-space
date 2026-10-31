# R268 — live Microsoft Excel table-calculated column acceptance

The previous live Excel acceptance program still expected **zero** restored formulas on a fully blank destination. That is now outdated after the user-requested R266 combined Formats & Formulas paste.

R268 changes the Office acceptance to verify eight real `FormulaR1C1` expressions at their destination addresses (J17:J19 and F20:J20), while all non-formula input cells remain empty. It continues to check real ListObject styles, merges, underline, an incompatible partial destination, a fully populated matching destination that retains its original business values, table name collision in the source workbook and a corrupt/unsupported table style.

A genuine Excel Table may automatically fill a captured formula into other table rows after a preceding formula is written. The live writer now accepts an **exactly matching** already-populated relative formula (no overwrite), but rejects unexpected auto-filled formulas, mismatched R1C1 expressions and literal content. It still performs the verified pre-change SaveCopyAs receipt and rollback.

This is a source and test correction, **not proof that real Office COM acceptance has passed**. Native Windows WPF tests plus source-parity build are required first. Actual Excel application testing must be run on a Windows device with Excel COM installed and no personal Excel instance running; only disposable workbooks must be used. Stable/Tester remains blocked.
