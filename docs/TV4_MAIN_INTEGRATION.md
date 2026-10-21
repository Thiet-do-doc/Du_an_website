# TV4 Chat & Notifications — integration on the shared main foundation

This patch follows `Ban_giao_du_an_web_hen_ho_5_thanh_vien_2026-10-20.docx`, section 09 and section 17. It is intended for a new branch `feature/tv4-chat-v2` based on current `origin/main`. It is **not** the old standalone demo ZIP.

## What the patch implements

- Chat domain source uses `ApplicationUser` / `Guid`, not `DemoUser`, `DemoMatch` or `DemoBlock`.
- `Conversation` records real participant IDs; `ChatMessage.SenderId` and `AppNotification.RecipientId` are real Auth user FKs. Unique `(MatchId)` and `(SenderId, ClientMessageId)` indexes prevent duplicate conversations/messages. Notifications have a per-recipient `DedupeKey` unique index.
- Authenticated REST controllers use `ICurrentUser` and `Policy = User`. SignalR `[Authorize(Policy = "User")]` uses the existing main JWT pipeline and preserves `AccountHubFilter`; the patch only maps `/hubs/chat`, it does not create another authentication scheme.
- `MatchChatBridge` implements TV3's `IMatchConversationBridge` contract. `EnsureConversationAsync` uses the caller's scoped `AppDbContext` and is intended to run inside MatchService's transaction; publish methods run after commit. Unmatch raises `MatchEnded` so the UI refreshes `canSend`.
- Messages are committed before real-time events. Retry with the same sender/clientMessageId and payload returns the same persisted message; conflicting payload returns 409. Client merges message/notification events by server ID.
- History and notifications support cursor pagination and `afterId` catch-up. Notification read operations are scoped to the authenticated recipient.
- The `/chat` route uses the shared AuthProvider and `authenticatedFetch`; the A/B/C demo selector is not routed into the normal application UI.
- `tv4-smoke.mjs` tests real Auth accounts and does not create demo users. `tv4-load.mjs` opens 20 real SignalR sessions and writes measured p50/p95/max/errors to `frontend/test-results/*.json`.

## Deliberate dependencies and fail-closed behavior

At the handover date, main contains Auth but does not yet contain TV3's real Match model or TV5's GUID-based BlockPolicy. This patch registers `UnconfiguredChatMatchAccess` and `UnconfiguredChatBlockPolicy` until those owners supply the shared contracts. Chat returns 503/Hub errors for these missing dependencies; it does **not** fake authorization or silently read demo tables. This is expected until the adapters are finalized.

- After TV3's `Modules/DiscoveryMatching/Entities/Match.cs` is merged, run `Finalize-TV4-Dependencies.ps1`. It adds `Conversation.MatchId -> Matches.Id` and a real read adapter over `AppDbContext.Set<Match>()`.
- After TV5's BlockPolicy has `Guid` IDs and a reviewed `CanInteractAsync(Guid, Guid, CancellationToken)` signature, the same script installs the adapter. Do not adapt the older string-ID branch unchanged.
- `IChatReportTargetAccess.CanReportMessageAsync(reporterId, messageId)` is the TV4-to-TV5 authorization contract. It only confirms that the target message exists, belongs to a conversation that the reporter participated in, and was sent by the other participant; it never returns message content. It intentionally still permits a valid historical report after block/unmatch. TV5 must inject this contract for `targetType=Message` and return 404 for an invalid/out-of-scope target. The current main does not yet contain the integrated Safety report endpoint, so the UI surfaces 404/501 as pending rather than claiming success.
- TV2's public ProfileSummary is not on main, so this patch uses a neutral `Người dùng` label and never displays another user's email. Replace with a reviewed public profile lookup once TV2 supplies it.

## No old migration is copied

Do not copy `InitialTV4`, the old AppDbContext snapshot, old Program.cs, the old package-lock, or the whole Demo folder. The old migration contains FK relationships to demo entities and would be wrong on the shared Auth model. Once the real Match model and the planned shared schema are present, coordinate with TV1 for the one migration author at a time:

```powershell
dotnet tool restore
dotnet ef migrations add AddChatNotifications --project backend/DatingApp.Api --output-dir Infrastructure/Migrations
dotnet ef migrations script --idempotent --project backend/DatingApp.Api --output migration-review.sql
```

Review the snapshot and SQL before applying. Apply only to a dedicated local/test database with the correct Auth migrations already applied; never run this against a database that has data to preserve without a backup/review plan.

## Apply the patch

1. Clone/update the official repository in a separate folder, then create `feature/tv4-chat-v2` from `origin/main`.
2. Ensure the working tree is clean.
3. Extract this patch ZIP to a temporary folder and run `Apply-TV4-Main.ps1 -RepoRoot <path-to-repo>` from that temporary folder.
4. Review `git diff`. Run `npm.cmd --prefix frontend install` so package-lock is regenerated by npm; don't copy the old TV4 lockfile.
5. Coordinate the shared migration after TV3's Match model exists. Chat endpoints remain fail-closed until Match and Block adapters are wired.

## Run tests with real disposable test accounts

Set environment variables only in the local shell. Do not commit them or paste passwords/tokens into chat:

```powershell
$env:TV4_EMAIL_A = 'confirmed-test-a@example.test'
$env:TV4_PASSWORD_A = 'local-password-A'
$env:TV4_EMAIL_B = 'confirmed-test-b@example.test'
$env:TV4_PASSWORD_B = 'local-password-B'
$env:TV4_EMAIL_C = 'confirmed-test-c@example.test'
$env:TV4_PASSWORD_C = 'local-password-C'
```

Then, with PostgreSQL and Backend running against the dedicated test database:

```powershell
dotnet test DatingApp.slnx
cd frontend
npm run build
npm run test:tv4
npm run test:tv4-load
```

The smoke test requires real A/B/C accounts and an A/B match created by the real Matching module. The block/unmatch mutation cases are opt-in and must only use disposable test accounts/database:

```powershell
$env:TV4_TEST_ALLOW_RELATIONSHIP_MUTATION = 'true'
$env:TV4_TEST_ALLOW_UNMATCH = 'true'
npm run test:tv4
```

Unmatch is a persistent business action; use a disposable pair. A missing dependency or missing match should fail the run rather than count as a pass.

Load test output is saved to the ignored local folder `frontend/test-results/`. Attach the actual JSON to the issue/PR after sanitizing it. The test reports whether delivery p95 is under 2000 ms; no performance result is claimed in this source package because it has not been run on the team machine/network.

## API contract

- `GET /api/v1/conversations?page=1&pageSize=20`
- `GET /api/v1/conversations/{conversationId}/messages?limit=30&cursor=...`
- `GET /api/v1/conversations/{conversationId}/messages?afterId={messageId}&limit=30`
- `GET /api/v1/notifications?page=1&pageSize=20&cursor=...`
- `GET /api/v1/notifications?afterId={notificationId}&pageSize=20`
- `PATCH /api/v1/notifications/{notificationId}/read`
- SignalR `/hubs/chat`: `JoinConversation`, `LeaveConversation`, `SendMessage`; events `MessageCreated`, `NotificationCreated`, `MatchCreated`, `MatchEnded`.

All user IDs are GUIDs from the authenticated principal. Client-supplied user IDs are not accepted to select a sender/recipient. UTC timestamps are used. Groups are not treated as an authorization boundary; authorization is rechecked for operations.
