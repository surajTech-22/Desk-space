# R267 — migrate compiled DeskSpace to a real checked-in source tree

Instead of depending forever on 100+ reconstruction stages, R267 adds the already-successful Windows CI #406 **complete, per-file SHA-256-verified WPF source** under `src/DeskSpace` in the development branch.

The one-time migration workflow is constrained to its explicit `r267/START_CANONICAL_MIGRATION.txt` push. It checks the exact successful run/commit, downloads the exact source+manifest+receipt artifact, verifies every member and checksum with R221, refuses an existing destination, compiles the independent `src/DeskSpace` project, then commits only source files to the development branch. It cannot merge, create a release or touch an installed user device.

After the migration, add an independent CI check that the checked-in canonical source matches the existing regenerated application and compile both independently. The old reconstruction remains temporarily as an independent compatibility oracle rather than being deleted before equality is verified. Subsequent updates should edit the canonical source directly. Only retire reconstruction after CI and installed device acceptance.

Physical Windows two-monitor acceptance, real Excel Office COM readback and installed-PC updater recovery are *not* established by this source migration. No Stable/Tester publication is authorized.
