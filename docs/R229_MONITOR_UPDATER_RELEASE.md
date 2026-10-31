# DeskSpace R228–R229 dual-monitor/update recovery release

The user's dual-display issue reports magnets between the two physical widgets, copied Deck locations, Deck escaping its source monitor, and update notices not completing the installation.

**R228:** Existing monitor sessions no longer periodically reposition unchanged widgets. Each Deck has its own monitor-keyed drag placement loaded after its native screen/session is attached. Placement is clamped in physical pixels, with one shared staged-file store but independent UI geometry.

**R229:** The verified update queue keeps its installer when the independent supervisor is temporarily busy. Stable and Tester maintain the R191 cross-process stage lock, official GitHub metadata and SHA256 validation, safe deferral when Workspace is still open, and an automatic retry without discarding verified bytes. Users can close Workspace windows normally; no force-closing or data deletion is introduced.

**Release precondition:** GitHub Windows run 41049100123 succeeded on exactly 2814520a37496358931fd57e4f30dd6c26bde14f. The release workflow restores the SHA256-verified canonical source artifact from that run and executes native regression and full installer build. It publishes the same release asset to Stable and Tester only after validation.

**Limitations:** Mixed-DPI physical dual-monitor behavior and automatic installation on the user's actual Windows PC cannot be confirmed remotely. The full feature roadmap remains gated. This is an interim reliability update, not a declaration that all features are complete. Do not use any of the user's Excel files.
