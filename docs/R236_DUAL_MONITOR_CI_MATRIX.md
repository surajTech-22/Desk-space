# R236: automatic GitHub dual-monitor regression (simulation)

This increment adds an always-on Windows WPF test matrix to the consolidated development branch, independent of the host VM's display topology. It runs on each pushed commit and reports failures back to GitHub Actions without publishing any installer.

Scenarios: 12 screen/widget setups covering left and right adjacent displays, negative desktop origins, vertically stacked monitors, 1024-width compact displays, display offsets, 100%/125%/150%/200% synthetic scaling, edge proximity without magnetic snapping, popup containment, independent re-entrant popup calculations, bounded monitor-specific Deck offsets and disconnect/reconnect identity restoration. Existing R214/R228/R231 native WPF acceptance remains active. There is a separate read-only Windows Forms enumeration of the *actual runner* screens, stored in a JSON CI artifact to prevent the simulation from being misrepresented as physical dual-monitor verification.

GitHub-hosted Windows runners are ephemeral VMs. A synthetic two-monitor topology test can verify position logic, but **cannot prove how two connected real monitors, their DPI and cursor/focus/drag behavior work**. The real mixed-DPI dual-monitor readiness checkpoint stays pending. A dedicated multi-display Windows self-hosted runner or actual approved physical device is required for an end-to-end acceptance test. Do not install unsigned kernel display drivers or change real display settings on a shared runner.

Release remains blocked by the existing consolidated readiness manifest. No new Stable or Tester release is triggered by R236.
