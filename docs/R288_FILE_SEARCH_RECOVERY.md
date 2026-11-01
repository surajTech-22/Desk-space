R288 — DeskSpace file search

User report: Search displays matching folders but not the files themselves.

The shared index used a stricter auto-recent-file filter rather than ordinary file-search eligibility; it processed folder matches in the same quota as real file names; and it lacked an immediate bounded search while the all-drive scan warmed up.

R288 adds an existing-file eligibility check (non-hidden, nonsystem, non-reparse, nontemporary), file-first ordering and separate file/directory quotas. A bounded, cancellable filename-only discovery scans Desktop, Documents, Downloads and OneDrive while the all-drive and Windows Search indexes warm. No file contents are read; both the universal search and donut Search bar reuse the existing shared backend.

Windows native tests use real XLSX and PDF filenames, a matching directory and hidden file, plus cancellation and scan-budget checks. Physical installed-PC search acceptance remains pending. Publication still depends on installer and signing/rollback gates.
