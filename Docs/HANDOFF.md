# Engineering hand-off

## Milestone 1 — Project foundation and combat domain

Status: implemented; static validation complete; Unity execution pending because Unity Editor is unavailable on this machine.

### Decisions

- Unity 2022.3 LTS is pinned for a stable mobile production baseline.
- Combat rules are separated from GameObjects, animation, and UI. This makes balance tests fast and enables server-side result validation later.
- Presentation receives `IBattleEventSink` events and never owns combat truth.
- All timers advance through an explicit `Tick(deltaTime)` method.

### Integration contract

- Construct `BattleSimulation` with 1-5 `HeroDefinition` values, one or more enemy waves, and base health.
- Call `Start()` once, then `Tick(Time.deltaTime)` while running.
- Call `UseUltimate(heroId)` from the HUD.
- Render damage, energy, and result changes from `IBattleEventSink`.
- Treat `BattleSimulation` as the authoritative state for a local battle.

### Known limitations

- Enemies currently attack the first living hero and reach the base only after all heroes die.
- Ultimate targets the first living enemy.
- Movement, status effects, faction modifiers, rewards, persistence, and UI belong to later milestones.
- `SetEnergyForTesting` exists to keep tests focused and will be replaced by an internal test hook when assembly visibility is configured.

### Verification

- Five EditMode tests cover start, victory, multi-wave progression, ultimate rules, and defeat.
- The local machine has only the .NET runtime and no SDK or Unity Editor, so compilation/test execution is not available here yet.

## Milestone 2 — Playable runtime shell

Status: implemented; Unity execution pending.

### Included

- `HeroConfig` and `StageConfig` ScriptableObject authoring types.
- `BattleRunner` lifecycle adapter and event sink.
- A portrait-friendly runtime HUD with wave/base status, unit health, energy, ultimate buttons, speed controls, result state, and retry.
- `GameBootstrap` creates a playable three-wave demo even from an empty scene.
- Authored configs override demo content when assigned in the Inspector.

### How to review

1. Open or create any scene and enter Play Mode.
2. The bootstrap creates `IdleHeroDefense/BattleRunner` automatically.
3. Observe three waves, use ultimates when energy reaches 100, change speed, and retry after the result.
4. To author content, create assets through **Create > Idle Hero Defense > Hero/Stage** and assign them to a scene `BattleRunner`.

The immediate-mode HUD is intentionally a functional vertical slice. The production uGUI screens and navigation will replace it after progression and save contracts stabilize.

## Milestone 3 — Progression, economy, idle rewards, and persistence

Status: implemented; Unity execution pending.

### Included

- Versioned `PlayerProfile` with currencies, campaign progress, idle timestamp, and hero progress.
- Atomic grant/spend operations with validation and explicit results.
- Hero level-up with deterministic escalating gold cost and a level cap.
- Offline gold calculation based on UTC, highest stage, whole minutes, and an eight-hour cap.
- JSON persistence through `PlayerPrefs`, new-profile creation, corrupt-save recovery, and lifecycle saves.
- Three additional EditMode tests for idle cap, successful upgrade, and insufficient funds.

### Security boundary

`PlayerPrefs` is appropriate only for the offline prototype. Before any real-money, summon, leaderboard, or competitive feature ships, the backend must become authoritative for currencies, claim timestamps, inventory, and transaction idempotency.

## Milestone 4 — App navigation and progression UI

Status: implemented; Unity execution pending.

### Included

- Safe-area-aware mobile app shell with Home, Heroes, Battle, Summon, and Shop navigation.
- Home displays account currencies, campaign progress, offline chest preview, claim, and continue CTA.
- Heroes displays the starter roster, level, role/faction, upgrade cost, affordability state, and upgrade feedback.
- Battle HUD is now isolated to the Battle tab and reserves space for bottom navigation.
- Winning a battle grants 100 gold, advances campaign progress, and persists exactly once per battle instance.
- A default three-hero roster is initialized for new and migrated empty profiles.
- Summon and Shop are explicit safe entry points; neither fabricates client-authoritative paid transactions.

### UX hand-off

The shell implements the approved information architecture and interaction flows using Unity IMGUI so it remains scene-independent. Visual production should replace each drawing method with prefab-based uGUI views while preserving `AppNavigation`, `ProfileController`, and `BattleRunner` contracts.

## Milestone 5 — Summon, shards, quests, and achievements

Status: implemented; Unity execution pending.

### Included

- One-pull summon with explicit 100-gem cost, 90/10 Rare/Epic distribution, and guaranteed Epic on the tenth non-Epic pull.
- Summon results add shards and unlock the corresponding hero record; Epic results reset pity.
- UI displays rates, current pity, affordability, rarity, shards, and guaranteed-pity results.
- Profile normalization keeps newly added collections safe when older JSON saves are loaded.
- Three account quests cover first victory, campaign stage 5, and hero level 3, with persisted one-time claims.
- Battle victories now increment the quest-compatible lifetime win counter.
- Two tests cover guaranteed pity and duplicate quest-claim prevention.

### Backend hand-off

`IRandomSource` makes summon rules deterministic in tests, but production summon outcomes must be generated and signed by the backend. The client should submit a transaction id and render the returned result; it must never select paid outcomes locally.

## Milestone 6 — Formation, faction synergy, game modes, and analytics

Status: implemented; Unity execution pending.

### Included

- Persisted 1-5 hero formation with locked-hero and duplicate-slot validation.
- Faction synergy rules: two matching heroes grant +10% attack; three also grant +20% health; five grant +30% attack.
- Hero level and faction modifiers are applied when the playable battle is constructed.
- Campaign difficulty scales by stage, Tower difficulty by floor, and Daily Dungeon uses its own fixed modifier.
- Daily Dungeon supports three UTC-reset victories per day and grants 300 gold.
- Endless Tower advances floor, scales rewards, and grants 20 gems every fifth completed floor.
- Home mode selectors show attempts/floor and enforce availability before battle creation or retry.
- Typed analytics sink and debug implementation emit `stage_started`, `stage_completed`, and `summon_result` with useful dimensions.
- Three additional tests cover synergy, invalid duplicate formations, and the daily UTC reset/limit.

### Design hand-off

`HeroCatalog` is a temporary code-backed content source for the zero-scene demo. Production content import should generate ScriptableObjects from the balance sheet while retaining stable hero ids. `IAnalyticsSink` is the adapter point for the selected analytics SDK.

## Milestone 7 — Data-driven abilities, status effects, and battle statistics

Status: implemented; Unity execution pending.

### Included

- Composable abilities define damage, targeting, and an optional status effect independently of presentation.
- Targeting supports first enemy, lowest-health enemy, and every living enemy.
- Burn supports duration, tick interval, and per-tick damage; Stun suppresses enemy attacks for its active duration.
- Starter hero identities now differ mechanically: Ember Knight stuns, Forest Archer executes the weakest target, and Shade Mage applies an area burn.
- Level and faction scaling preserve each hero's configured ability behavior.
- Combat statistics aggregate actual applied damage by source, including over-time damage, and appear on the result HUD.
- Three tests cover area targeting/statistics, burn timing, and stun attack suppression.

### Extension contract

New skills should be assembled from `AbilityDefinition`, `TargetingRule`, and `StatusEffectDefinition`. Healing, shields, taunt, and buffs should extend the effect model rather than adding hero-specific branches to `BattleSimulation`.

## Milestone 8 — Remote config, LiveOps, and inbox

Status: implemented; Unity execution pending.

### Included

- Versioned and validated live configuration with safe built-in defaults.
- StreamingAssets baseline, cached last-known-good remote response, eight-second request timeout, and rejection without replacing a valid config.
- Campaign, Daily Dungeon, and Tower rewards/limits now read from live config.
- UTC-windowed LiveOps events can publish inbox rewards without a client release.
- Welcome and event mail claims are persisted and idempotent; expired mail cannot be claimed.
- Home shows inbox messages, reward contents, and claim state.
- Analytics records successful mail claims.
- Three tests cover invalid config rejection, mail idempotency, and UTC event visibility.

### Operations hand-off

Set `idle_hero_defense.live_config.endpoint` in PlayerPrefs during environment bootstrap to point at the HTTPS JSON endpoint. The client uses `Assets/StreamingAssets/live-config.json` when offline and a cached last-known-good response when the endpoint is temporarily unavailable. Keep `schemaVersion` at 1 until a migration path is implemented.

## Milestone 9 — Backend contract, authentication, and idempotent economy

Status: implemented; Unity execution pending.

### Included

- Async `IGameBackend` boundary for guest authentication and authoritative economy transactions.
- REST implementation posts JSON to `/v1/auth/guest` and `/v1/economy/transactions`, supports bearer authentication, cancellation, and timeouts.
- Offline-authoritative implementation follows the same contract for local development.
- Every economy request carries an idempotency key and deterministic fingerprint.
- The last 200 transaction receipts persist in the player profile, allowing replay after save/load without applying a grant or spend twice.
- Reusing a key with different transaction data is rejected as a collision.
- Runtime backend controller selects REST when `idle_hero_defense.api_url` is configured and otherwise authenticates against the offline backend.
- Authentication and successful currency transactions emit analytics events.
- Two tests verify replay behavior and collision rejection.

### Server hand-off

The production service must implement the two endpoints above, use the authenticated player id rather than a client-supplied id, persist idempotency keys transactionally, and return the complete authoritative gold/gem balances. Configure only an HTTPS API URL. The client-side offline backend is a development adapter, not an anti-cheat boundary.

## Milestone 10 — Onboarding, feature unlocks, and accessibility

Status: implemented; Unity execution pending.

### Included

- Persisted tutorial state machine: welcome, open Heroes, upgrade, start campaign, win, complete.
- Tutorial actions only advance the expected step and emit analytics.
- Feature gates progressively unlock Daily Dungeon, Summon, Endless Tower, and Shop by campaign stage.
- Locked bottom-navigation entries communicate their required stage; mode buttons provide toast feedback.
- Settings persist music, sound effects, reduced motion/flashes, and combat damage text preferences.
- Battle event text respects the damage-text preference.
- Two tests cover ordered tutorial advancement and feature thresholds.

### UX hand-off

The onboarding is action-driven and does not block unrelated rendering. Production prefabs should anchor coach marks to the corresponding controls and preserve the same `TutorialAction` signals. Audio mixers and VFX controllers should consume the persisted settings rather than duplicating preference storage.

## Milestone 11 — Equipment, artifacts, base, and formation editor

Status: implemented; Unity execution pending.

### Included

- Persistent equipment instances with Weapon/Armor slots, levels, ownership, and equipped hero.
- Starter inventory, best-available auto-equip, per-item material upgrades, and derived hero attack/health bonuses.
- Account-wide War Banner and Guardian Idol artifacts increase hero attack and base health.
- Gold-funded base upgrades increase battle base health by 100 per level.
- Daily Dungeon now supplies gear materials; Endless Tower supplies artifact dust.
- The Heroes screen lists every unlocked hero, supports add/remove formation with 1-5 constraints, auto-equip, gear upgrades, and live synergy summary.
- Battle construction applies hero level, equipment, artifact, and faction modifiers in a deterministic order.
- Home exposes base/artifact upgrades and current material balances.
- Four tests cover auto-equip slots, equipment scaling/cost, meta progression, and mode material rewards.

### Balance hand-off

Equipment definitions are code-backed for the vertical slice and use stable ids. Move these rows into the same generated content pipeline as heroes before content production. Current modifier order is level → equipment → artifact → faction synergy; changing that order changes balance materially and requires regression snapshots.

## Milestone 12 — IAP validation, restore purchases, and rewarded ads

Status: implemented; platform SDK integration pending.

### Included

- Product catalog separates consumables and non-consumable entitlements.
- Store, receipt validator, and rewarded-ad provider are SDK-independent interfaces.
- Purchases grant rewards only after a validation response matches the store transaction id.
- Processed purchase ids persist, making delivery idempotent across retries and app restarts.
- REST receipt validator posts authenticated receipts to `/v1/iap/validate`.
- Restore flow revalidates every returned purchase before delivery.
- Rewarded ads grant gems only after a completed-view callback and enforce a five-per-UTC-day cap.
- Shop UI displays products, store state, restore action, rewarded-ad availability, and remaining daily views.
- Default adapters are deliberately unavailable: development builds cannot fabricate a paid receipt or ad completion.
- Two tests cover exactly-once purchase delivery and rewarded-ad daily reset/cap.

### Platform hand-off

Implement `IStoreAdapter` with Unity IAP/App Store/Google Play and `IRewardedAdProvider` with the selected ads SDK, then inject both through `MonetizationController.Configure`. The server must validate receipts directly with the platform, bind transactions to the authenticated player, and return the canonical transaction id. Never replace `RejectingReceiptValidator` with client-only signature checks in production.

## Milestone 13 — Validation, automated builds, and CI

Status: implemented; execution pending a Unity-enabled runner and license.

### Included

- Editor project validator checks live-config integrity, all expected hero definitions, combat values, unique product ids, product rewards, and profile schema.
- Validation is exposed through an editor menu, batch method, and EditMode test.
- Build entry points create the minimal Bootstrap scene when absent, apply environment-driven version metadata, validate before building, and fail on non-success reports.
- Android and Windows build methods share the same validation path.
- PowerShell test runner resolves a supplied or Hub-installed Unity Editor, writes NUnit XML, and propagates failure exit codes.
- GitHub Actions runs EditMode tests before producing an Android artifact and uploads test/build outputs.
- Git LFS rules cover common binary art formats while source/YAML files use stable LF endings.

### CI hand-off

Configure `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` repository secrets before enabling the workflow. The pinned editor version is `2022.3.62f1`; update `ProjectVersion.txt`, the workflow, and developer installations together. Android signing is intentionally absent from development CI—add protected keystore secrets only in a separate release workflow.

## Milestone 14 — Energy and repeatable daily/weekly quests

Status: implemented; Unity execution pending.

### Included

- Energy has a cap of 60 and regenerates one point per complete five-minute UTC interval, including offline time.
- Campaign costs no energy; Daily Dungeon costs 5 and Endless Tower costs 3 per start/retry.
- Mode availability checks attempt limits and energy before mutating either resource.
- Header displays current energy and the next regeneration countdown.
- Daily counters cover battle wins, hero upgrades, and summons; weekly counters cover ten battle wins.
- Daily state resets at UTC date change and weekly state resets on UTC Monday without erasing lifetime achievements.
- Repeatable rewards support both gold and gems and persist claim state for the active period.
- Battle, level-up, and summon flows record quest progress at their authoritative success point.
- Four tests cover regeneration/cap, mode costs, daily reset, and Monday weekly reset.

### Economy hand-off

Energy deliberately gates side modes only, keeping the campaign playable. For an online release, move energy timestamp, quest counters, and claims to the backend; retain these client services as prediction/presentation models fed by the server snapshot.

## Milestone 15 — Diagnostics, analytics batching, and lifecycle hardening

Status: implemented; Unity execution pending.

### Included

- Analytics events now carry unique id, session id, UTC timestamp, and normalized key/value properties.
- Queue retains up to 200 events, persists through restart, sends batches of 20, and removes events only after transport success.
- Failed sends remain queued for the next 15-second flush cycle.
- Debug and HTTP transports share the same batch contract; endpoint is selected through environment PlayerPrefs.
- Error, exception, and assertion logs create bounded `client_error` events without recursive logging.
- Session start, foreground, and background lifecycle events are emitted; queue and player profile persist on pause/focus loss.
- Runtime selects 30 FPS/reduced texture resolution below 3 GB system memory, otherwise 60 FPS, and disables v-sync conflicts.
- Two tests verify successful structured flushing and failure retention.

### Observability hand-off

Set `idle_hero_defense.analytics_url` to the HTTPS ingestion endpoint. The service should deduplicate by event id and accept retries. Add a native crash SDK for failures that terminate before the managed queue persists; `client_error` covers recoverable managed errors and exceptions, not native crash dumps or ANRs.

## Milestone 16 — Localization and balance content snapshots

Status: implemented; Unity execution pending.

### Included

- Runtime localization loads typed JSON tables from Resources with English fallback and explicit missing-key fallback.
- English and Vietnamese tables cover navigation, primary Home actions, progression headings, Shop/Summon, claims, and accessibility settings.
- Language choice persists in the player profile and emits analytics when changed.
- Settings UI switches language immediately without a restart.
- Hero and equipment catalogs expose stable enumerable rows for tooling.
- Canonical CSV snapshot includes every hero combat row, ability targeting/status, equipment stat row, product grant, and critical economy default.
- Editor tools export the current snapshot or import-and-validate a reviewed snapshot.
- Project validation and CI fail when the committed balance snapshot is stale.
- Two tests cover Vietnamese/English fallback and deterministic complete snapshot generation.

### Content hand-off

Treat `Docs/balance-snapshot.csv` as a reviewed contract: intentional balance changes must export a new snapshot and include the diff in code review. Add new locale files under `Assets/Resources/Localization/<language>.json`; untranslated keys fall back through the call-site English text until a full translation table is supplied.

## Milestone 17 — Visual battlefield, pooling, and combat feedback

Status: implemented; visual runtime review pending Unity Editor.

### Included

- A scene-independent orthographic battlefield and camera are created automatically from the empty bootstrap scene.
- Hero and enemy views display faction/hostile colors, labels, and live health bars at separate formation lines.
- Unit views are synchronized from authoritative `BattleSimulation` snapshots and pooled across waves/retries.
- Damage events drive pooled floating numbers; ultimate damage has distinct emphasis.
- Reduced-motion and damage-text accessibility preferences are honored by visual feedback.
- Player base has a dedicated world marker and current base health remains in the top HUD.
- Battle HUD is split into compact top/bottom overlays so the center battlefield stays visible.
- Ultimate buttons show per-hero energy and remain disabled until ready.
- Generic pool detects double release, reuses instances, supports prewarming, and has direct test coverage.

### Art hand-off

`BattleUnitView` intentionally uses a generated white sprite so the code runs without imported assets. Replace its body renderer and TextMesh children with Addressable hero/enemy prefabs while preserving `Configure`, `UpdateHealth`, and pooling lifecycle. The combat model must remain unaware of animation, VFX, and camera objects.

## Milestone 18 — Audio buses, combat hooks, and haptic feedback

Status: implemented; audio-art and device review pending Unity Editor/hardware.

### Included

- Persistent music and SFX preferences directly mute independent runtime audio sources.
- Placeholder music, hit, ultimate, victory, and defeat clips are synthesized at runtime, keeping the zero-asset build audible and testable.
- Battle damage/state events trigger SFX without coupling audio to combat rules.
- Music pauses with the application and resumes only when the user preference permits it.
- Haptic feedback is behind `IHapticProvider`, triggers only for ultimates, and is disabled by either the haptics preference or reduced-motion mode.
- Settings UI persists the haptic option and English/Vietnamese locale tables include it.
- Event subscriptions and generated clips are cleaned up on destruction.
- A test verifies haptic preference and reduced-motion behavior.

### Audio hand-off

Replace generated clips with Addressable `AudioClip` assets and route the two AudioSources through Music/SFX `AudioMixerGroup`s. Preserve battle event subscriptions and preference ownership. Platform-specific haptic SDKs can replace `UnityHapticProvider` without changing gameplay or UI code.

## Milestone 19 — Backend reference service

Status: implemented; automated execution available in CI, local execution pending .NET 8 SDK.

### Included

- ASP.NET Core 8 service implements guest authentication, profile read/sync, economy, summon, receipt validation, analytics ingestion, and health endpoints.
- Opaque 384-bit bearer tokens are stored only as SHA-256 hashes and expire after 30 days.
- Economy mutations are serialized, bounds checked, persisted atomically, replayable by idempotency key, and reject key collisions with different fingerprints.
- Summon cost, rarity roll, hero selection, pity, shards, balances, and request replay are server-owned.
- Profile sync uses optimistic version checks, prevents rollback/large stage jumps, and validates unique 1-5 formations.
- Purchase delivery is transaction-idempotent and receipt verification fails closed by default.
- Analytics ingestion deduplicates bounded event ids.
- Single-instance JSON storage requires no external service and supports an override path; Docker packaging is included.
- Three HTTP integration tests cover authentication/economy replay/collision, authorization, and fail-closed receipt handling.
- CI now requires backend tests and Unity EditMode tests before Android build.

### Production backend hand-off

The JSON repository is a reference transaction boundary, not a horizontally scalable database. Replace `GameStore` with PostgreSQL/Redis transactions while retaining endpoint DTOs and invariants. Implement Apple/Google receipt verification behind `IReceiptVerifier`, deploy behind TLS/rate limiting, rotate expired sessions, and move analytics to a dedicated ingestion pipeline before production traffic.

## Milestone 20 — Remote profile, summon, and economy client integration

Status: implemented; end-to-end execution pending Unity/.NET-enabled environment.

### Included

- `IGameBackend` now covers profile pull/update and authoritative summon in addition to authentication/economy.
- REST DTOs match backend camel-case responses and convert dictionary-backed server shards into Unity-compatible arrays.
- Online login pulls gold, gems, stage, pity, formation, shards, and optimistic profile version before enabling transactions.
- Offline backend implements the identical contract and persists summon request receipts for replay safety.
- Summon UI no longer calls local RNG directly; it waits for backend readiness and applies returned balances/result.
- Hero level-up gold spending uses the authoritative economy endpoint and only increments after a successful response.
- Campaign completion and formation changes request coalesced optimistic profile sync; conflicts pull the authoritative snapshot.
- Server profile response is narrowed to an explicit DTO rather than leaking persistence models/dictionaries.
- Campaign stage sync grants its configured reference reward server-side and blocks jumps larger than one stage.
- Two Unity tests cover profile version sync and summon replay; a backend HTTP test covers server-owned idempotent summon.

### Integration hand-off

Set `idle_hero_defense.api_url` before startup to enable online authority. With no URL, the same flows use `OfflineGameBackend`. The current reference sync owns balances, stage, formation, pity, and shards; expand typed backend commands for equipment/artifact/base mutations before allowing those systems in competitive or paid production environments.

## Milestone 21 — Authoritative progression mutations and claims

Status: implemented; end-to-end execution pending Unity/.NET-enabled CI.

### Included

- Backend progression command enum covers base, artifact, equipment, achievement, and mail mutations.
- Every command has request id plus kind/target fingerprint; successful and failed results are replayable and key collisions are rejected.
- Server validates costs, balances, known equipment/artifacts, achievement eligibility, welcome-mail allowlist, and claim history before mutation.
- Server persists gear materials, artifact dust, base/equipment/artifact levels, hero levels, wins, claims, and mutation receipts.
- Hero-level economy reasons are server-validated against current level and exact cost before recording the new level.
- Unity online/offline adapters expose the same progression command contract.
- Controller applies authoritative balances/materials/levels only after success and emits mutation analytics.
- Home/Heroes claim and upgrade controls no longer call local economy services directly; local presentation bypass methods were removed.
- One Unity and one HTTP integration test cover exactly-once base upgrade plus idempotency collision rejection.

### Authority hand-off

Repeatable daily/weekly claims and event mail beyond `welcome_v1` remain tied to the LiveOps schedule and should receive dedicated server commands once their schedule is hosted by the backend. The current endpoint intentionally rejects unknown mail ids instead of trusting client-supplied rewards.

## Milestone 22 — Authoritative rewards, repeatable quests, and IAP delivery

Status: implemented; end-to-end execution pending Unity/.NET-enabled CI.

### Included

- Campaign, Daily Dungeon, and Endless Tower completion rewards are calculated, applied, versioned, and receipt-cached by the backend.
- Battle responses return authoritative balances, materials, stage/floor, attempts, wins, and an explicit reward breakdown.
- Idle gold uses server UTC, whole minutes, stage rate, and an eight-hour cap; claims are request-idempotent.
- Backend owns daily/weekly counters, UTC reset periods, eligibility, claim sets, and gold/gem rewards.
- Hero upgrade, summon, and battle success update their corresponding repeatable counters server-side.
- Unity battle, idle chest, and repeatable-quest UI no longer invoke local reward mutations.
- Full profile pull restores idle timestamp, mode progress, repeatable counters/claims, materials, levels, and entitlements.
- Online IAP validation no longer grants client-side currency; it refreshes the profile after server delivery. Offline adapters retain local delivery for development.
- Battle and idle receipts are persisted for offline replay parity.
- One Unity test covers battle/idle exactly-once rewards; one HTTP test covers authoritative idempotent battle completion.

### Security hand-off

The reference backend accepts a battle-complete command but does not yet validate a signed combat transcript. Competitive modes should submit deterministic input/config hashes and server-replay the simulation before reward issuance. Rate-limit all reward endpoints and retain receipts beyond the JSON reference store's bounded development history.

## Milestone 23 — Server energy reservations and signed ad rewards

Status: implemented; platform ad adapter and end-to-end execution pending.

### Included

- Battle start is now an authenticated server command that regenerates UTC energy, validates mode/attempt availability, spends once, and returns a 30-minute ticket.
- Battle completion requires the matching unexpired, single-use player/mode ticket before issuing rewards.
- Start requests are idempotent; replay returns the same ticket without spending energy twice and kind collisions fail.
- Unity waits for start authorization before constructing a battle; retry obtains a new ticket and no presentation method can start directly.
- Profile snapshots include authoritative energy/timestamp; local regeneration is display prediction while the server remains the mutation authority.
- Rewarded-ad provider contract returns a provider transaction id instead of a trusted boolean.
- Server-to-server ad callback uses expiring HMAC-SHA256 signatures, fixed-time comparison, player/placement binding, daily cap, and transaction idempotency.
- Client claims only a callback-verified transaction and applies the returned gem/ad-count snapshot.
- Online ad and IAP rewards no longer use local currency grants; offline adapters retain explicit development behavior.
- Backend integration tests cover exactly-once energy reservation and signed exactly-once ad delivery.

### Ads operations hand-off

Set `AdCallbackSecret` through the deployment secret store, never `appsettings.json`. Configure the ad network's server-side verification callback to sign `playerId|transactionId|placement|expiresUtcTicks`. The mobile SDK adapter must return the same transaction id after completion; client-only completion callbacks are insufficient for reward delivery.

## Milestone 24 — Fixed-step combat transcripts and reward throttling

Status: implemented; end-to-end execution pending Unity/.NET-enabled CI.

### Included

- Runtime combat now advances on deterministic 100 ms simulation ticks, independent of render frame rate and selected display speed.
- Every applied damage event is recorded with step, source, target, amount, and ultimate flag; the client submits a canonical SHA-256 transcript hash with battle completion.
- Battle tickets snapshot the active hero ids and exact enemy-health workload for the selected mode/difficulty at authorization time.
- The backend rejects missing, oversized, out-of-order, out-of-range, hash-mismatched, expired, wrong-player, wrong-mode, or insufficient-victory transcripts before granting rewards.
- Transcript verification uses fixed-time hash comparison and preserves a ticket after failed verification so a transient/corrupt upload can be retried.
- The offline adapter validates the same transcript shape and hash for development parity.
- Authentication and high-value reward routes have separate fixed-window, per-IP rate-limit policies with HTTP 429 rejection.
- Unity coverage verifies deterministic/tamper-sensitive transcript hashing; backend coverage verifies a valid victory and a tampered-hash rejection.

### Security hand-off

This milestone validates transcript integrity, ticket binding, event bounds, and exact victory workload; it does not make a client-generated transcript cryptographically trustworthy. For adversarial competitive modes, port the pure combat domain to a server worker and replay signed inputs/config snapshots, or run battles fully server-side. Put the API behind a proxy that forwards the real client IP only from trusted hops, and replace the in-process limiter with a distributed gateway policy when horizontally scaling.
