# DeskSpace R42 — Final candidate acceptance checklist

**Status:** Source integrated into R42; native Windows compiler/installer, WPF visuals and real Excel/Windows location tests are still required. **Do not distribute as a confirmed stable release before they pass.**

## Fixed wheel geometry

There must be **13 main sectors total**, **six inner** and **seven outer**.

- **Inner (unchanged):** Search · Calendar · Calculator · Clock · Weather · Settings.
- **Outer:** Information · Notes · Reminders · Clipboard · Files · Tasks · **Excel Functions**.

Excel Functions is the **last outer sector**. There is **no main Excel Clipboard sector**, no Excel Functions inner sector, and none of the original twelve modules are removed.

### Excel Functions child wheel

Excel Functions opens a curved child donut containing exactly **Clipboard** and **Information**. Clipboard uses the existing Excel-only clipboard. Information uses the existing saved-mapping system and shows:

- an **ungrouped** saved mapping directly under Information;
- a **grouped** mapping under its named group, for example **Clients → Audit → [mapping]**;
- a **Back** action for nested navigation, plus existing manage/edit access.

Grouping classifies existing mapping IDs; it is **not another Excel Functions child**. Hover only previews. Actual Excel paste requires an explicit saved-mapping click or committed drag, verified Excel cell addresses, exact label match, formula protection and a workbook backup.

### Layout and notification

The Excel Mapping Workspace must keep the editable header/value mapping table usable in shorter windows and reduce excess instructional content. A verified **successful Excel paste** shows a success notice for **three seconds**, then closes. Errors, blocked paste and other modules' notices are not auto-dismissed.

## Weather

- A **Use my location** control requests Windows geolocation permission only from the visible Weather Settings screen after user action.
- Manual **worldwide city/region search** supplements the original city presets.
- If Windows location is denied, disabled or unavailable, manual search remains accessible, and last good readings are marked offline/cached.
- Never quietly substitute a different city when the chosen city or current location cannot be resolved.
- Device coordinates are rounded for a general-weather query and are not saved to local DeskSpace preferences.

## Test and release gate

- Run original R2–R41 source tests, R42 Excel regression tests, R42 Weather regression tests and all Windows PowerShell syntax preflights.
- Compile the self-contained Windows app and Inno installer locally; run WPF smoke/visual capture on the 13-wedge wheel and mapping editor. Compare with the current tester screenshots.
- Live-test **Use my location** both granted and denied, worldwide search, and cached offline weather.
- Test workbook matching, grouping navigation, and 3-second success-only message with disposable files.
- Gracefully close running DeskSpace, back up user data, install, and confirm the installed version.
- Only after successful checks upload `DeskSpace_Setup_R42_Local_*.exe` and SHA-256 into **private GitHub test Releases**. Stable v1.4.266 remains separate; **GitHub Actions minutes must not be consumed**.

See [Windows tester guide](../docs/TESTING_GUIDE.md) for a reproducible checklist and bug-report template.
