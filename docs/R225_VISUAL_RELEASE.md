# DeskSpace R225 Search and Deck visual fix

The visible user screenshot of the compact Search and running-app Deck showed a too-narrow Search bar and overlapping popup behavior. R225 corrects this at source level and retains the existing shared held-file data.

**Prerequisite verified:** Windows Actions run 41034994246 succeeded on commit 8b133fb0cb4a533521db3741aef7ab6e028ca742. It compiled all R201–R225 changes, ran native WPF acceptance tests including Search/Deck layout, and independently rebuilt the SHA-256-verified source archive.

**Shipping procedure:** reproduce that exact source from its verified immutable artifact; run synthetic rollback, verified checksum, app and UI tests; create a new monotonically increasing installer version; write a 64-byte SHA256 receipt compatible with R191; publish the same verified binary to Stable and Tester; and notify both channels.

The physical screenshot appearance on the user's PC and remaining proposed features are **not independently verified**. R224 startup-stability correction is included. No user Excel workbook or files are accessed.
