# Idle Hero Defense backend reference

ASP.NET Core 8 reference service implementing the client contracts.

```powershell
cd Backend/IdleHeroDefense.Api
dotnet run
```

Set the Unity PlayerPrefs key `idle_hero_defense.api_url` to the HTTPS service URL. Local state defaults to `App_Data/game-store.json`; override it with `GAME_STORE_PATH`.

Endpoints:

- `POST /v1/auth/guest`
- `GET/PUT /v1/profile`
- `POST /v1/economy/transactions`
- `POST /v1/summon`
- `POST /v1/progression/mutate`
- `POST /v1/rewards/battle`
- `POST /v1/rewards/idle`
- `POST /v1/rewards/quest`
- `POST /v1/battles/start`
- `POST /v1/ads/callback`
- `POST /v1/ads/claim`
- `POST /v1/iap/validate`
- `POST /v1/analytics/batch`
- `GET /health`

Battle flow is two-phase: call `/v1/battles/start`, then submit the returned ticket plus the fixed-step
damage transcript, total step count, and canonical SHA-256 hash to `/v1/rewards/battle`. Authentication is
limited separately from battle/idle/quest/ad reward mutations; rejected requests return HTTP 429.

The JSON store is suitable for a single-instance reference deployment and local integration tests. Production must replace it with a transactional database and shared session/idempotency storage.

Transcript validation proves that a ticket-bound formation supplied a bounded, internally consistent victory
workload. A competitive production service should additionally replay deterministic battle inputs on the server;
client-authored transcripts alone are not an anti-cheat trust boundary.

Receipt verification is fail-closed. Test receipts work only when the ASP.NET environment is Development and `AllowTestReceipts=true`; their format is `test:{transactionId}:{productId}`. Production must provide an `IReceiptVerifier` backed by Apple/Google APIs.
