# R235 — cross-process Deck position persistence (development)

DeskSpace displays independent Deck popups per monitor, but earlier saves used a C# `lock` shared only inside the saving process. When the two widgets are hosted in separate DeskSpace processes and both Deck positions change, simultaneous read-modify-write of one JSON map could overwrite the other monitor's offset.

R235 adds an installation-data-root-scoped Windows named mutex to protect each read-modify-write save, with a bounded two-second acquire, safe recovery from an abandoned mutex, a strict failure rather than silently dropping an update, and guaranteed release in a finally block. Existing atomic temporary-file replacement, independent monitor keys and shared held files are preserved.

Pending: Windows compile/CI, then physical concurrent mixed-DPI monitor test. This is NOT an authorization to publish a consolidated release with the remaining incomplete gates.
