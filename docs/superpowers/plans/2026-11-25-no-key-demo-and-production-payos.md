# Prepare the no-key demo and production payOS

- [x] Launch the Release build on loopback port 5190 with per-process credentials empty and development-only payOS mock enabled. Keep the normal credential store and production isolated.
- [x] Verify the local dashboard, speech catalog (95 videos), writing capability list (21 templates), and readiness. Verify missing service configuration from Voice (`available=false`), Web Push (`configured=false`), and vocabulary image lookup (0 results). A synthetic local learner reached the payOS mock checkout with HTTP 200 and the no-money notice.
- [x] Identify the active TCT English merchant channel in the authenticated payOS dashboard. Its current webhook points at the development tunnel.
- [x] Match the live credentials to the active TCT English channel and save them in Plesk. payOS confirmed the production webhook with HTTP 200 and code `00`. Verified the webhook address persisted in the payOS portal. Enabled payOS on production with mock mode off. IIS acknowledged the recycle, readiness returned HTTP 200, and the release marker still showed release 123. No live order or transfer was made.
