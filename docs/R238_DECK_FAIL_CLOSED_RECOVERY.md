# R238 — non-destructive per-monitor Deck state recovery (development)

R235 introduced an OS mutex for cross-process Deck offset updates, but existing code still discarded malformed JSON or an oversized map and then overwrote it with a new one-monitor dictionary. This could destroy the saved location of another monitor after interrupted, malformed or concurrent state writes.

R238 parses the entire previous map **before** changing it, rejects invalid/oversized/duplicate-case monitor identifiers, treats monitor device names case-insensitively, bounds saved JSON to 16 KiB and retains the previous file untouched on failure. New and changed positions continue to be written atomically under the existing R235 named mutex. Invalid reads use a safe ephemeral zero offset; **invalid writes never erase original data**.

Acceptance: strict static test; native Windows .NET reflection tests for two monitor identities, case-insensitive restore, null/garbled/duplicate monitor maps, invalid IDs and oversize. Mandatory full WPF build, UiSmoke and independent canonical-source rebuild. Real two-monitor and installed-device acceptance remain pending; release gate stays closed.
