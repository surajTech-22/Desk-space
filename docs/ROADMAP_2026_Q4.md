# DeskSpace modernization roadmap — 28 October 2026

Status: tracked plan, **not a claim that all features are implemented**.
Baseline: verified stable v1.5.906 (R160). Release work is performed in small tested PRs.
Every milestone must preserve existing workspaces, captured Excel data, app pins,
held-file semantics, privacy and independent local widget positions.

## Required acceptance gates for EVERY milestone
1. Full Windows x64 build and actual WPF smoke; SHA-256 checks.
2. No crash or unhandled exception on startup, shutdown, sleep/resume or update.
3. Primary plus secondary display: different DPI, negative screen coordinates,
   hover/right-click on only one widget; both read the same shared Deck files.
4. Protect real source files, saved notes, mappings, formulas, reminders and pins.
5. Regression screenshots for affected donut arcs, Deck and application menus.
6. If a gate fails, keep the change on a feature branch; do not publish stable.

## Phase 1 — Reliability and architecture
- [ ] Convert the reconstruction chain into one authoritative buildable source tree;
      preserve exact produced binary equivalence before deleting legacy scripts.
- [ ] Consolidate monitor geometry, popup fencing, DPI and mouse ownership into one
      well-tested widget infrastructure service.
- [ ] Real physical dual-monitor UI automation including disconnect/reconnect,
      negative origin and mixed DPI, plus visual pixel comparisons.
- [ ] Update supervisor rollback: capture an *entire verified prior installation*,
      check post-install heartbeat and restore atomically on failure, without
      replacing user data or merely restoring one incompatible DLL/exe.
- [x] R161: Opt-in privacy-safe diagnostics ZIP in Downloads, crash-type capture.
- [ ] Startup recovery, exception containment and watchdog: avoid foreground
      interruptions, preserve tray, add crash-loop safe mode without resetting data.
- [ ] Monitor CPU, GPU, RAM and memory leaks; replace unnecessary polling with
      event subscriptions where supported.
- [ ] CI build provenance; reproducible source/build manifest and unsigned/hardened
      artifact policy.

## Phase 2 — Existing module improvements
- [x] R160: Two in-circle folder ZIP / Copy targets during drag, commit on drop.
- [x] R161: Preview ZIP entry names read-only in Deck (bounded, never extract).
- [x] R161: Prevent duplicate staging by canonical source path across both widgets.
- [ ] Add cancellable ZIP/copy progress and size estimates; clean interrupted
      .partial artifacts on restart.
- [ ] Support content fingerprints and optional replace/keep-both choices;
      first version deduplicates only identical source paths.
- [ ] Smart Drop for images, PDFs, text and URL objects using optional workflows.
      Keep ordinary file drop immediate unless the user explicitly selects an action.
- [ ] Advanced clipboard conversions, opt-in OCR and file-to-note actions.
- [ ] Better type-aware PDF, Office, code and audio previews (sandboxed/size-limited).
- [ ] Search index health, progressive results, context actions and optional
      Everything IPC provider when installed.
- [ ] Applications: richer window previews and recent-document quick-open.
- [ ] Excel: verified active-sheet signature suggestions, type normalization,
      preview, formula/format readback and reversal support, never unverified paste.
- [ ] Information: contextual suggestions (no plaintext secret auto-fill).
- [ ] Reminders: actionable notifications, snooze/repeat and repeat-run history.
- [ ] Media: reliable play/pause, mute and close integration in shared Deck.
- [ ] Settings: consistent navigation/validation and immediate non-destructive
      preview of widget choices.

## Phase 3 — New capabilities
- [ ] Reusable monitor-specific window layout profiles, with mixed-DPI restore.
- [ ] Action command palette over current search, with clear confirmation for
      destructive commands and launch targets.
- [ ] No-code workflow builder with permissions, safe limits, logging and undo.
- [ ] Information Vault separated from regular information with Windows
      Credential Manager/Hello where supported; never save raw passwords.
- [ ] Accessible keyboard-only donut, focus states, high contrast, reduced motion,
      scaling, keyboard shortcuts and screen reader labels.
- [ ] Export/import validated profiles and layouts, with encrypted export options
      where sensitive data may be included.
- [ ] One-click component status dashboard for clipboard, index, Excel COM, tray,
      updater, Deck and monitors.

## Release rules
- No monolithic unverified 'everything implemented' release.
- One bounded feature set per PR, with additional physical owner acceptance for
  UI behavior and auto-update validation on a second PC.
- Avoid destabilizing the actively used stable channel. Testers opt in.
- All diagnostics/test output goes into Downloads; user documents remain unchanged.
