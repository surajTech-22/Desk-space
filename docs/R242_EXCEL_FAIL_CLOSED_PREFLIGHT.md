# R242 — reject corrupt Excel template types before formatting

DeskSpace's existing pure destination eligibility gate approved a completely blank target before validating every saved source/target cell's storage-type consistency. An unsupported or corrupt captured type could therefore reach the write stage on an empty destination.

R242 adds a mandatory early scan for *every physical cell*. Types must be known (Blank, Text, Boolean, Number, Date stored numerically, Formula, Error), and occupied/blank flags must agree with the stored type. Invalid, unknown, missing, or contradictory source or destination metadata is rejected even for an otherwise blank target. Date and numeric Excel Value2 are compatible; formulas remain distinct from literal text. Existing strict partial-destination rejection is preserved. The work is purely an in-process validation change: no Excel COM invocation, source-write path, clipboard changes, VBA or macro execution is introduced.

Native Windows WPF tests exercise a fully empty destination, matching populated data and numeric dates, conflicting formulas, partially filled cells, unknown/null/blank-as-occupied, populated-as-blank, malformed destination and empty templates. This is a hardening increment only, **not** a claim that full Excel format/formula/table capture/readback or real Office acceptance is complete.

Release must remain blocked pending all consolidated gates and real Excel/physical-device acceptance.
