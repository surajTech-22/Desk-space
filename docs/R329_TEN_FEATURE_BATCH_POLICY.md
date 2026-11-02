# DeskSpace — Ten-feature release policy (effective after v1.5.1120)

## User instruction

Do **not** publish routine DeskSpace updates after completing only two or three features. Accumulate **at least ten independently useful, user-visible features** and publish them **together as one verified Stable + Tester release**.

The ten items for the next batch are defined in [R329-feature-batch.json](../release/batches/R329-feature-batch.json). Each item needs a visible action or UI, an independent acceptance outcome, integration into the **shipping** Windows source, and real Windows regression evidence. A refactor, bug fix, repeated subtask, test, commit or documentation page **does not count as a feature**. Improvements already present in v1.5.1120 cannot be recounted.

## Rules for each batch

1. Keep incomplete work in a feature branch; do **not** publish Stable/Tester or notify installed users as soon as one feature passes.
2. Review code and native Windows tests feature by feature. Mark status `planned` → `implemented` → `verified` only when evidence exists. Update the batch manifest for each transition.
3. All ten must pass together on the exact shipping-source reconstruction, with no regressions in Search, Excel, Deck, monitor placement, update supervisor, saved data or rollback.
4. The feature batch must pass `pwsh ./scripts/assert-ten-feature-release.ps1 -Mode Release` **before** installer packaging or GitHub release creation. `Validate` is for planning only; it never authorizes a release.
5. Real dual-monitor and Excel COM acceptance are mandatory for the R329 features that touch these systems. CI simulation alone is not physical acceptance.
6. Publish one checksum-verified installer to both Tester and Stable. Never claim that a release is installed on the user's PC until verified.
7. Urgent data-loss or security fixes may be isolated as **emergency hotfixes**, with a documented incident, dedicated Windows regression and explicit release justification. Ordinary cosmetic or feature changes are not emergencies.

## Ten planned capabilities

| ID | Module | Acceptance deliverable |
|---|---|---|
| R329-01 | Search | Persistent low-memory filename index with restart correctness |
| R329-02 | Search | Save/rename/delete search presets |
| R329-03 | Search | Open, Copy Path, and Open Containing Folder |
| R329-04 | Files | User-triggered SHA-256 verification |
| R329-05 | Files | Read-only duplicate file inspector |
| R329-06 | Files | Previewed, cancellable image conversion |
| R329-07 | Clipboard | Explicitly saved private snippets, no passive capture |
| R329-08 | Capture | User-selected local OCR with consent |
| R329-09 | Excel | Verified mapping/paste preview and backup/readback gate |
| R329-10 | Workspace | Preview and approve monitor-aware session restoration |

Existing functionality should be reused rather than duplicated. In particular, do not replace the already-shipped R324 all-drive search, R326 advanced filters, the current Excel mapping framework, or existing Deck/workspace frameworks.

## Current status

- Baseline: `v1.5.1120`.
- R329 batch items: 10 scoped; **zero independently verified as R329 deliverables** at initial planning time.
- The full feature release is **blocked** until implementation, Windows tests and relevant real-device acceptance finish. No version number or publication date is promised for this batch.
