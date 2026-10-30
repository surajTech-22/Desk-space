# DeskSpace interim release authorization — October 30, 2026

The user explicitly requested publication of the current implemented changes while continuing the outstanding roadmap.

This is an **interim release**, not completion of the 30-item consolidated roadmap.
The final consolidated release gate (`r204/require-consolidated-ready.ps1`) stays intact, and its pending checkpoints are not marked verified.

**Verified prerequisite:** R201–R222 Windows acceptance at commit `19948aa574072a49326506712723766d1aadbed1`, run `41028168373`, successful. Source archive `DeskSpace-R221-Consolidated-Verified-Source` contains a SHA-256 verified copy of exactly that WPF application.

**Interim publication safeguards:** independently restore and hash each source; run native UI and updater acceptance on shipping source; stamp monotonic installer/updater versions; produce real self-contained Windows installer; enforce R220 exact 64-byte SHA-256 receipt for old R191 clients; verify matched Update.exe and Setup.exe digests; pin current branch head; release identical installer binaries to Stable and Tester; validate published GitHub asset digests.

**Known unfinished work:** real mixed-DPI physical monitor testing, live Excel COM and three workbook acceptance, unsigned installer trust and app-signing, and numerous feature proposals. Do not claim they passed. Installation on the user's PC cannot be verified remotely. The existing supervised R185 rollback is preserved.

New releases must not download or modify the user's workbook or local DeskSpace data during CI.
