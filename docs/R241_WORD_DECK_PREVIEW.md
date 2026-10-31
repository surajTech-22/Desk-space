# R241 — Safe Word DOCX text excerpts in DeskSpace Deck (development)

R241 adds an explicit, on-open, **read-only** Word DOCX preview inside the existing Deck. It reads only the Word main document XML file and presents at most the first 3,000 characters of wordprocessing text, paragraph breaks, and tabs. It does not attempt to reproduce Word formatting, does not run Office or shells, does not execute macros, and never follows XML relationships, remote links, attachments or other package content. Macro-enabled DOCM and DOTM are unsupported.

Constraints: source DOCX must be a normal non-reparse file no larger than 16 MiB; its document XML entry is capped at 2 MiB; a streaming byte bound independently protects against deceptive ZIP metadata. Only exactly one canonical main document XML entry is accepted. VBA payloads are refused. XML parsing forbids DTDs and external entity resolution. No network, filesystem writes, clipboard operations, background watcher or saved plaintext.

The view loads on a worker thread only after a user opens the preview and clears text on unload. Native Windows tests create disposable DOCX packages and verify two paragraphs, unchanged source SHA-256, DTD and macro rejection, compressed-but-oversized XML, corrupt archive and missing file handling. Full WPF compilation, native smoke and independently rebuilt source remain required.

This is not marked released. Real user-device acceptance and all consolidated release checks must pass before Stable and Tester publication.
