# R249 — prepublication Stable/Tester one-hash guard

A consolidated DeskSpace update must use the same verified installer bytes for Stable and Tester, not separately built executables with differing state. R249 provides a **read-only prepublication check** comparing SHA-256 of each candidate against one strict official 64-character SHA receipt, rejecting empty/oversized/missing/linked files and divergent channels. It does not itself sign, install, publish or alter channels.

Windows CI uses disposable synthetic candidate bytes to prove that identical files pass and mismatched bytes, incorrect published checksum, malformed receipt and missing channel fail closed. The existing R204 consolidated release gate still blocks any publication until all mandatory Windows/physical-acceptance checks pass. This is partial UPD-04 development; production release workflows still need an approved same-artifact upload and verified signature/provenance checks.
