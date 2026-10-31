# R251 — Excel source formula integrity before direct format/formula paste

Existing DeskSpace format/formula mode can reconstruct recorded R1C1 formulas on a wholly blank destination, preserving the user's values on populated destinations. A legacy check only rejected missing R1C1 source expressions when *at least one* source formula had already been recognized, allowing a damaged template in some all-missing cases.

R251 adds a pure source metadata validator that checks **every physical captured cell** at the preview stage, including empty destinations. Every stored formula must have a literal, nonempty, equals-prefixed R1C1 expression of at most Excel's 8,192-character formula limit and contain no forbidden control characters. A non-formula cell carrying formula metadata is invalid. The same rule is rechecked before writing a blank destination formula. Missing/unknown/contradictory captures fail closed and ask for recapture; the user's existing workbook is not modified.

This is source integrity hardening, not proof of real Excel formula/table readback. Native Windows tests exercise normal relative R1C1 references, missing/misclassified formulas, unprefixed text, formula size boundaries and control characters. Full Office acceptance remains pending; release remains blocked.
