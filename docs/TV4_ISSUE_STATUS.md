# TV4 acceptance status against handover issues

This file is a truthful status record for the integration patch; it is not a declaration that all issues are closed.

| Issue / task | Implemented in this patch | Still needed before acceptance |
|---|---|---|
| T2-NV4 / #20 — Hub and data contract | Auth `[Authorize]`, DTO fields, real Auth FKs for message sender/notification recipient, unique constraints, Match bridge contract, unit tests for membership/match/block/unmatch rules | Run the real Auth negative tests after DB migration; TV3 Match adapter and TV5 GUID block adapter must be merged and reviewed |
| T3-NV4 / #25 — send, receive, history | Commit before publish, idempotent `clientMessageId`, conflict 409, cursor/afterId history, server-side permission check for every read/join/send | Real Match/Block integrations; prove unmatch/block/account lock while Hub is already open using disposable test accounts |
| T4-NV4 / #30 — idempotency, notification, report | Notification dedupe key, persistence, recipient-only mark-read, cursor + afterId catch-up, MatchChatBridge publish after commit, UI submits exact messageId and `IChatReportTargetAccess` validates that target IDs are in-scope without returning body | SafetyAdmin must integrate that contract into the report endpoint; verify notification reload and report ownership after TV5 PR is integrated |
| T5-NV4 / #35 — reconnect and load | AuthProvider route, current-token provider for Hub reconnect, retry after initial connection failures, catch-up for messages and notifications, 20-connection measured load script | Run actual load test on target local environment; attach JSON with p50/p95/max/errors; prove offline/online and backend restart manually |

## Test evidence to attach

- `dotnet test DatingApp.slnx`: passed/failed/skipped from the current branch.
- `npm run build`: actual result after npm regenerated lockfile.
- `npm run test:tv4`: actual A/B/C output from test database with the real modules available.
- `npm run test:tv4-load`: JSON report generated on the team machine.
- Screenshots/video: two real signed-in browser sessions, C denied, restart/reconnect recovery, block/unmatch/lock effects.
- PR reviewers: TV1 reviews Auth/Hub/filter and migration; TV3 reviews Match transaction/bridge; TV5 reviews Block/report policy. Do not close issues merely because interfaces or scripts exist.
