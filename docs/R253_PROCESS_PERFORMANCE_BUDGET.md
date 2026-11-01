# R253 — performance budget status without background polling

DeskSpace already presents on-demand lifetime CPU and managed-memory counters. R253 adds a **Runtime advisory budget** row to the existing Component Status screen, using Windows process private bytes, kernel handles and thread count, with advisory thresholds of 1 GiB, 5,000 handles, and 256 threads. It presents observed counts and warnings if one of these bounds is exceeded.

The check runs only when Component Status is explicitly opened/refreshed; it creates no additional timer, watcher or network traffic and does not terminate or throttle another app. Numbers are advisory and not a proof of leak-freedom, low CPU use or guaranteed performance. Native Windows tests exercise healthy, threshold-crossing and invalid metrics. Full sustained performance-budget profiling and real installed-PC acceptance remain pending.
