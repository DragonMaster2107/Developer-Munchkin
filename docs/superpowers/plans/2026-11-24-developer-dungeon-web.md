# Developer Dungeon – Web-Oberfläche Implementation Plan (Plan 2 von 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine installierbare, offline-fähige Blazor-WebAssembly-PWA, mit der 3–6 Spieler eine komplette Partie Developer Dungeon auf einem herumgereichten Gerät spielen können, automatisch auf GitHub Pages veröffentlicht.

**Architecture:** `DeveloperDungeon.Web` (Blazor WASM Standalone, PWA) referenziert die fertige Engine. Alle Spiel-Logik der Oberfläche (Gerät übergeben, Aufbau-Runde, Eingreifen, Countdown, Kartenaktionen, Animations-Hinweise, Speichern) liegt in schlanken C#-Klassen unter `Logic/`, die ohne Browser testbar sind; Razor-Komponenten unter `Components/` und `Pages/` zeigen nur an und rufen diese Klassen. Die Engine wird ausschließlich über `IGameSession`, `GameView`, `CardCatalog`, `Game.New/Load/Save` und die Befehls-Records angesprochen.

**Tech Stack:** .NET 10, Blazor WebAssembly Standalone (`dotnet new blazorwasm --pwa`), eigenes CSS (keine UI-Bibliothek), xUnit + bUnit, `Microsoft.Extensions.TimeProvider.Testing` (nur Tests), GitHub Actions + GitHub Pages.

**Spec:** `docs/superpowers/specs/2026-11-23-developer-dungeon-design.md` (v. a. Abschnitte 5, 6.4, 7.3–7.5, 8)

**Vorgänger:** Plan 1 (Engine) ist fertig. Relevante öffentliche Engine-API (nicht ändern, nur nutzen):
- `Game.New(IReadOnlyList<PlayerSetup> players, CardCatalog catalog, ulong seed)`, `Game.Load(string json, CardCatalog catalog)` (wirft `InvalidDataException` bei kaputtem/veraltetem Stand), `game.Save()`, `game.GetView(string? viewerId)`, `record PlayerSetup(string Name, string Color)`
- `CardCatalog.LoadEmbedded()`, `catalog.Get(id)`, `catalog.All`; `CardDefinition` (Id, Name, Deck, Kind, Flavor, RulesText, Effects, Tags, Level, RewardLevels, RewardLoot, BadStuff, Slot, Hands, Bonus, Value, RequiresDepartment, RequiresLanguage, Key)
- `IGameSession` (`GetView`, `Catalog`, `SendAsync`, `CheckAsync`, `event Action<IReadOnlyList<GameEvent>>? StateChanged`), `LocalGameSession(Game game, Action<string>? onSaved = null)`
- `GameView` (Phase, ActivePlayerId, TurnNumber, Players: `PublicPlayerView`(Id, Name, Color, Level, Title, HandCount, Equipped, Backpack, Departments, Languages, BasePower), MyHand, Combat: `CombatView(CombatState State, CombatSummary Summary)`, TicketDeckCount, LootDeckCount, TicketDiscardTop, LootDiscardTop, CurrentChoice: `PendingChoice`(PlayerId, Kind, RecipientPlayerId, SourceCardId), WinnerId, RecentLog)
- Befehle und Ereignisse: `src/DeveloperDungeon.Engine/Commands/GameCommand.cs`, `src/DeveloperDungeon.Engine/Events/GameEvent.cs`

**Nachfolger:** Plan 3 – Kartenpool ~90 Karten mit Texten, SVG-Grafiken für alle Karten, Balancing.

## Global Constraints

- `net10.0`, `Nullable`/`ImplicitUsings`/`TreatWarningsAsErrors` aus `Directory.Build.props` gelten auch für Web und Web.Tests (Trimming-Warnungen beim Publish sind Fehler).
- Die Engine bleibt unverändert und ohne Paketabhängigkeiten; das Web-Projekt nutzt keine UI-Komponentenbibliothek (eigenes CSS, Spec 11).
- Alle sichtbaren Texte Deutsch; Code-Bezeichner Englisch.
- Spielname überall „Developer Dungeon“ (Titel, Manifest, Startseite). Der Name „Munchkin“ erscheint nirgends in der App.
- Hände anderer Spieler sind nur nach dem „Gerät übergeben“-Bildschirm sichtbar (Spec 8). Öffentliches (Stufe, Ausrüstung, Abteilung, Sprache, Kampf, Protokoll) ist immer sichtbar.
- Regelwidrige Aktionen werden nicht angeboten oder sind ausgegraut mit Begründung (Spec 7.2); Begründung kommt von `IGameSession.CheckAsync`. Eine abgelehnte `SendAsync` zeigt den Ablehnungstext als Hinweis-Toast.
- Countdown nach „Kampf auswerten“: Standard **5 Sekunden**, einstellbar (0–10); Animationen blockieren nie länger als **1,5 s**, sind abschaltbar und respektieren `prefers-reduced-motion` (Spec 6.4).
- Spielstand wird nach jedem angenommenen Befehl in `localStorage` gespeichert (Schlüssel `dd.save.v1`), Einstellungen unter `dd.settings.v1`.
- Primär Tablet/Laptop quer; Smartphone (≥ 360 px Breite) muss ohne horizontales Scrollen der Seite funktionieren (Hand als wischbare Leiste).
- GitHub Pages unter dem Repo-Pfad `/Developer-Munchkin/`; lokal `/`.
- Commit-Stil `[ADD]`/`[FIX]`/`[CHG]` + deutsche Beschreibung, Zeile `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; nie `.superpowers/` committen.

## Review Focus

1. **Seite neu geladen mitten im Spiel / mitten in der Aufbau-Runde / während eines Kampfs mit Countdown:** „Fortsetzen“ stellt den Spielstand her, die Hand ist zunächst verdeckt (Gerät-übergeben an den aktiven Spieler bzw. den nächsten Aufbau-Spieler), ein laufender Countdown ist weg und muss neu gestartet werden. → Task 3 (`Resume_InCombat_HidesHandAndHasNoCountdown`, `Resume_InSetup_ContinuesWithFirstUnfinishedPlayer`).
2. **Kaputter oder veralteter Spielstand in localStorage:** Startseite zeigt „Der gespeicherte Spielstand passt nicht mehr zu dieser Version.“ und bietet nur „Neues Spiel“; kein Absturz. → Task 5 (`StartPage_CorruptSave_ShowsHintAndOffersNewGameOnly`).
3. **Zug geht an den nächsten Spieler über, während noch die Hand des alten Spielers offen ist:** Hand wird sofort verdeckt, Gerät-übergeben an den neuen aktiven Spieler. → Task 3 (`TurnEnded_HidesHandAndRequestsNextPlayer`).
4. **Eingreifender Spieler spielt eine Karte und vergisst „Fertig“:** Seine Hand bleibt nicht dauerhaft offen – jeder Wechsel zurück zum aktiven Spieler (Kampf auswerten, Zugende) verlangt Gerät-übergeben. → Task 8 (`ResolvingCombat_WhileInterjecting_ReturnsDeviceToActivePlayer`).
5. **Mehr als 5 Handkarten bei Feierabend:** Die App fordert genau N Karten zum Abgeben an und lässt „Feierabend“ erst mit genau N ausgewählten zu. → Task 6 (`EndTurn_WithSevenCards_RequiresSelectingExactlyTwo`).

---

### Task 1: Web-Projekt, Test-Projekt, Grundlayout

**Files:**
- Create: `src/DeveloperDungeon.Web/` (per Template), `tests/DeveloperDungeon.Web.Tests/DeveloperDungeon.Web.Tests.csproj`, `tests/DeveloperDungeon.Web.Tests/StartupTests.cs`, `.claude/launch.json`
- Modify: `DeveloperDungeon.slnx`, `src/DeveloperDungeon.Web/wwwroot/index.html`, `wwwroot/manifest.webmanifest`, `wwwroot/css/app.css`, `Layout/MainLayout.razor`, `Pages/Home.razor`

**Interfaces:**
- Produces: Projekt `DeveloperDungeon.Web` (Namespace-Wurzel `DeveloperDungeon.Web`), Testprojekt mit bUnit; CSS-Design-Tokens in `app.css` (`--dd-monster`, `--dd-loot`, `--dd-curse`, `--dd-class`, `--dd-ink`, `--dd-paper`, `--dd-radius`, Abstände), die alle späteren Komponenten nutzen; `.claude/launch.json`-Eintrag `web` (`dotnet run --project src/DeveloperDungeon.Web`, Port aus `Properties/launchSettings.json`).

- [ ] **Step 1: Gerüst anlegen**

```powershell
dotnet new blazorwasm --pwa -n DeveloperDungeon.Web -o src/DeveloperDungeon.Web -f net10.0
dotnet new xunit -n DeveloperDungeon.Web.Tests -o tests/DeveloperDungeon.Web.Tests -f net10.0
dotnet sln add src/DeveloperDungeon.Web tests/DeveloperDungeon.Web.Tests
dotnet add src/DeveloperDungeon.Web reference src/DeveloperDungeon.Engine
dotnet add tests/DeveloperDungeon.Web.Tests reference src/DeveloperDungeon.Web
dotnet add tests/DeveloperDungeon.Web.Tests package bunit
dotnet add tests/DeveloperDungeon.Web.Tests package Microsoft.Extensions.TimeProvider.Testing
```
Template-Beispielseiten (Counter, Weather, NavMenu, sample-data) löschen. In `.csproj` des Web-Projekts doppelte Properties entfernen, die `Directory.Build.props` setzt.

- [ ] **Step 2: Failing Test** – `StartupTests.Home_ShowsGameTitle`: bUnit rendert `Home` und findet ein `h1` mit Text „Developer Dungeon“.
- [ ] **Step 3: Test laufen lassen** – `dotnet test tests/DeveloperDungeon.Web.Tests` → FAIL.
- [ ] **Step 4: Implementieren** – `Home.razor` mit Titel (Inhalt der Startseite folgt in Task 5); `index.html`: `<title>Developer Dungeon</title>`, `lang="de"`, Ladeanzeige „Developer Dungeon lädt …“; `manifest.webmanifest`: `name` „Developer Dungeon“, `short_name` „DevDungeon“, `start_url` „./“, `scope` „./“, `display` „standalone“, `orientation` „any“, Theme-/Hintergrundfarbe aus den Tokens; `MainLayout.razor` ohne Seitenleiste (Vollbild-Spiel); `app.css` mit Tokens, System-Schrift, kräftigen Konturen (Comic-Stil der Stilreferenz `docs/superpowers/specs/assets/2026-11-23-sample-cards.html`).
- [ ] **Step 5: Test laufen lassen** – PASS; zusätzlich `dotnet build` der Solution ohne Warnungen.
- [ ] **Step 6: Commit** – `[ADD] Web-Projekt (Blazor PWA) und Web-Tests`

---

### Task 2: Speichern und Einstellungen

**Files:**
- Create: `src/DeveloperDungeon.Web/Logic/IGameStorage.cs`, `Logic/BrowserGameStorage.cs`, `Logic/GameSettings.cs`, `Logic/SettingsStore.cs`
- Modify: `Program.cs` (DI-Registrierung)
- Create: `tests/DeveloperDungeon.Web.Tests/Support/InMemoryGameStorage.cs`, `tests/.../Logic/BrowserGameStorageTests.cs`, `tests/.../Logic/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public interface IGameStorage { Task<string?> LoadAsync(string key); Task SaveAsync(string key, string value); Task RemoveAsync(string key); }`
  - `public sealed class BrowserGameStorage(IJSRuntime js) : IGameStorage` – nutzt direkt `localStorage.getItem/setItem/removeItem` per `js.InvokeAsync`/`InvokeVoidAsync`; fängt `JSException` beim Lesen ab und liefert `null` (privater Modus / gesperrter Speicher).
  - `public static class StorageKeys { public const string Save = "dd.save.v1"; public const string Settings = "dd.settings.v1"; }`
  - `public sealed record GameSettings(bool AnimationsEnabled = true, int CountdownSeconds = 5)`
  - `public sealed class SettingsStore(IGameStorage storage) { Task<GameSettings> LoadAsync(); Task SaveAsync(GameSettings settings); }` – JSON per `System.Text.Json` mit eigenem `[JsonSerializable(typeof(GameSettings))] partial class WebJsonContext`; ungültiges JSON → Standardwerte; `CountdownSeconds` auf 0…10 geklemmt.
  - Test-Helfer `InMemoryGameStorage : IGameStorage` (Dictionary).

- [ ] **Step 1: Failing Tests**
  - `BrowserGameStorageTests.Load_ReturnsStoredValue` / `Save_CallsSetItem` / `Load_WhenJsThrows_ReturnsNull` – mit bUnit-`JSInterop` (`JSInterop.Setup<string?>("localStorage.getItem", "dd.save.v1").SetResult("x")` usw.; für den Fehlerfall `SetException(new JSException("blocked"))`).
  - `SettingsStoreTests.Load_Empty_ReturnsDefaults` (`AnimationsEnabled == true`, `CountdownSeconds == 5`), `Save_ThenLoad_RoundTrips`, `Load_InvalidJson_ReturnsDefaults`, `Load_ClampsCountdownToTen` (gespeichert 99 → 10).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren**; in `Program.cs` `IGameStorage` → `BrowserGameStorage` (scoped), `SettingsStore` (scoped), `CardCatalog` als Singleton aus `CardCatalog.LoadEmbedded()`.
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Speichern im Browser und Einstellungen`

---

### Task 3: TableController – Spielsteuerung, Gerät übergeben, Aufbau-Runde

**Files:**
- Create: `src/DeveloperDungeon.Web/Logic/TableController.cs`, `Logic/PlayerColors.cs`
- Modify: `Program.cs`
- Create: `tests/DeveloperDungeon.Web.Tests/Logic/TableControllerTests.cs`, `tests/.../Support/TestGames.cs`

**Interfaces:**
- Consumes: `IGameStorage`, `StorageKeys`, Engine-API (siehe Kopf).
- Produces:
  - `public static class PlayerColors { public static IReadOnlyList<(string Hex, string Name)> All { get; } }` – genau 6: `#e07a5f` „Koralle“, `#3d85c6` „Blau“, `#81b29a` „Salbei“, `#f2b134` „Senf“, `#9b5de5` „Lila“, `#f15bb5` „Pink“.
  - `public enum ResumeResult { NoSave, Resumed, Incompatible }`
  - `public sealed class TableController(CardCatalog catalog, IGameStorage storage)`:
    - `bool HasGame`; `GameView View` (Sicht für `RevealedPlayerId`, sonst öffentliche Sicht); `CardCatalog Catalog`
    - `string ActorId` – `InterjectorId ?? (Phase == Setup ? SetupPlayerId : View.ActivePlayerId)`
    - `string? RevealedPlayerId` – wessen Hand gerade sichtbar ist; `string? HandoffTargetId` – wenn gesetzt, zeigt die UI den Gerät-übergeben-Bildschirm für diesen Spieler
    - `string? InterjectorId` – Spieler, der per „Ich spiel was“ gerade eingreift
    - `string? SetupPlayerId` – in der Aufbau-Runde der Spieler, der gerade auslegt
    - `Task StartNewGameAsync(IReadOnlyList<PlayerSetup> players, ulong seed)` – `Game.New`, löscht alten Stand, speichert neuen, Aufbau-Runde beginnt beim Startspieler (`View.ActivePlayerId`), `RequestDevice(SetupPlayerId)`
    - `Task<ResumeResult> TryResumeAsync()` – lädt `dd.save.v1`; `InvalidDataException`/`JsonException` → `Incompatible` (Stand wird **nicht** gelöscht, erst bei neuem Spiel); bei `Setup` beginnt die Aufbau-Runde wieder beim Startspieler; sonst `RequestDevice(active)`
    - `void RequestDevice(string playerId)` → `HandoffTargetId = playerId`, `RevealedPlayerId = null`; `void ConfirmHandoff()` → `RevealedPlayerId = HandoffTargetId`, `HandoffTargetId = null`
    - `Task FinishSetupForCurrentAsync()` – nächster Spieler in Zugreihenfolge ab Startspieler; nach dem letzten → `SendAsync(new StartGame(SetupPlayerId))`, `SetupPlayerId = null`, `RequestDevice(active)`
    - `void BeginInterjection(string playerId)` (nicht der aktive Spieler) → `InterjectorId = playerId`, `RequestDevice(playerId)`; `void EndInterjection()` → `InterjectorId = null`, `RequestDevice(active)`
    - `Task<CommandResult> SendAsync(GameCommand command)` – über die `LocalGameSession`; bei Annahme: speichern (`await storage.SaveAsync(StorageKeys.Save, json)` mit dem JSON, das `onSaved` geliefert hat), `LastEvents` setzen, bei `TurnEnded` im Ereignis → `InterjectorId = null`, `RequestDevice(neuer aktiver Spieler)`; `Changed` auslösen. Bei Ablehnung: `LastRejection` setzen, `Changed` auslösen.
    - `Task<CommandResult> CheckAsync(GameCommand command)`
    - `IReadOnlyList<GameEvent> LastEvents`, `string? LastRejection`, `event Action? Changed`
  - Als scoped Service in `Program.cs`.
  - Test-Helfer `TestGames`: `static TableController NewController(int players = 3, ulong seed = 42)` (mit `InMemoryGameStorage`, echter `LoadEmbedded()`-Katalog), Hilfen zum Durchklicken der Aufbau-Runde.

- [ ] **Step 1: Failing Tests** (`TableControllerTests`)
  - `NewGame_StartsSetupWithHiddenHandAndHandoffToStartPlayer` (`Phase == Setup`, `HandoffTargetId == View.ActivePlayerId`, `View.MyHand == null`).
  - `ConfirmHandoff_RevealsOnlyThatPlayersHand` (`View.MyHand` = Hand dieses Spielers, `HandCount` der anderen unverändert sichtbar).
  - `FinishSetup_ForAllPlayers_StartsGame` (nach N Aufrufen: `Phase == OpenTicket`, `HandoffTargetId == active`).
  - `NewGame_IsSavedToStorage` und `AcceptedCommand_SavesState`, `RejectedCommand_DoesNotSave_AndSetsLastRejection`.
  - `TurnEnded_HidesHandAndRequestsNextPlayer` (Zug bis `EndTurn` treiben; danach `RevealedPlayerId == null`, `HandoffTargetId == neuer aktiver`).
  - `Interjection_RevealsInterjectorAndActsAsInterjector` (`ActorId == interjector`), `EndInterjection_ReturnsDeviceToActive`, `BeginInterjection_ByActivePlayer_IsIgnored`.
  - `Resume_NoSave_ReturnsNoSave`, `Resume_CorruptSave_ReturnsIncompatible_AndKeepsSave`, `Resume_InCombat_HidesHandAndHasNoCountdown` (Kampf herbeiführen, neuen Controller auf demselben Storage → `Resumed`, `RevealedPlayerId == null`, `HandoffTargetId == active`), `Resume_InSetup_ContinuesWithFirstUnfinishedPlayer` (Aufbau-Runde beginnt wieder beim Startspieler).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.** Seed in der UI später per `(ulong)Random.Shared.NextInt64()` (im Web-Projekt ist `System.Random` erlaubt).
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Spielsteuerung mit Gerät-übergeben und Aufbau-Runde`

---

### Task 4: Kartendarstellung, Grafiken, Kartengalerie

**Files:**
- Create: `src/DeveloperDungeon.Web/Components/CardView.razor` (+ `.razor.css`), `Components/CardArt.razor`, `Components/CardZoom.razor`, `Logic/CardArtCatalog.cs`, `Logic/CardTexts.cs`, `Pages/Gallery.razor`
- Create: `src/DeveloperDungeon.Web/wwwroot/cards/art.json`, `wwwroot/cards/_placeholder-monster.svg`, `_placeholder-curse.svg`, `_placeholder-loot.svg`, `_placeholder-ticket.svg`, `wwwroot/cards/monster-works-on-my-machine.svg`, `oneshot-cold-coffee.svg`, `curse-unlocked-pc.svg`
- Create: `tests/DeveloperDungeon.Web.Tests/Components/CardViewTests.cs`, `tests/.../Logic/CardArtCatalogTests.cs`

**Interfaces:**
- Produces:
  - `public static class CardTexts { static string KindLabel(CardKind kind); static string FooterLeft(CardDefinition c); static string FooterRight(CardDefinition c); static string HeaderBadge(CardDefinition c); }` – Kopf/Fuß wie in der Stilreferenz: Monster „Monster“ + „Stufe N“, Fuß „N Stufe(n)“ / „N Loot“; Ausrüstung Slot-Name („Kopf“, „Körper“, „Füße“, „1 Hand“/„2 Hände“, „Groß“, „Klein“) + „+Bonus“, Fuß Wert „N Credits“; Einweg „Loot · Einmalig“; Fluch „Fluch“ + „Sofort“; Abteilung „Abteilung“; Sprache „Sprache“; Stufe-hoch „Stufe hoch“; Verstärker „Verstärker“; Sonder „Sonderkarte“. Singular/Plural korrekt („1 Stufe“, „2 Stufen“).
  - `public sealed class CardArtCatalog { static CardArtCatalog Parse(string json); string UrlFor(CardDefinition c); }` – `art.json` ist ein Objekt `{ "<cardId>": "svg" | "webp" | "png" }`; vorhandener Eintrag → `cards/<id>.<ext>`, sonst Platzhalter nach Art: Monster → `_placeholder-monster.svg`, Fluch → `_placeholder-curse.svg`, übrige Ticket-Karten → `_placeholder-ticket.svg`, Loot → `_placeholder-loot.svg`. Wird per `HttpClient` beim Start geladen (Singleton), Fehler → leerer Katalog (nur Platzhalter).
  - `CardView` Parameter: `CardDefinition Card`, `CardSize Size` (`enum CardSize { Small, Normal, Large }`), `EventCallback OnClick`, `bool Disabled`, `string? DisabledReason` (als `title`/Untertitel); Rahmenfarbe per CSS-Klasse `card--monster|card--curse|card--loot|card--class|card--special`.
  - `CardArt` rendert `<img src="@url" alt="@Card.Name">` (SVG-Animationen laufen eingebettet).
  - `CardZoom` Parameter `CardDefinition? Card`, `EventCallback OnClose` – Vollbild-Overlay mit `Size=Large`.
  - Route `/karten` (`Gallery.razor`): alle Katalogkarten gruppiert nach Stapel und Art, Antippen → `CardZoom`.

Die drei Grafiken werden aus der abgenommenen Stilreferenz `docs/superpowers/specs/assets/2026-11-23-sample-cards.html` übernommen (je das `<svg>` als eigenständige Datei mit eingebettetem `<style>` für die Keyframes, `viewBox="0 0 200 130"`). Platzhalter im gleichen Stil: kräftige Fläche in der Art-Farbe, großes Symbol (Monster: Käfer-Silhouette, Fluch: Totenkopf-Laptop, Loot: Truhe, Ticket: Ticket-Zettel), dezente Endlosanimation.

- [ ] **Step 1: Failing Tests**
  - `CardViewTests.Monster_ShowsLevelBadgeAndRewards` (Karte `monster-works-on-my-machine`: Text „Stufe 3“, „1 Stufe“, „1 Loot“, Klasse `card--monster`), `Equipment_ShowsSlotBonusAndValue` (`equip-visual-studio`: „2 Hände“, „+4“, „800 Credits“), `Curse_HasCurseFrame`, `Disabled_ShowsReason`, `Click_InvokesCallback`.
  - `CardArtCatalogTests.KnownCard_UsesOwnArt`, `UnknownCard_UsesPlaceholderByKind` (Monster/Fluch/Abteilung/Loot), `EveryArtEntry_HasFileOnDisk` (liest `art.json` aus dem Web-Projektordner per relativem Pfad vom Testausgabeordner und prüft Existenz jeder Datei plus der vier Platzhalter), `InvalidJson_YieldsPlaceholdersOnly`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren** (inkl. Galerie-Seite und Grafiken).
- [ ] **Step 4: Tests laufen lassen** – PASS. Kurz manuell: App starten (`preview_start` mit `web`), `/karten` öffnen, Screenshot – Karten haben Rahmen, Bild, Text; die drei Grafiken animieren.
- [ ] **Step 5: Commit** – `[ADD] Kartendarstellung, Grafiken und Kartengalerie`

---

### Task 5: Startseite, Spieler-Setup, Regeln, Einstellungen

**Files:**
- Modify: `src/DeveloperDungeon.Web/Pages/Home.razor`
- Create: `Pages/Setup.razor`, `Logic/SetupForm.cs`, `Pages/Rules.razor`, `Pages/Settings.razor`
- Create: `tests/DeveloperDungeon.Web.Tests/Pages/HomeTests.cs`, `tests/.../Logic/SetupFormTests.cs`, `tests/.../Pages/SettingsTests.cs`

**Interfaces:**
- Consumes: `TableController` (`TryResumeAsync`, `StartNewGameAsync`), `SettingsStore`, `PlayerColors`.
- Produces:
  - Routen: `/` Start, `/neu` Setup, `/regeln`, `/einstellungen`, `/karten` (Task 4), `/spiel` (Task 6).
  - `public sealed class SetupForm { List<SetupRow> Rows; void AddPlayer(); void RemovePlayer(int index); IReadOnlyList<string> Validate(); IReadOnlyList<PlayerSetup> ToPlayers(); }`, `public sealed class SetupRow { string Name; string Color; }` – Start mit 3 Zeilen, Farben vorbelegt aus `PlayerColors` (je eine noch freie); `AddPlayer` nur bis 6, `RemovePlayer` nur über 3; `Validate` liefert deutsche Fehler: „Jeder Spieler braucht einen Namen.“, „Jeder Name darf nur einmal vorkommen.“, „Jede Farbe darf nur einmal vorkommen.“ (Namen getrimmt, Vergleich ohne Groß/Klein).

Startseite: Logo-Titel, Buttons „Neues Spiel“ (→ `/neu`), „Fortsetzen“ (nur bei `ResumeResult.Resumed` → `/spiel`), „Regeln“, „Kartengalerie“, „Einstellungen“. Bei `Incompatible`: Hinweis „Der gespeicherte Spielstand passt nicht mehr zu dieser Version.“ und kein „Fortsetzen“. Regeln-Seite: kurze deutsche Zusammenfassung der Spec-Regeln 5.1–5.7 in eigenen Worten mit der Stufenleiter (`LevelTitles.For(1..10)`). Einstellungen: Schalter „Animationen“ und Zahlenfeld „Countdown (Sekunden)“ 0–10, speichert sofort.

- [ ] **Step 1: Failing Tests**
  - `SetupFormTests.Starts_WithThreeRows_DistinctColors`, `AddPlayer_StopsAtSix`, `RemovePlayer_StopsAtThree`, `Validate_EmptyName`, `Validate_DuplicateNameIgnoringCase`, `Validate_DuplicateColor`, `ToPlayers_TrimsNames`.
  - `HomeTests.NoSave_HidesResume`, `ValidSave_ShowsResume`, `StartPage_CorruptSave_ShowsHintAndOffersNewGameOnly`.
  - `SettingsTests.ChangingCountdown_IsSaved`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.** Setup-Seite: pro Zeile Namensfeld + Farbauswahl (Farbkreise mit deutschem Namen als `title`), „Spieler hinzufügen/entfernen“, „Los geht's“ → `StartNewGameAsync(form.ToPlayers(), (ulong)Random.Shared.NextInt64())` → `/spiel`. Fehler aus `Validate` unter dem Formular.
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Startseite, Spieler-Setup, Regeln und Einstellungen`

---

### Task 6: Spieltisch – Spielerleiste, Stapel, Protokoll, Gerät übergeben, Zugablauf

**Files:**
- Create: `src/DeveloperDungeon.Web/Pages/Table.razor` (`/spiel`, + `.razor.css`), `Components/PlayerBar.razor`, `Components/DeckArea.razor`, `Components/LogPanel.razor`, `Components/HandoffScreen.razor`, `Components/ActionBar.razor`, `Components/Toast.razor`, `Logic/TurnActions.cs`
- Create: `tests/DeveloperDungeon.Web.Tests/Pages/TableTests.cs`, `tests/.../Logic/TurnActionsTests.cs`

**Interfaces:**
- Consumes: `TableController`, `CardView`, `CardZoom`.
- Produces:
  - `public sealed record TurnAction(string Label, GameCommand Command);`
  - `public static class TurnActions { static IReadOnlyList<TurnAction> For(GameView view, string actorId); }` – nur für den aktiven Spieler: `OpenTicket` → „Ticket öffnen“; `AfterTicket` → „Backlog abarbeiten“ (`WorkBacklog`) (Ärger suchen läuft über Monster-Karten in der Hand, Task 7); `EndTurn`-Phase → „Feierabend“ (`EndTurn` mit leerer Liste, wenn Hand ≤ 5; sonst nicht in dieser Liste – siehe Abgabe-Modus).
  - `PlayerBar`: je Spieler Farbkreis mit Initialen, Name, „Stufe N – Titel“, Abteilungen/Sprachen als Kartennamen-Chips, Kampfstärke (`BasePower`), Handkartenanzahl, Markierung des aktiven Spielers; Button „Ich spiel was“ für jeden Nicht-Aktiven → `BeginInterjection`.
  - `DeckArea`: Ticket-/Loot-Stapel mit Anzahl, oberste Ablagekarte als `CardView Small` (antippen → Zoom).
  - `LogPanel`: `RecentLog` neueste oben; auf schmalen Bildschirmen einklappbar.
  - `HandoffScreen` Parameter `string PlayerName`, `string Color`, `EventCallback OnReveal`: Vollbild in Spielerfarbe, „Gib das Gerät an {Name}“, Button „Ich bin {Name} – aufdecken“.
  - `Toast`: zeigt `LastRejection` 4 s lang.
  - Aufbau-Runde: Hinweisband „Aufbau-Runde: {Name} legt Abteilung, Sprache und Ausrüstung aus“, Button „Fertig“ → `FinishSetupForCurrentAsync`.
  - Abgabe-Modus bei Feierabend: Hat der aktive Spieler > 5 Handkarten, zeigt „Feierabend“ die Aufforderung „Wähle N Karten zum Abgeben“; Handkarten werden auswählbar; „Feierabend“ ist erst bei genau N gewählten Karten aktiv und sendet `EndTurn(active, gewählte Ids)`.
  - Sieg: `WinnerId != null` → Vollbild „{Name} ist CTO!“ mit Button „Zur Startseite“.

- [ ] **Step 1: Failing Tests**
  - `TurnActionsTests.OpenTicketPhase_OffersOpenTicket`, `AfterTicket_OffersWorkBacklog`, `NonActiveActor_GetsNoTurnActions`, `EndTurnPhase_WithSmallHand_OffersEndTurn`.
  - `TableTests.Handoff_IsShown_UntilRevealed` (neues Spiel → Handoff-Text mit Startspielername; nach Klick sichtbar: Hand-Bereich), `SetupRound_FinishForAll_ShowsOpenTicket`, `OpenTicket_Click_ShowsLogEntry`, `RejectedCommand_ShowsToast` (Befehl erzwingen, der abgelehnt wird, z. B. `WorkBacklog` in falscher Phase über den Controller → Toast mit Text), `EndTurn_WithSevenCards_RequiresSelectingExactlyTwo` (Hand per Werkzeugkasten-Befehl `ManualTakeFromDiscard`/`ManualMoveCard` auf 7 bringen; Button deaktiviert bei 1 und 3 gewählten, aktiv bei 2), `Winner_ShowsVictoryScreen` (per `ManualAdjustLevel` auf 10).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.** Tisch-Layout quer: oben Spielerleiste, Mitte Stapel + Kampfbereich (Platzhalter bis Task 8), unten Spielerbereich (Task 7), rechts Protokoll; unter 768 px gestapelt, Protokoll einklappbar. `Table.razor` leitet zur Startseite um, wenn `!HasGame`.
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Spieltisch mit Spielerleiste, Protokoll und Zugablauf`

---

### Task 7: Hand und Ausrüstung – Kartenaktionen, Zielauswahl, Verkaufen

**Files:**
- Create: `src/DeveloperDungeon.Web/Logic/CardActionResolver.cs`, `Components/PlayerArea.razor` (+ `.razor.css`), `Components/CardActionMenu.razor`, `Components/TargetPicker.razor`, `Components/SellPanel.razor`
- Modify: `Pages/Table.razor`
- Create: `tests/DeveloperDungeon.Web.Tests/Logic/CardActionResolverTests.cs`, `tests/.../Components/PlayerAreaTests.cs`

**Interfaces:**
- Consumes: `TableController`, `CardView`.
- Produces:
  - `public enum CardLocation { Hand, Equipped, Backpack, Department, Language }`
  - `public enum TargetKind { None, Player, Side, Monster, MonsterFromHand }`
  - `public sealed record CardAction(string Label, TargetKind Target, Func<ActionTarget, GameCommand> Build);`
  - `public sealed record ActionTarget(string? PlayerId = null, Side? Side = null, int? MonsterInstanceId = null);`
  - `public static class CardActionResolver { static IReadOnlyList<CardAction> For(CardDefinition def, CardInstance card, CardLocation location, GameView view, string actorId); }` – Zuordnung (Labels exakt):
    - Hand: Equipment/Department/Language und Special mit `MaxDepartments`/`MaxLanguages` → „Anlegen“ (`EquipCard`); Monster → „Ärger suchen“ (`LookForTrouble`) nur wenn `view.Phase == AfterTicket` und Akteur aktiv; Curse → „Verfluchen …“ (Player, `PlayCard` mit `TargetPlayerId`); OneShot mit `CombatBonus` → „Ausspielen …“ (Side); OneShot ohne `CombatBonus` (AutoEscape/Manual) → „Ausspielen“ (None); MonsterEnhancer → „Auf Monster spielen …“ (Monster, `TargetMonsterInstanceId`); LevelUp → „Ausspielen“; Special mit `AddMonsterToCombat` → „Monster dazuholen …“ (MonsterFromHand, `SecondaryInstanceId`); Special nur mit `Manual` → „Ausspielen“; jede Handkarte zusätzlich → „Abwerfen“ (`DiscardOwnCard`).
    - Equipped: „In den Rucksack“ (`UnequipCard`), „Abwerfen“.
    - Backpack: „Anlegen“, OneShot zusätzlich wie Hand, „Abwerfen“.
    - Department/Language: „Abwerfen“.
  - `PlayerArea` zeigt für den sichtbaren Spieler (`RevealedPlayerId`): Ausrüstungsslots (Kopf, Körper, Füße, Hände, Groß, Klein) mit `CardView Small`, Rucksack, Abteilungen/Sprachen, Hand als horizontal wischbare Leiste. Antippen einer Karte → `CardActionMenu` mit den Aktionen; Aktionen mit `Target == None` werden beim Öffnen per `CheckAsync` geprüft und bei Ablehnung ausgegraut mit Begründung; Aktionen mit Ziel öffnen `TargetPicker` (Spielerliste / „Für mich“-„Für das Monster“ / Monster im Kampf / Monster aus der Hand), danach `SendAsync` (Ablehnung → Toast). „Ansehen“ öffnet `CardZoom`.
  - `SellPanel`: Button „Verkaufen“ im Spielerbereich → Auswahlmodus über Hand/Ausrüstung/Rucksack (nur Karten mit `Value > 0`), Summe „N Credits = M Stufe(n)“ live, „Verkaufen“ sendet `SellItems`.

- [ ] **Step 1: Failing Tests**
  - `CardActionResolverTests`: je ein Test pro Zeile der Zuordnung, z. B. `HandEquipment_OffersEquipAndDiscard`, `HandMonster_InAfterTicket_OffersLookForTrouble`, `HandMonster_InOpenTicket_DoesNotOfferLookForTrouble`, `HandCurse_NeedsPlayerTarget_BuildsPlayCardWithTarget` (Build mit `new ActionTarget(PlayerId: "p2")` → `PlayCard { TargetPlayerId = "p2" }`), `ColdCoffee_NeedsSide`, `RemoteDay_NeedsNoTarget`, `Enhancer_NeedsMonster`, `WanderingBug_NeedsMonsterFromHand`, `Equipped_OffersBackpackAndDiscard`, `Backpack_OneShot_OffersPlay`.
  - `PlayerAreaTests.Curse_ChoosePlayer_SendsCommand` (Fluch per `ManualMoveCard` in die Hand legen, Menü → „Verfluchen …“ → Spieler wählen → Protokoll enthält Fluch-Eintrag), `IllegalAction_IsDisabledWithReason` (Ausrüstung im fremden Zug angeboten → ausgegraut mit Engine-Begründung), `Sell_ShowsLiveSumAndSends` (600 + 400 + 100 → „1100 Credits = 1 Stufe“, nach Verkaufen Stufe +1).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Hand, Ausrüstung, Kartenaktionen und Verkaufen`

---

### Task 8: Kampf – Kampfbereich, Hilfe, Eingreifen, Countdown, Flucht, Auswahlen

**Files:**
- Create: `src/DeveloperDungeon.Web/Logic/CombatCountdown.cs`, `Components/CombatArea.razor` (+ `.razor.css`), `Components/HelpPanel.razor`, `Components/ChoiceDialog.razor`, `Components/ManualEffectDialog.razor`
- Modify: `Logic/TableController.cs`, `Pages/Table.razor`, `Program.cs` (`TimeProvider.System` registrieren)
- Create: `tests/DeveloperDungeon.Web.Tests/Logic/CombatCountdownTests.cs`, `tests/.../Logic/TableControllerCombatTests.cs`, `tests/.../Components/CombatAreaTests.cs`

**Interfaces:**
- Consumes: `TableController`, `SettingsStore`, `TimeProvider`.
- Produces:
  - `public sealed class CombatCountdown(TimeProvider time) : IDisposable { bool IsRunning; int RemainingSeconds; void Start(int seconds); void Cancel(); event Action? Tick; event Action? Elapsed; }` – `Start(0)` löst `Elapsed` sofort aus; tickt jede Sekunde.
  - `TableController` erweitert um: `Task RequestCombatResolutionAsync(int countdownSeconds)` (nur aktiver Spieler; startet Countdown; bei `Elapsed` → `InterjectorId` beenden, `SendAsync(new ResolveCombat(active))`, falls Gerät nicht beim aktiven Spieler: `RequestDevice(active)`), `bool CountdownRunning`, `int CountdownRemaining`; `BeginInterjection` bricht einen laufenden Countdown ab (Entscheidung: Abbrechen statt Zurücksetzen, weil der Eingreifende Zeit für Gerät-übergeben und Kartenwahl braucht; der aktive Spieler drückt danach erneut „Kampf auswerten“). Der Konstruktor wird zu `TableController(CardCatalog catalog, IGameStorage storage, TimeProvider time)` erweitert; `TestGames.NewController` übergibt dann einen `FakeTimeProvider` (bestehende Tests anpassen, nicht duplizieren).
  - `CombatArea`: Monster als `CardView` mit ihren Verstärkern darunter, gespielte Einweg-Karten je Seite, Helfer-Name, großer Stärkevergleich „Du {PlayerStrength} : {MonsterStrength} Monster“ mit Hinweis „Gleichstand gewinnt (QA)“ wenn `PlayerWinsTies`, erwartete Belohnung „{TotalRewardLevels} Stufe(n), {TotalRewardLoot} Loot“; Button „Kampf auswerten“ (aktiver Spieler) → Countdown-Anzeige „Letzte Chance! N“; in `Fleeing`: je fliehendem Spieler Button „{Name} würfelt“ (`AttemptEscape`) und danach Würfelergebnisse aus `LastEvents` (`EscapeRolled` → „{Name} gegen {Monster}: Wurf {Roll} + {Bonus} – entkommen“ bzw. „– erwischt“; bei `Roll == 0` „{Name} entkommt {Monster} ohne Wurf“).
  - `HelpPanel`: für Nicht-Aktive „Hilfe anbieten“ mit Zahl „Forderung (Loot-Karten)“ → `OfferHelp`; für den Aktiven Liste der Angebote mit „Annehmen“ → `AcceptHelp`.
  - Eingreifen-Leiste im Kampf: für jeden Nicht-Aktiven „Moment, ich spiel noch was!“ → `BeginInterjection`; während Eingreifen Banner „{Name} greift ein“ und Button „Fertig“ → `EndInterjection`.
  - `ChoiceDialog`: bei `View.CurrentChoice` modal für den betroffenen Spieler: Text je `ChoiceKind` („{Name}: Wirf ein kleines Item ab“ / „{Name}: Gib ein angelegtes Item an {Empfänger}“), Auswahl aus dessen angelegten Karten (öffentlich, keine Übergabe nötig) → `ResolveChoice(choice.PlayerId, id)`.
  - `ManualEffectDialog`: bei `ManualEffectShown` in `LastEvents` die Karte groß mit `RulesText` und Hinweis „Setzt diese Karte mit dem Werkzeugkasten um.“

- [ ] **Step 1: Failing Tests**
  - `CombatCountdownTests.Start_TicksDownAndElapses` (`FakeTimeProvider.Advance(1s)` × 5 → `Elapsed` genau einmal), `StartZero_ElapsesImmediately`, `Cancel_PreventsElapsed`.
  - `TableControllerCombatTests.RequestResolution_AfterCountdown_ResolvesCombat`, `Interjection_CancelsCountdown`, `ResolvingCombat_WhileInterjecting_ReturnsDeviceToActivePlayer` (Eingreifen ohne „Fertig“, Countdown neu starten und ablaufen lassen → `InterjectorId == null`, `HandoffTargetId == active`), `RequestResolution_ByNonActiveDevice_IsIgnored`.
  - `CombatAreaTests.ShowsStrengthComparison`, `HelpOfferAndAccept_ShowsHelper`, `Fleeing_ShowsRollButtonPerFleeingPlayer`, `Choice_ShowsDialogForAffectedPlayer_AndResolves`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Kampf mit Hilfe, Eingreifen, Countdown und Flucht`

---

### Task 9: Werkzeugkasten

**Files:**
- Create: `src/DeveloperDungeon.Web/Components/ToolboxPanel.razor` (+ `.razor.css`)
- Modify: `Pages/Table.razor`
- Create: `tests/DeveloperDungeon.Web.Tests/Components/ToolboxPanelTests.cs`

**Interfaces:**
- Consumes: `TableController` (`ActorId` ist der Handelnde aller Werkzeugkasten-Befehle).
- Produces: aufklappbares Panel „Werkzeugkasten“ (Schraubenschlüssel-Button, auf allen Bildschirmgrößen erreichbar) mit:
  - „Stufe ändern“: Spieler wählen, −1/+1 → `ManualAdjustLevel`
  - „Karte verschieben“: Karte aus allen öffentlich sichtbaren Zonen (Ausrüstung, Rucksack, Abteilungen, Sprachen aller Spieler, Kampf, oberste Ablagekarten) wählen, Zielspieler und Zielzone („Hand“, „Ausrüstung“, „Rucksack“, „Abteilung“, „Sprache“) → `ManualMoveCard`
  - „Karte abwerfen“ → `ManualDiscard`
  - „Von der Ablage nehmen“ → `ManualTakeFromDiscard` (oberste Karte eines Ablagestapels, Zielspieler)
  - „Kampfstärke anpassen“ (nur im Kampf): Seite „Spieler“/„Monster“, −1/+1 → `ManualCombatAdjust`
  - „Würfeln“ → `RollDie`, Ergebnis aus `DieRolled` groß anzeigen
  - Hinweis oben: „Für Korrekturen und Hausregeln – alles landet im Protokoll.“
  Handkarten anderer Spieler sind im Werkzeugkasten nicht auswählbar (Privatsphäre); die eigene Hand nur, wenn sie aufgedeckt ist.

- [ ] **Step 1: Failing Tests** – `ToolboxPanelTests.AdjustLevel_Plus_RaisesLevel`, `Discard_EquippedCardOfOtherPlayer_Works`, `MoveCard_ToBackpack_Works`, `CombatAdjust_HiddenOutsideCombat`, `RollDie_ShowsResultBetweenOneAndSix`, `OtherPlayersHand_IsNotSelectable`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Werkzeugkasten in der Oberfläche`

---

### Task 10: Animationen

**Files:**
- Create: `src/DeveloperDungeon.Web/Logic/AnimationCues.cs`, `Components/AnimationLayer.razor` (+ `.razor.css`), `wwwroot/css/animations.css`
- Modify: `Pages/Table.razor`, `wwwroot/index.html` (CSS einbinden)
- Create: `tests/DeveloperDungeon.Web.Tests/Logic/AnimationCuesTests.cs`, `tests/.../Components/AnimationLayerTests.cs`

**Interfaces:**
- Consumes: `TableController.LastEvents`, `Changed`, `GameSettings.AnimationsEnabled`.
- Produces:
  - `public enum CueKind { MonsterSlam, ScreenShake, LevelUp, LevelDown, CardFly, EscapeSuccess, EscapeFail, Victory }`
  - `public sealed record AnimationCue(CueKind Kind, string Text, int DurationMs);`
  - `public static class AnimationCues { static IReadOnlyList<AnimationCue> From(IReadOnlyList<GameEvent> events, GameView view, CardCatalog catalog); }` – Zuordnung: `CombatStarted` → MonsterSlam „{Monstername}!“ 900 ms; `CursePlayed` → ScreenShake „Fluch: {Kartenname}“ 700 ms; `LevelChanged` mit `To > From` → LevelUp „{Name}: {Titel der neuen Stufe}“ 1500 ms, `To < From` → LevelDown „{Name}: Stufe {To}“ 900 ms; `CardPlayedInCombat` → CardFly „{Kartenname}“ 600 ms; `EscapeRolled` → EscapeSuccess/EscapeFail „Wurf {Roll}“ 800 ms; `GameWon` → Victory „{Name} ist CTO!“ 1500 ms. Alle Dauern ≤ 1500 ms. Andere Ereignisse → keine Cue.
  - `AnimationLayer`: Overlay mit `pointer-events: none`, spielt Cues nacheinander ab (Konfetti bei LevelUp/Victory per CSS-Partikel, Wackeln per Klasse auf dem Tisch-Container, „Knall“-Skalierung bei MonsterSlam, Flug-Bewegung bei CardFly); bei `AnimationsEnabled == false` nichts; `@media (prefers-reduced-motion: reduce)` schaltet Bewegung in `animations.css` ab (nur Text-Einblendung bleibt).

- [ ] **Step 1: Failing Tests** – `AnimationCuesTests.CombatStarted_GivesMonsterSlamWithName`, `LevelUp_UsesNewTitle` (Stufe 4 → 5: „Senior Developer“), `LevelDown_GivesLevelDown`, `Curse_GivesShake`, `UnrelatedEvent_GivesNoCue`, `AllDurations_AtMost1500`; `AnimationLayerTests.Disabled_RendersNothing`, `Enabled_RendersCueText`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Animationen für Kampf, Flüche und Stufenaufstieg`

---

### Task 11: Deployment auf GitHub Pages

**Files:**
- Create: `.github/workflows/deploy.yml`
- Modify: `src/DeveloperDungeon.Web/wwwroot/index.html` (Base-Href-Platzhalter bleibt `/`), ggf. `wwwroot/service-worker.published.js` nur falls nötig

**Interfaces:**
- Produces: Workflow „Deploy“ – Trigger `push` auf `main` und `workflow_dispatch`; Rechte `contents: read`, `pages: write`, `id-token: write`; `concurrency: pages`. Job `build`: `actions/checkout@v4`, `actions/setup-dotnet@v4` mit `dotnet-version: 10.0.x`, `dotnet test` (gesamte Solution, muss grün sein), Base-Href **vor** dem Publish ersetzen (damit die Integritäts-Hashes des Service Workers stimmen): `sed -i 's|<base href="/" />|<base href="/Developer-Munchkin/" />|' src/DeveloperDungeon.Web/wwwroot/index.html`, `dotnet publish src/DeveloperDungeon.Web -c Release -o publish`, `cp publish/wwwroot/index.html publish/wwwroot/404.html`, `touch publish/wwwroot/.nojekyll`, `actions/upload-pages-artifact@v3` mit `path: publish/wwwroot`. Job `deploy` (needs build): `actions/deploy-pages@v4`, Environment `github-pages`.

- [ ] **Step 1: Lokale Probe des Release-Builds** – dieselben Schritte wie im Workflow lokal in einer Kopie ausführen (Base-Href-Ersetzung auf eine Kopie von `index.html` im Scratch-Ordner anwenden, nicht im Repo committen): `dotnet publish src/DeveloperDungeon.Web -c Release -o <scratch>/publish` → Erwartet: Erfolg ohne Warnungen (Trimming!), `publish/wwwroot/service-worker-assets.js`, `manifest.webmanifest`, `cards/art.json` vorhanden.
- [ ] **Step 2: Workflow schreiben** wie oben.
- [ ] **Step 3: Workflow-Syntax prüfen** – YAML parsebar (z. B. `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/deploy.yml'))"` falls Python vorhanden, sonst sorgfältiges Gegenlesen), alle Pfade existieren.
- [ ] **Step 4: Commit** – `[ADD] Deployment auf GitHub Pages`. Nicht pushen – Push und das Aktivieren von Pages (Repo → Settings → Pages → Source „GitHub Actions“; Pages braucht ein öffentliches Repo oder einen bezahlten Plan) entscheidet der Nutzer.

---

### Task 12: Ende-zu-Ende-Durchlauf im Browser

**Files:** keine neuen (nur Fixes, falls etwas auffällt – jeweils mit Test im passenden Task-Testfile)

- [ ] **Step 1: App starten** – `preview_start` mit `web` (`.claude/launch.json` aus Task 1).
- [ ] **Step 2: Durchspielen** – 3 Spieler anlegen; Aufbau-Runde mit Gerät-übergeben für alle; Ticket öffnen; einen Kampf bis zur Auswertung mit Countdown spielen (einmal eingreifen mit „Moment, ich spiel noch was!“ und Einweg-Karte, sofern eine auf der Hand ist – sonst per Werkzeugkasten eine nehmen); Flucht würfeln; Feierabend; nächster Spieler bekommt Gerät-übergeben. Dabei `read_console_messages` (onlyErrors) prüfen – Erwartet: keine Fehler.
- [ ] **Step 3: Neu laden** – Seite neu laden → Startseite zeigt „Fortsetzen“ → Spiel geht mit verdeckter Hand weiter.
- [ ] **Step 4: Smartphone-Breite** – `resize_window` `mobile` → kein horizontales Scrollen der Seite (`document.documentElement.scrollWidth <= innerWidth` per `javascript_tool`), Hand wischbar, Protokoll und Werkzeugkasten erreichbar; danach `resize_window` `desktop`.
- [ ] **Step 5: Kartengalerie und Einstellungen** – `/karten` zeigt alle Karten; Animationen in den Einstellungen ausschalten → beim nächsten Stufenaufstieg keine Animation.
- [ ] **Step 6:** Gefundene Fehler beheben (mit Test), `dotnet test` grün, Commit `[FIX] …` je Fehler. Ergebnis des Durchlaufs (inkl. Screenshots-Beschreibung) im Bericht festhalten.
