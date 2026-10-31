# R248 — repeatable build from one hash-verified canonical source bundle

DeskSpace development currently applies a long sequence of verified patch scripts to the v1.5.1093 canonical WPF source on Windows CI and packages the fully composed source in R221. R248 provides an explicit **independent build entry point** that takes only the packaged source ZIP, its per-file manifest, and its SHA-256 receipt. It requires R221 to verify every archive member and receipt before extraction, builds the unchanged extracted WPF project with .NET 8, and removes the isolated temporary checkout. No patch scripts are executed during the independent rebuild; no installer or user data is accessed.

GitHub Actions uses this exact command instead of hand-written ad-hoc extraction, and continues publishing the verified source **as a CI artifact only**, not an installer. This moves UPD-01 forward, but the repo still requires a future separate canonical source migration from the patch overlay into the normal editable application source tree. Marking source migration fully verified before that migration would be incorrect.

User data, original DeskSpace installation, release channels and existing version numbers remain unchanged.
