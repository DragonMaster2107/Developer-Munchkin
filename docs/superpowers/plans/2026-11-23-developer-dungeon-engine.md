# Developer Dungeon – Engine Implementation Plan (Plan 1 von 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die komplette Spiellogik von Developer Dungeon als reine, getestete C#-Bibliothek – inklusive Kartendatenformat, Startkarten und Spielsitzungs-Schnittstelle –, sodass Plan 2 (Blazor-UI) nur noch darauf aufsetzt.

**Architecture:** `DeveloperDungeon.Engine` hält einen vollständig serialisierbaren `GameState`. Die UI schickt `GameCommand`s an `Game.Execute`, das jeden Befehl erst vollständig prüft und erst dann anwendet (abgelehnte Befehle ändern nichts) und `GameEvent`s zurückgibt. Karten sind JSON-Daten mit einem festen Wirkungskatalog; Zufall läuft über eine eigene, im Zustand gespeicherte RNG.

**Tech Stack:** .NET 10 (`net10.0`), C# latest, System.Text.Json mit Source Generation, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-23-developer-dungeon-design.md`

**Folgepläne (nicht Teil dieses Plans):** Plan 2 – Blazor-WASM-PWA (Spieltisch, Gerät-übergeben, Werkzeugkasten-UI, Speichern in localStorage, Animationen, GitHub-Pages-Deployment). Plan 3 – Kartenpool ~90 Karten mit Texten + SVG-Grafiken + Balancing (ersetzt/erweitert die Startkarten aus Task 12).

## Global Constraints

- Ziel-Framework `net10.0`; `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` true (in `Directory.Build.props`).
- `DeveloperDungeon.Engine` hat **keine** Paketabhängigkeiten außer der BCL (kein Blazor, kein ASP.NET).
- Code-Bezeichner Englisch, alle für Menschen sichtbaren Texte (Ablehnungsgründe, Log-Einträge, Stufentitel) Deutsch.
- Wirkungskatalog der Spec ↔ Code: Kampfbonus=`CombatBonus`, KampfbonusGegen=`ConditionalCombatBonus`, VerliereStufe=`LoseLevels`, ErhalteStufe=`GainLevels`, VerliereSlot=`LoseSlot`, VerliereKleinesItem=`LoseSmallItem`, VerliereHandkarten=`DiscardHand`, GibItemAnNachbarn=`GiveEquippedItemToNeighbor`, ZieheKarten=`DrawCards`, FluchtBonus=`EscapeBonus`, AutoFlucht=`AutoEscape`, GleichstandGewinnt=`TiesWin`, HilfeBringtStufe=`HelpingGrantsLevel`, MaxAbteilungen=`MaxDepartments`, MaxSprachen=`MaxLanguages`, NaechsteRundeMalus=`NextTurnPenalty`, Manuell=`Manual`; zusätzlich `MonsterModifier`, `CurseImmunity`, `AddMonsterToCombat`.
- Spielregel-Zahlen (exakt): 3–6 Spieler; Start Stufe 1 mit 4 Ticket- + 4 Loot-Karten; Sieg bei Stufe 10, Stufe 10 nur durch gewonnenen Kampf; Stufe nie < 1; Kampfsieg nur bei Spielerstärke **>** Monsterstärke (außer `TiesWin`); Flucht mit W6 bei **5–6** (+`EscapeBonus`); Handlimit **5**; Verkauf **je volle 1000 Credits = 1 Stufe**.
- Abgelehnte Befehle verändern den Zustand nicht (Prüfen-dann-Anwenden). Ausnahme-frei: Regelverstöße sind `CommandResult.Rejected(grund)`, keine Exceptions.
- Werkzeugkasten-Befehle (`Manual*`, `RollDie`) sind nicht regelgeprüft, nur auf Existenz von Spieler/Karte.
- Jeder Befehl trägt `ActorId` (Spieler-Id `p1`…`p6`).
- Stapel-Konvention: Index 0 einer Kartenliste ist die oberste Karte.
- „Links“ = nächster Spieler in Zugreihenfolge (`(index + 1) % n`).

## Review Focus

1. **Beide Stapel leer** (Ticket- oder Loot-Stapel und dessen Ablage leer): Ziehen liefert weniger/keine Karten, kein Absturz, Spiel läuft weiter. → Test in Task 3 (`Draw_BothEmpty_ReturnsNull`) und Task 6 (`OpenTicket_WithEmptyTicketDecks_GoesToAfterTicket`).
2. **Auswahl-Wirkung ohne gültige Option** (z. B. „gib ein getragenes Item ab“, aber nichts getragen): Wirkung verpufft, kein hängender `PendingChoice`. → Task 5 (`GiveItemCurse_WithNothingEquipped_Fizzles`).
3. **Speichern/Laden mitten im Kampf mit offener Auswahl**: nach `Load` liefern dieselben Befehle exakt dieselben Ergebnisse (inkl. Würfel). → Task 9 (`SaveLoad_MidFleeing_ContinuesIdentically`).
4. **Befehle vom falschen Spieler oder nach Spielende**: abgelehnt, Zustand byte-gleich. → Task 4 (`Rejected_Command_DoesNotChangeState`, `AnyCommand_AfterGameOver_IsRejected`).
5. **Stufen-Grenzen**: Boss mit 2 Stufen auf Stufe 9 → 10 und Spielende (nicht 11); `LoseLevels(2)` auf Stufe 2 → 1; Verkauf/Stufe-hoch-Karte auf Stufe 9 abgelehnt. → Task 7, Task 5, Task 10.

Zusätzlich deckt die Simulation in Task 12 Deadlocks auf (Zustände, in denen kein Befehl mehr angenommen wird).

---

### Task 1: Solution-Gerüst und deterministische Zufallsquelle

**Files:**
- Create: `DeveloperDungeon.slnx` (bzw. `.sln`, je nachdem was `dotnet new sln` in .NET 10 erzeugt), `Directory.Build.props`
- Create: `src/DeveloperDungeon.Engine/DeveloperDungeon.Engine.csproj`, `src/DeveloperDungeon.Engine/Random/GameRandom.cs`
- Create: `tests/DeveloperDungeon.Engine.Tests/DeveloperDungeon.Engine.Tests.csproj`, `tests/DeveloperDungeon.Engine.Tests/Random/GameRandomTests.cs`

**Interfaces:**
- Produces: `public sealed class GameRandom { public GameRandom(ulong state); public ulong State { get; } public int Next(int maxExclusive); public int RollD6(); public void Shuffle<T>(IList<T> list); }`

- [ ] **Step 1: Gerüst anlegen**

```powershell
dotnet new sln -n DeveloperDungeon
dotnet new classlib -n DeveloperDungeon.Engine -o src/DeveloperDungeon.Engine -f net10.0
dotnet new xunit -n DeveloperDungeon.Engine.Tests -o tests/DeveloperDungeon.Engine.Tests -f net10.0
dotnet sln add src/DeveloperDungeon.Engine tests/DeveloperDungeon.Engine.Tests
dotnet add tests/DeveloperDungeon.Engine.Tests reference src/DeveloperDungeon.Engine
```
`Class1.cs`/`UnitTest1.cs` löschen. `Directory.Build.props` mit `Nullable`, `ImplicitUsings`, `LangVersion latest`, `TreatWarningsAsErrors`. In der Engine-csproj `<InternalsVisibleTo Include="DeveloperDungeon.Engine.Tests" />`.

- [ ] **Step 2: Failing Tests schreiben** (`GameRandomTests`)

```csharp
[Fact] public void SameSeed_ProducesSameSequence() { var a = new GameRandom(42); var b = new GameRandom(42);
  Assert.Equal(Enumerable.Range(0,20).Select(_ => a.Next(100)), Enumerable.Range(0,20).Select(_ => b.Next(100))); }
[Fact] public void RestoredFromState_ContinuesIdentically() { var a = new GameRandom(7); a.Next(10); var b = new GameRandom(a.State);
  Assert.Equal(a.Next(1000), b.Next(1000)); }
[Fact] public void RollD6_StaysInRange_AndHitsAllFaces() { var r = new GameRandom(1); var rolls = Enumerable.Range(0,1000).Select(_ => r.RollD6()).ToList();
  Assert.All(rolls, x => Assert.InRange(x, 1, 6)); Assert.Equal(6, rolls.Distinct().Count()); }
[Fact] public void Shuffle_IsDeterministicPermutation() { var l1 = Enumerable.Range(0,30).ToList(); var l2 = l1.ToList();
  new GameRandom(5).Shuffle(l1); new GameRandom(5).Shuffle(l2);
  Assert.Equal(l1, l2); Assert.NotEqual(Enumerable.Range(0,30), l1); Assert.Equal(Enumerable.Range(0,30), l1.Order()); }
```

- [ ] **Step 3: Tests laufen lassen** – `dotnet test` → FAIL (GameRandom fehlt).
- [ ] **Step 4: `GameRandom` implementieren** – SplitMix64 auf `State`; `Next` per Ablehnungs-Sampling ohne Modulo-Bias; `Shuffle` = Fisher-Yates.
- [ ] **Step 5: Tests laufen lassen** – `dotnet test` → PASS.
- [ ] **Step 6: Commit** – `git add -A; git commit -m "[ADD] Engine-Gerüst und deterministische Zufallsquelle"`

---

### Task 2: Kartenmodell, Wirkungskatalog, Katalog-Laden und -Validierung

**Files:**
- Create: `src/DeveloperDungeon.Engine/Cards/CardEnums.cs`, `Cards/Effects.cs`, `Cards/CardDefinition.cs`, `Cards/CardCatalog.cs`, `Serialization/EngineJsonContext.cs`
- Create: `tests/DeveloperDungeon.Engine.Tests/Fixtures/TestCards.json` (CopyToOutputDirectory), `tests/.../Support/TestData.cs`, `tests/.../Cards/CardCatalogTests.cs`

**Interfaces:**
- Produces:
  - `enum Deck { Ticket, Loot }`, `enum CardKind { Monster, MonsterEnhancer, Curse, Department, Language, Special, Equipment, OneShot, LevelUp }`, `enum Slot { None, Head, Body, Feet, Hands, Big }`, `enum Side { Player, Monster }`, `enum Direction { Left, Right }`
  - Abstrakter `record Effect` mit `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` und abgeleiteten Records (Diskriminator = Name):
    `CombatBonus(int Amount)`, `ConditionalCombatBonus(int Amount, string? MonsterTag, string? PlayerDepartment, string? PlayerLanguage)`, `LoseLevels(int Amount)`, `GainLevels(int Amount)`, `LoseSlot(Slot Slot)`, `LoseSmallItem()`, `DiscardHand(int? Count)` (null = alle), `GiveEquippedItemToNeighbor(Direction Direction)`, `DrawCards(Deck Deck, int Count)`, `EscapeBonus(int Amount)`, `AutoEscape()`, `TiesWin()`, `HelpingGrantsLevel()`, `MaxDepartments(int Max)`, `MaxLanguages(int Max)`, `NextTurnPenalty(int Amount)`, `MonsterModifier(int Levels, int ExtraLoot)`, `CurseImmunity(string CurseTag)`, `AddMonsterToCombat()`, `Manual()`
  - `sealed record CardDefinition` mit `init`-Properties: `string Id, string Name, Deck Deck, CardKind Kind, string Flavor, string RulesText, int Count = 1, IReadOnlyList<Effect> Effects = []`, `IReadOnlyList<string> Tags = []`, Monster: `int? Level, int RewardLevels, int RewardLoot, IReadOnlyList<Effect> BadStuff = [], int? NoPursuitBelowLevel`, Ausrüstung: `Slot Slot, int Hands, int Bonus, int Value, string? RequiresDepartment, string? RequiresLanguage`, Abteilung/Sprache: `string? Key`
  - `sealed class CardCatalog { static CardCatalog FromJson(params string[] jsonArrays); static CardCatalog LoadEmbedded(); CardDefinition Get(string id) /*KeyNotFoundException*/; IReadOnlyList<CardDefinition> All { get; } IReadOnlyList<string> Validate(); }`
  - `[JsonSerializable(typeof(List<CardDefinition>))] internal partial class EngineJsonContext : JsonSerializerContext` (in Task 3 um `GameState` erweitert); Enums als Strings (`JsonStringEnumConverter`), camelCase.
  - Test-Helfer: `static class TestData { static CardCatalog Catalog { get; } }` lädt `Fixtures/TestCards.json`.

Kartendatei-Format (Beispiel, Array von Karten):
```json
[{ "id": "monster-l3-legacy", "name": "Test Legacy", "deck": "ticket", "kind": "monster", "flavor": "x", "rulesText": "x",
   "level": 3, "rewardLevels": 1, "rewardLoot": 1, "tags": ["Legacy"], "count": 3,
   "badStuff": [{ "type": "LoseSlot", "slot": "head" }] }]
```

**Fixture-Inhalt `TestCards.json`** (Ids exakt so, von allen späteren Tasks benutzt; Counts so, dass jeder Stapel ≥ 40 Karten hat):
- Ticket: `monster-l1` (L1, 1/1, BadStuff `LoseLevels(1)`, count 10), `monster-l3-legacy` (L3, 1/1, Tags `Legacy`, BadStuff `LoseSlot(Head)`, count 6), `monster-l8-hates-qa` (L8, 1/2, Effects `ConditionalCombatBonus(2, playerDepartment:"qa")`, BadStuff `LoseSmallItem`, count 4), `monster-boss-l12` (L12, 2/3, Tags `Meeting`, `NoPursuitBelowLevel` 3, BadStuff `LoseLevels(2)`, count 2), `enhancer-plus5` (`MonsterModifier(5,1)`, count 3), `curse-lose-level` (`LoseLevels(1)`, count 3), `curse-give-item` (Tags `Security`, `GiveEquippedItemToNeighbor(Left)`, count 3), `curse-lose-head` (`LoseSlot(Head)`, count 2), `curse-discard-two` (`DiscardHand(2)`, count 2), `curse-penalty` (`NextTurnPenalty(2)`, count 2), `curse-manual` (`Manual`, count 1), `dept-qa` (Key `qa`, `TiesWin`), `dept-team-assistant` (Key `team-assistant`, `CurseImmunity("Security")`, `HelpingGrantsLevel`), `dept-cloud` (Key `cloud-platform`, `EscapeBonus(1)`), `lang-csharp` (Key `csharp`, `ConditionalCombatBonus(1, monsterTag:"Legacy")`), `lang-python` (Key `python`, `CombatBonus(1)`), `special-full-stack` (`MaxDepartments(2)`), `special-polyglot` (`MaxLanguages(2)`), `special-wandering-bug` (`AddMonsterToCombat`, count 2)
- Loot: `equip-head` (Head, +1, 200, count 3), `equip-feet` (Feet, +1, 400, count 3), `equip-body` (Body, +2, 300, count 3), `equip-ide-1h` (Hands, 1 Hand, +2, 300, count 4), `equip-ide-2h` (Hands, 2 Hände, +3, 600, count 2), `equip-big` (Big, +3, 500, count 2), `equip-qa-only` (Hands, 1 Hand, +4, 500, RequiresDepartment `qa`, count 2), `equip-small` (Slot None, +1, 100, count 4), `oneshot-plus2` (`CombatBonus(2)`, `NextTurnPenalty(1)`, Value 100, count 6), `oneshot-plus5` (`CombatBonus(5)`, Value 300, count 3), `oneshot-remote` (`AutoEscape`, Value 200, count 3), `levelup-raise` (`GainLevels(1)`, count 4), `loot-manual` (OneShot, `Manual`, count 1)

- [ ] **Step 1: Failing Tests schreiben** (`CardCatalogTests`)
  - `FixtureCatalog_LoadsAndValidatesClean`: `Assert.Empty(TestData.Catalog.Validate())`; `Get("monster-l3-legacy").Level == 3`; `BadStuff[0]` ist `LoseSlot { Slot: Slot.Head }`.
  - `Get_UnknownId_Throws`: `Assert.Throws<KeyNotFoundException>(() => TestData.Catalog.Get("nope"))`.
  - `Validate_ReportsDuplicateIds`, `Validate_ReportsMonsterWithoutLevel`, `Validate_ReportsHandsMismatch` (Slot Hands mit Hands 0 oder 3 → Fehler; Slot ≠ Hands mit Hands > 0 → Fehler), `Validate_ReportsCountBelowOne`, `Validate_ReportsDepartmentWithoutKey`, `Validate_ReportsWrongDeckForKind` (Monster im Loot-Stapel) – jeweils mit kleinem Inline-JSON; die Fehlermeldung enthält die Karten-Id. (Equipment mit Slot None = kleines Item, gültig.)
  - `UnknownEffectType_ThrowsJsonException`.
- [ ] **Step 2: Tests laufen lassen** – FAIL (Typen fehlen).
- [ ] **Step 3: Typen und `CardCatalog` implementieren.** `LoadEmbedded` liest alle Manifest-Ressourcen der Engine-Assembly, die auf `.json` enden und unter `Cards/Data/` liegen (`<EmbeddedResource Include="Cards\Data\*.json" />`; der Ordner bleibt bis Task 12 leer bzw. ohne Dateien). Die Prüfung „Grafik vorhanden“ aus der Spec gehört nicht hierher (Grafiken liegen im Web-Projekt) und kommt in Plan 3. Validierungsregeln: eindeutige Ids; Monster brauchen `Level ≥ 1`, `RewardLoot ≥ 0`; Department/Language brauchen `Key`; Hands-Regel wie oben; `Count ≥ 1`; `Deck` passt zur Kind (Monster/Enhancer/Curse/Department/Language → Ticket; Equipment/OneShot/LevelUp → Loot; Special beides).
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Kartenmodell, Wirkungskatalog und Katalog-Validierung`

---

### Task 3: Spielzustand, neues Spiel, Ziehen, Speichern/Laden, Test-Builder

**Files:**
- Create: `src/DeveloperDungeon.Engine/State/GameState.cs`, `State/PlayerState.cs`, `State/CardInstance.cs`, `State/CombatState.cs`, `State/PendingChoice.cs`, `State/TemporaryEffect.cs`, `State/LogEntry.cs`, `State/GamePhase.cs`, `Game.cs`, `Rules/Decks.cs`
- Modify: `Serialization/EngineJsonContext.cs` (+ `GameState`)
- Create: `tests/.../Support/GameBuilder.cs`, `tests/.../GameSetupTests.cs`, `tests/.../DeckTests.cs`, `tests/.../SaveLoadTests.cs`

**Interfaces:**
- Produces (veränderliche Klassen mit öffentlichen get/set, damit Source-Gen-JSON funktioniert):
  - `enum GamePhase { Setup, OpenTicket, AfterTicket, Combat, EndTurn, GameOver }`
  - `class CardInstance { int InstanceId; string CardId; }`
  - `class PlayerState { string Id; string Name; string Color; int Level = 1; List<CardInstance> Hand, Equipped, Backpack, Departments, Languages; }`
  - `class TemporaryEffect { string PlayerId; int CombatModifier; int StartsAfterTurnNumber; string SourceCardId; }` – aktiv, wenn `state.TurnNumber > StartsAfterTurnNumber`; wird am Ende des eigenen Zuges dieses Spielers entfernt, sofern aktiv.
  - `enum ChoiceKind { DiscardSmallItem, GiveEquippedItem }`, `class PendingChoice { string PlayerId; ChoiceKind Kind; string? RecipientPlayerId; string SourceCardId; }`
  - `enum CombatStage { Fighting, Fleeing }`, `class OneShotPlay { CardInstance Card; Side Side; string PlayedBy; }`, `class HelpOffer { string PlayerId; int LootShare; }`, `class FleeingPlayer { string PlayerId; List<int> UnresolvedMonsterIds; }`
  - `class CombatState { CombatStage Stage; List<CardInstance> Monsters; Dictionary<int, List<CardInstance>> Enhancers /*key = Monster-InstanceId*/; List<OneShotPlay> OneShots; List<CardInstance> OtherPlayed /*z. B. Wandernder Bug*/; string? HelperId; int HelperLootShare; List<HelpOffer> Offers; int ManualPlayerAdjust; int ManualMonsterAdjust; List<FleeingPlayer> Fleeing; }`
  - `class LogEntry { int Seq; string Text; }`
  - `class GameState { GamePhase Phase; List<PlayerState> Players; int ActivePlayerIndex; int TurnNumber; List<CardInstance> TicketDeck, TicketDiscard, LootDeck, LootDiscard; CombatState? Combat; List<PendingChoice> PendingChoices; List<TemporaryEffect> TemporaryEffects; ulong RngState; string? WinnerId; List<LogEntry> Log; }`
  - `record PlayerSetup(string Name, string Color)`
  - `sealed class Game { static Game New(IReadOnlyList<PlayerSetup> players, CardCatalog catalog, ulong seed); static Game Load(string json, CardCatalog catalog); string Save(); GameState State { get; } CardCatalog Catalog { get; } }`
  - `internal static class Decks { static CardInstance? Draw(GameState s, GameRandom rng, Deck deck); static void Discard(GameState s, CardCatalog c, CardInstance card); }` – Ablage nach `catalog.Get(card.CardId).Deck`; `Draw` mischt die Ablage in den leeren Stapel, sind beide leer → `null`.
  - Interne Zufallsnutzung: `Game` hält eine `GameRandom`, deren `State` nach jeder Nutzung in `GameState.RngState` zurückgeschrieben wird (bzw. `GameRandom` wird jedes Mal aus `RngState` erzeugt) – entscheidend ist: `Save`/`Load` setzt die Zufallsfolge exakt fort.
  - Test-Builder: `sealed class GameBuilder { static GameBuilder Create(int players = 3, ulong seed = 42); GameBuilder Hand(int p, params string[] cardIds); GameBuilder Equip(int p, params string[] cardIds); GameBuilder Backpack(int p, params string[] ids); GameBuilder Department(int p, string id); GameBuilder Language(int p, string id); GameBuilder TicketTop(params string[] ids); GameBuilder LootTop(params string[] ids); GameBuilder Level(int p, int level); GameBuilder Active(int p); GameBuilder Phase(GamePhase phase); GameBuilder ClearHands(); Game Build(); }` – erzeugt per `Game.New` mit `TestData.Catalog`, verschiebt dann Instanzen der genannten Karten-Ids aus Stapeln/Ablagen direkt in die Zielzone (wirft, wenn keine Instanz mehr frei ist). `Phase(...)` setzt die Phase direkt; ohne Aufruf bleibt `Setup`.

- [ ] **Step 1: Failing Tests schreiben**
  - `GameSetupTests.New_RejectsTooFewOrTooManyPlayers` (2 und 7 → `ArgumentException`).
  - `New_DealsFourTicketAndFourLootToEachPlayer_AtLevelOne` (für 3 und 6 Spieler; Ids `p1`…`pN`; Phase `Setup`; `TurnNumber == 0`).
  - `New_IsDeterministicForSeed` (`Game.New(..., 9).Save() == Game.New(..., 9).Save()`; Seed 10 ≠).
  - `New_ExpandsCardCountsIntoUniqueInstances` (Summe aller Instanzen über alle Zonen = Summe `Count` im Katalog; `InstanceId`s eindeutig).
  - `DeckTests.Draw_TakesTopCard`, `Draw_EmptyDeck_ReshufflesDiscard`, `Draw_BothEmpty_ReturnsNull`.
  - `SaveLoadTests.Roundtrip_ProducesIdenticalJson`, `Load_ContinuesRandomSequence` (nach Load liefert ein Reshuffle dieselbe Reihenfolge wie ohne Save/Load).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Zustandsklassen, `Game.New/Load/Save`, `Decks`, `GameBuilder` implementieren.** `New`: Stapel aus Katalog expandieren (fortlaufende InstanceIds ab 1), getrennt nach Deck mischen, je Spieler 4 Ticket + 4 Loot ziehen, Startspieler `rng.Next(n)`, Log-Eintrag „Neues Spiel mit … Spielern“.
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Spielzustand, Spielstart und Speichern/Laden`

---

### Task 4: Befehls-Infrastruktur, Aufbau-Phase, Ausrüsten

**Files:**
- Create: `src/DeveloperDungeon.Engine/Commands/GameCommand.cs`, `Commands/CommandResult.cs`, `Events/GameEvent.cs`, `Rules/CommandGuard.cs`, `Rules/PassiveEffects.cs`, `Rules/EquipRules.cs`
- Modify: `Game.cs` (`Execute`)
- Create: `tests/.../CommandGuardTests.cs`, `tests/.../EquipRulesTests.cs`

**Interfaces:**
- Consumes: `GameState`, `CardCatalog`, `GameBuilder` (Task 3).
- Produces:
  - `abstract record GameCommand(string ActorId)`; hier: `StartGame(string ActorId)`, `EquipCard(string ActorId, int InstanceId)`, `UnequipCard(string ActorId, int InstanceId)`, `DiscardOwnCard(string ActorId, int InstanceId)`.
  - `sealed record CommandResult(bool Accepted, string? Rejection, IReadOnlyList<GameEvent> Events) { static CommandResult Ok(IReadOnlyList<GameEvent> e); static CommandResult Rejected(string reason); }`
  - `abstract record GameEvent`; hier: `GameStarted(string StartPlayerId)`, `CardEquipped(string PlayerId, int InstanceId)`, `CardUnequipped(...)`, `CardDiscarded(string PlayerId, int InstanceId)`. Jeder angenommene Befehl schreibt mind. einen deutschen `LogEntry`.
  - `public CommandResult Execute(GameCommand command)` auf `Game` – zentraler `switch` auf den Befehlstyp, delegiert an statische Regelklassen `XyzRules.Handle(GameState, CardCatalog, GameRandom, cmd) : CommandResult`.
  - `static class CommandGuard`: gemeinsame Vorprüfungen in dieser Reihenfolge: Spieler existiert („Unbekannter Spieler“); `Phase != GameOver` („Das Spiel ist vorbei.“); wenn `PendingChoices` nicht leer, sind nur `ResolveChoice` (Task 5) und Werkzeugkasten-Befehle (Task 10) erlaubt („Erst muss {Name} eine Auswahl treffen.“).
  - `static class PassiveEffects { static IEnumerable<Effect> Of(GameState s, CardCatalog c, string playerId); static int MaxDepartments(...); static int MaxLanguages(...); }` – sammelt `Effects` aller Karten in `Equipped`, `Departments`, `Languages` (nicht Hand/Rucksack). `MaxDepartments` = max(1, größtes `MaxDepartments.Max`), analog Sprachen.
  - Helfer `CommandGuard.IsOwnTurnOutsideCombat(GameState s, string actorId) : bool` = `actorId` ist aktiv und Phase ∈ {OpenTicket, AfterTicket, EndTurn}.

Ausrüst-Regeln (`EquipCard`): erlaubt in `Setup` (jeder Spieler) oder `IsOwnTurnOutsideCombat`. Karte aus eigener Hand oder eigenem Rucksack. Nach Kind:
- `Equipment`: Slot Head/Body/Feet/Big je max. 1 getragen; Hands: Summe `Hands` getragener Hand-Items + neue ≤ 2; Slot None unbegrenzt; `RequiresDepartment`/`RequiresLanguage` muss zu einem getragenen `Key` passen → Ablehnung z. B. „Nur für qa. Du hast diese Abteilung nicht.“ (Abteilungs-Anzeigename = Kartenname der Abteilung, wenn im Katalog per Key auffindbar).
- `Department`/`Language`: Anzahl < `MaxDepartments`/`MaxLanguages` → in `Departments`/`Languages`.
- `Special` nur, wenn sie `MaxDepartments` oder `MaxLanguages` enthält → `Equipped`.
- alle anderen Kinds → Ablehnung „Diese Karte kann man nicht anlegen.“
`UnequipCard`: nur `Equipped` → `Backpack`; abgelehnt, wenn danach mehr Abteilungen/Sprachen liegen als erlaubt. `DiscardOwnCard`: aus Hand/Equipped/Backpack/Departments/Languages in die passende Ablage; gleiche Zeitregel wie Ausrüsten; gleiche Limit-Prüfung. `StartGame`: nur in `Setup` → `Phase = OpenTicket`, `TurnNumber = 1`.

- [ ] **Step 1: Failing Tests schreiben**
  - `CommandGuardTests.UnknownActor_IsRejected`, `AnyCommand_AfterGameOver_IsRejected`, `Rejected_Command_DoesNotChangeState` (`var before = game.Save(); game.Execute(rejected); Assert.Equal(before, game.Save());`), `StartGame_MovesToOpenTicket_TurnOne`, `StartGame_OutsideSetup_IsRejected`.
  - `EquipRulesTests`: `Equip_Head_Twice_IsRejected`, `Equip_TwoOneHandedIdes_Allowed_ThirdRejected`, `Equip_TwoHandedPlusOneHanded_IsRejected`, `Equip_QaOnly_WithoutQa_IsRejected_WithQa_Allowed`, `Equip_SecondDepartment_RejectedUnlessFullStack`, `Equip_SecondLanguage_RejectedUnlessPolyglot`, `Equip_InOtherPlayersTurn_IsRejected`, `Equip_DuringCombat_IsRejected` (Builder `Phase(GamePhase.Combat)`), `Equip_InSetup_AllowedForAnyPlayer`, `Equip_OneShot_IsRejected`, `Unequip_MovesToBackpack_AndBackpackDoesNotCount` (`PassiveEffects.Of` enthält danach dessen Effekte nicht mehr), `Unequip_FullStack_WithTwoDepartments_IsRejected`, `DiscardOwnCard_PutsCardOnMatchingDiscardPile`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren** wie oben beschrieben.
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Befehlsverarbeitung und Ausrüst-Regeln`

---

### Task 5: Wirkungen anwenden, Flüche, Auswahlen, temporäre Effekte

**Files:**
- Create: `src/DeveloperDungeon.Engine/Rules/EffectApplier.cs`, `Rules/Curses.cs`, `Rules/TemporaryEffects.cs`, `Rules/CardPlayRules.cs`, `Rules/ChoiceRules.cs`
- Modify: `Commands/GameCommand.cs`, `Events/GameEvent.cs`, `Game.cs`
- Create: `tests/.../CurseTests.cs`, `tests/.../ChoiceTests.cs`

**Interfaces:**
- Consumes: Task 3/4.
- Produces:
  - `PlayCard(string ActorId, int InstanceId, string? TargetPlayerId = null, int? TargetMonsterInstanceId = null, Side? Side = null, int? SecondaryInstanceId = null)` – ein Befehl für alle ausgespielten Karten; `CardPlayRules.Handle` verzweigt nach Kind (Tasks 8 und 10 ergänzen weitere Zweige).
  - `ResolveChoice(string ActorId, int InstanceId)`
  - `internal static class EffectApplier { static void ApplyToPlayer(GameState s, CardCatalog c, GameRandom rng, string targetPlayerId, IEnumerable<Effect> effects, string sourceCardId, List<GameEvent> events); }` – behandelt: `LoseLevels` (min. 1), `GainLevels` (max. 9 – Stufe 10 nie per Wirkung), `LoseSlot` (alle getragenen Items dieses Slots ablegen), `LoseSmallItem` (→ `PendingChoice DiscardSmallItem`, wenn ein Slot-None-Item getragen wird, sonst verpufft), `DiscardHand(null)` alle / `DiscardHand(n)` n zufällige Handkarten, `GiveEquippedItemToNeighbor` (→ `PendingChoice GiveEquippedItem` mit Empfänger = Nachbar, wenn etwas in `Equipped` liegt, sonst verpufft), `DrawCards`, `NextTurnPenalty(n)` (→ `TemporaryEffect` mit `CombatModifier = -n`, `StartsAfterTurnNumber = s.TurnNumber`), `Manual` (→ Event `ManualEffectShown(cardId)`); andere Wirkungstypen werden hier ignoriert (sie sind passiv oder kampfbezogen).
  - Events: `CursePlayed(string ById, string TargetId, string CardId)`, `CurseBlocked(string TargetId, string CardId)`, `LevelChanged(string PlayerId, int From, int To)`, `CardsLost(string PlayerId, IReadOnlyList<int> InstanceIds)`, `ChoiceRequested(string PlayerId, ChoiceKind Kind)`, `ChoiceResolved(string PlayerId, int InstanceId)`, `ManualEffectShown(string CardId)`.
  - `internal static class TemporaryEffects { static int CombatModifierFor(GameState s, string playerId); static void ExpireAtEndOfTurn(GameState s, string playerId); }` – `CombatModifierFor` summiert aktive Effekte (`s.TurnNumber > StartsAfterTurnNumber`); `ExpireAtEndOfTurn` entfernt die aktiven Effekte dieses Spielers (aufgerufen in Task 6 bei `EndTurn`).
  - `internal static class Curses { static void Apply(GameState s, CardCatalog c, GameRandom rng, string byId, string targetId, CardInstance curse, List<GameEvent> events); }` – prüft `CurseImmunity` (passiver Effekt des Ziels mit Tag ∈ `curse.Tags` → `CurseBlocked`), wendet sonst `Effects` an, legt die Karte ab. Wird von Task 6 (gezogener Fluch) wiederverwendet.

Fluch aus der Hand (`PlayCard`, Kind `Curse`): erlaubt in jeder Phase außer `Setup`/`GameOver`, von jedem Spieler, `TargetPlayerId` Pflicht. `ResolveChoice`: nur der Spieler des ersten `PendingChoice`; `DiscardSmallItem` → gewählte Karte muss ein getragenes Slot-None-Item sein → Ablage; `GiveEquippedItem` → gewählte Karte aus `Equipped` → in den **Rucksack** des Empfängers (er legt sie ggf. selbst an).

- [ ] **Step 1: Failing Tests schreiben**
  - `CurseTests.LoseLevelCurse_FromHand_LowersTargetLevel_AndDiscards`, `LoseLevels_NeverBelowOne` (Stufe 2, `EffectApplier.ApplyToPlayer` mit `new LoseLevels(2)` → Stufe 1), `CurseWithoutTarget_IsRejected`, `Curse_InOtherPlayersTurn_IsAllowed`, `Curse_InSetup_IsRejected`, `LoseHeadCurse_DiscardsHeadItem`, `DiscardTwoCurse_RemovesTwoHandCards`, `TeamAssistant_IsImmuneToSecurityCurse` (`curse-give-item` → `CurseBlocked`, keine Auswahl), `PenaltyCurse_StartsNextTurn` (`CombatModifierFor` im selben `TurnNumber` 0, nach `TurnNumber++` −2), `ManualCurse_EmitsManualEffectShown`.
  - `ChoiceTests.GiveItemCurse_CreatesChoice_ForTarget`, `GiveItemCurse_WithNothingEquipped_Fizzles`, `PendingChoice_BlocksOtherCommands` (z. B. `EquipCard` → abgelehnt), `ResolveChoice_ByWrongPlayer_IsRejected`, `ResolveChoice_GiveItem_MovesItemToLeftNeighborsBackpack` (Spieler-Index 2 von 3 → Empfänger Index 0), `ResolveChoice_InvalidCard_IsRejected`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Flüche, Wirkungen und Spielerauswahlen`

---

### Task 6: Zugablauf – Ticket öffnen, Ärger suchen, Backlog, Feierabend

**Files:**
- Create: `src/DeveloperDungeon.Engine/Rules/TurnRules.cs`, `Rules/CombatStart.cs`
- Modify: `Commands/GameCommand.cs`, `Events/GameEvent.cs`, `Game.cs`
- Create: `tests/.../TurnTests.cs`, `tests/.../CharityTests.cs`

**Interfaces:**
- Consumes: `Decks.Draw`, `Curses.Apply`, `TemporaryEffects.ExpireAtEndOfTurn`.
- Produces:
  - Befehle `OpenTicket(string ActorId)`, `LookForTrouble(string ActorId, int MonsterInstanceId)`, `WorkBacklog(string ActorId)`, `EndTurn(string ActorId, IReadOnlyList<int> GiveAway)`.
  - `internal static class CombatStart { static void Begin(GameState s, CardInstance monster); }` → `Combat = new CombatState { Stage = Fighting, Monsters = [monster] }`, `Phase = Combat`.
  - Events: `TicketOpened(string PlayerId, string CardId)`, `CombatStarted(string PlayerId, string MonsterCardId)`, `CardDrawn(string PlayerId, Deck Deck)` (Karte verdeckt), `CardsGiven(string FromId, string ToId, int Count)`, `TurnEnded(string PlayerId, string NextPlayerId)`.

Regeln: Alle vier Befehle nur vom aktiven Spieler. `OpenTicket` nur in `OpenTicket`: Ticket ziehen; `null` → `AfterTicket`; Monster → `CombatStart.Begin`; Fluch → `Curses.Apply(byId: aktiver, target: aktiver)`, dann `AfterTicket`; sonst auf die Hand, `AfterTicket`. `LookForTrouble` nur in `AfterTicket`, Monster aus eigener Hand → Kampf. `WorkBacklog` nur in `AfterTicket` → Ticket verdeckt auf die Hand (oder nichts, wenn leer) → `EndTurn`. Nach Kampfende (Tasks 7/9) geht es direkt in `EndTurn`.
`EndTurn` nur in Phase `EndTurn`: Ist `Hand.Count > 5`, muss `GiveAway` genau `Hand.Count - 5` verschiedene eigene Handkarten enthalten, sonst Ablehnung „Du musst genau N Karten abgeben.“; ist `Hand.Count ≤ 5`, muss `GiveAway` leer sein. Verteilung: Empfänger = Mitspieler mit der niedrigsten Stufe; ist die eigene Stufe ≤ dieser Stufe → alle abgegebenen Karten ablegen; bei mehreren niedrigsten Mitspielern reihum ab dem nächsten in Zugreihenfolge verteilen. Danach `TemporaryEffects.ExpireAtEndOfTurn(aktiver)`, `ActivePlayerIndex = (i+1)%n`, `TurnNumber++`, `Phase = OpenTicket`.

- [ ] **Step 1: Failing Tests schreiben**
  - `TurnTests.OpenTicket_Monster_StartsCombat`, `OpenTicket_Curse_AppliesToActivePlayer_ThenAfterTicket`, `OpenTicket_OtherCard_GoesToHand`, `OpenTicket_ByNonActivePlayer_IsRejected`, `OpenTicket_Twice_IsRejected`, `OpenTicket_WithEmptyTicketDecks_GoesToAfterTicket`, `LookForTrouble_WithMonsterFromHand_StartsCombat`, `LookForTrouble_WithNonMonster_IsRejected`, `WorkBacklog_DrawsTicketToHand_AndGoesToEndTurn`, `EndTurn_FromAfterTicket_IsRejected`, `EndTurn_PassesToNextPlayer_AndIncrementsTurn`, `EndTurn_ExpiresActiveTemporaryEffects`.
  - `CharityTests.EndTurn_WithSevenCards_RequiresExactlyTwo` (1 → abgelehnt, 3 → abgelehnt, 2 → ok), `Excess_GoesToLowestLevelPlayer`, `Excess_SplitAmongTiedLowest_RoundRobin`, `Excess_DiscardedWhenActiveIsLowest`, `Excess_DiscardedWhenActiveTiesLowest`, `GiveAway_WithFiveCards_MustBeEmpty`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Zugablauf mit Ticket, Backlog und Feierabend`

---

### Task 7: Kampfstärke und Kampfauswertung

**Files:**
- Create: `src/DeveloperDungeon.Engine/Rules/StrengthCalculator.cs`, `Rules/CombatRules.cs`, `Rules/CombatCleanup.cs`
- Modify: `Commands/GameCommand.cs`, `Events/GameEvent.cs`, `Game.cs`
- Create: `tests/.../StrengthTests.cs`, `tests/.../CombatResolutionTests.cs`

**Interfaces:**
- Consumes: `PassiveEffects`, `TemporaryEffects.CombatModifierFor`, `Decks`.
- Produces:
  - `public sealed record CombatSummary(int PlayerStrength, int MonsterStrength, bool PlayerWinsTies, bool PlayerWouldWin, int TotalRewardLevels, int TotalRewardLoot);`
  - `public static class StrengthCalculator { static CombatSummary Summarize(GameState s, CardCatalog c); static int BasePower(GameState s, CardCatalog c, string playerId); }`
    - `BasePower` = Stufe + `Bonus` aller `Equipped`-Equipment-Karten + Summe passiver `CombatBonus`.
    - Spielerstärke = `BasePower(aktiv)` (+ `BasePower(Helfer)`) + passive `ConditionalCombatBonus` beider Kämpfer mit `MonsterTag` ∈ Tags irgendeines Monsters + `TemporaryEffects.CombatModifierFor` beider + Summe `CombatBonus` der One-Shots auf `Side.Player` + `ManualPlayerAdjust`.
    - Monsterstärke = Summe Monster-`Level` + Summe `MonsterModifier.Levels` aller Verstärker + One-Shots auf `Side.Monster` + `ConditionalCombatBonus`-Effekte der Monsterkarten, deren `PlayerDepartment`/`PlayerLanguage` zu Aktivem oder Helfer passt + `ManualMonsterAdjust`.
    - `PlayerWinsTies` = passiver `TiesWin` bei Aktivem oder Helfer. `PlayerWouldWin` = `P > M || (P == M && PlayerWinsTies)`.
    - `TotalRewardLevels` = Summe `RewardLevels`; `TotalRewardLoot` = Summe `RewardLoot` + Summe `MonsterModifier.ExtraLoot`.
  - Befehl `ResolveCombat(string ActorId)` – nur aktiver Spieler, `Phase == Combat`, `Stage == Fighting`.
  - Events: `CombatWon(string PlayerId, int LevelsGained, int LootDrawn)`, `CombatLost(string PlayerId)`, `GameWon(string PlayerId)`.
  - `internal static class CombatCleanup { static void DiscardAll(GameState s, CardCatalog c); }` – legt Monster, Verstärker, One-Shots, `OtherPlayed` ab, `Combat = null`, `Phase = EndTurn`.

Sieg: Aktiver +`TotalRewardLevels` (max. 10); Helfer +1 nur mit passivem `HelpingGrantsLevel` (max. 9). `TotalRewardLoot` Loot-Karten ziehen; die ersten `min(HelperLootShare, gezogen)` an den Helfer, Rest an den Aktiven (Karten werden verdeckt gezogen, daher ist „Helfer wählt zuerst“ gleichwertig). Erreicht der Aktive 10 → `WinnerId`, `Phase = GameOver`, `GameWon`. Sonst `CombatCleanup`. Niederlage: `Stage = Fleeing`, `Fleeing` = Aktiver (+ Helfer), jeweils mit allen Monster-InstanceIds.

- [ ] **Step 1: Failing Tests schreiben**
  - `StrengthTests.PlayerStrength_IsLevelPlusEquippedBonuses` (Stufe 2, `equip-head` +1, `equip-ide-1h` +2 → 5; Rucksack-Item zählt nicht), `ConditionalBonus_AppliesOnlyAgainstMatchingTag` (`lang-csharp` vs `monster-l3-legacy` +1, vs `monster-l1` +0), `MonsterBonus_AgainstQa` (`monster-l8-hates-qa` gegen QA-Spieler → 10), `TemporaryPenalty_ReducesStrength`, `HelperStrength_IsAdded`, `TiesWin_FromQa`.
  - `CombatResolutionTests.Win_GrantsLevelAndLoot_ThenEndTurn` (Stufe 4 vs `monster-l3-legacy` → Stufe 5, +1 Handkarte, Phase `EndTurn`, Monster in Ablage), `Tie_IsLoss_WithoutTiesWin`, `Tie_IsWin_WithQa`, `BossAtLevelNine_ReachesTenNotEleven_AndWins` (Stufe 9 + Ausrüstung > 12 gegen `monster-boss-l12` → Stufe 10, `Phase GameOver`, `WinnerId == "p1"`), `Loss_EntersFleeing_WithActiveAndHelper`, `ResolveCombat_ByNonActive_IsRejected`, `HelperGetsAgreedLootShare_AndNoLevel` (Share 1 bei 2 Loot → je 1 Karte), `TeamAssistantHelper_GetsOneLevel`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.** Für Hilfe-Tests den Kampfzustand im Test direkt setzen (`game.State.Combat!.HelperId = "p2"`), da `OfferHelp` erst in Task 8 kommt.
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Kampfstärke und Kampfauswertung`

---

### Task 8: Eingreifen im Kampf – Einweg-Karten, Verstärker, Wandernder Bug, Hilfe

**Files:**
- Modify: `Rules/CardPlayRules.cs`, `Rules/CombatRules.cs`, `Commands/GameCommand.cs`, `Events/GameEvent.cs`, `Game.cs`
- Create: `tests/.../CombatInteractionTests.cs`, `tests/.../HelpTests.cs`

**Interfaces:**
- Consumes: `PlayCard` (Task 5), `StrengthCalculator` (Task 7).
- Produces:
  - Befehle `OfferHelp(string ActorId, int LootShare)`, `AcceptHelp(string ActorId, string HelperId)`.
  - Events: `CardPlayedInCombat(string PlayerId, string CardId, Side? Side)`, `HelpOffered(string PlayerId, int LootShare)`, `HelpAccepted(string HelperId)`.

Regeln (alle nur in `Phase Combat`, `Stage Fighting`, Karte aus Hand oder Rucksack des Ausspielenden, jeder Spieler darf):
- `OneShot` mit `CombatBonus`: `Side` Pflicht → `OneShots`. Enthält die Karte `NextTurnPenalty(n)`, bekommt **der Ausspielende** den temporären Effekt. `OneShot` mit `AutoEscape` hier abgelehnt („Das hilft erst bei der Flucht.“ – Task 9). `OneShot` mit `Manual` → `ManualEffectShown`, Karte ablegen.
- `MonsterEnhancer`: `TargetMonsterInstanceId` Pflicht und muss im Kampf sein → `Enhancers[id]`.
- `Special` mit `AddMonsterToCombat`: `SecondaryInstanceId` = Monster aus der Hand des Ausspielenden → zu `Monsters`, Sonderkarte zu `OtherPlayed`.
- `LevelUp`, `Equipment` im Kampf → abgelehnt.
- `OfferHelp`: nicht vom Aktiven, noch kein Helfer, `LootShare ≥ 0`; ein neues Angebot desselben Spielers ersetzt das alte.
- `AcceptHelp`: nur Aktiver, Angebot von `HelperId` existiert, noch kein Helfer → `HelperId`, `HelperLootShare`, `Offers` leeren.

- [ ] **Step 1: Failing Tests schreiben**
  - `CombatInteractionTests.OneShotOnPlayerSide_TurnsLossIntoWin` (Stufe 1 vs `monster-l3-legacy`, `oneshot-plus5` → `PlayerWouldWin`), `OneShotOnMonsterSide_ByOpponent_TurnsWinIntoLoss`, `OneShotWithoutSide_IsRejected`, `ColdCoffeePenalty_HitsPlayerWhoPlayedIt_NextTurn`, `OneShot_OutsideCombat_IsRejected`, `Enhancer_RaisesMonsterAndLoot` (+5 Stärke, `TotalRewardLoot` +1), `Enhancer_WithoutTarget_IsRejected`, `WanderingBug_AddsSecondMonster_StrengthsAndRewardsSum`, `LevelUp_InCombat_IsRejected`, `RemoteDay_DuringFighting_IsRejected`.
  - `HelpTests.OfferAndAccept_SetsHelper`, `ActivePlayer_CannotOfferHelp`, `Accept_WithoutOffer_IsRejected`, `SecondHelper_IsRejected`, `NewOffer_ReplacesOldOfferOfSamePlayer`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Eingreifen und Hilfe im Kampf`

---

### Task 9: Flucht und Schlimme Dinge

**Files:**
- Create: `src/DeveloperDungeon.Engine/Rules/EscapeRules.cs`
- Modify: `Game.cs` (Würfel-Override), `Rules/CardPlayRules.cs` (AutoEscape), `Commands/GameCommand.cs`, `Events/GameEvent.cs`
- Create: `tests/.../EscapeTests.cs`

**Interfaces:**
- Consumes: `EffectApplier`, `CombatCleanup`, `PassiveEffects`.
- Produces:
  - Befehl `AttemptEscape(string ActorId)`.
  - Events: `EscapeRolled(string PlayerId, string MonsterCardId, int Roll, int Bonus, bool Escaped)`, `BadStuffApplied(string PlayerId, string MonsterCardId)`.
  - `internal void ForceNextRolls(params int[] rolls)` auf `Game` – nur für Tests: Warteschlange von W6-Ergebnissen, die vor der RNG verbraucht wird (nicht im `GameState`, nicht serialisiert).

Regeln: `AttemptEscape` nur in `Stage Fleeing` von einem Spieler in `Fleeing` mit offenen Monstern. Pro offenem Monster: `NoPursuitBelowLevel` gesetzt und Spielerstufe < Wert → entkommen ohne Wurf; sonst W6 + Summe passiver `EscapeBonus` ≥ 5 → entkommen; sonst `EffectApplier.ApplyToPlayer(BadStuff)` + `BadStuffApplied`. Danach Spieler aus `Fleeing` entfernen. Ist `Fleeing` leer → `CombatCleanup.DiscardAll` (offene `PendingChoices` bleiben bestehen und blockieren wie gewohnt). `PlayCard` mit `OneShot`+`AutoEscape` in `Stage Fleeing` durch einen fliehenden Spieler: alle seine Monster gelten als entkommen, Karte ablegen, ggf. Cleanup.

- [ ] **Step 1: Failing Tests schreiben**
  - `EscapeTests.RollFiveOrSix_Escapes` (`ForceNextRolls(5)`, kein Stufenverlust), `RollFour_AppliesBadStuff` (`monster-l1`, Stufe 3 → 2), `EscapeBonus_TurnsFourIntoEscape` (`dept-cloud`), `NoPursuitBelowLevel_EscapesWithoutRoll` (Stufe 2 vs Boss; kein `EscapeRolled` mit Wurf, `Escaped == true`), `TwoMonsters_RollSeparately`, `HelperFleesSeparately` (Aktiver entkommt, Helfer nicht → nur Helfer verliert), `AfterAllFled_CombatClearedAndEndTurn`, `BadStuffWithChoice_LeavesPendingChoice_CombatStillCleared` (`monster-l8-hates-qa` mit `equip-small` getragen), `RemoteDay_WhileFleeing_EscapesAll`, `AttemptEscape_ByNonFleeingPlayer_IsRejected`.
  - `SaveLoad_MidFleeing_ContinuesIdentically`: Kampf verloren, Fluchtphase; `json = game.Save()`; `var a = game.Execute(new AttemptEscape("p1"))`; `var b = Game.Load(json, catalog).Execute(new AttemptEscape("p1"))`; Events (inkl. Wurf) gleich und beide `Save()` gleich (ohne `ForceNextRolls`).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Flucht und Schlimme Dinge`

---

### Task 10: Stufe-hoch-Karten, Verkaufen, Werkzeugkasten

**Files:**
- Create: `src/DeveloperDungeon.Engine/Rules/EconomyRules.cs`, `Rules/ToolboxRules.cs`
- Modify: `Rules/CardPlayRules.cs`, `Rules/CommandGuard.cs`, `Commands/GameCommand.cs`, `Events/GameEvent.cs`, `Game.cs`
- Create: `tests/.../EconomyTests.cs`, `tests/.../ToolboxTests.cs`

**Interfaces:**
- Produces:
  - Befehle: `SellItems(string ActorId, IReadOnlyList<int> InstanceIds)`; Werkzeugkasten: `enum Zone { Hand, Equipped, Backpack, Department, Language }`, `ManualAdjustLevel(string ActorId, string TargetPlayerId, int Delta)`, `ManualMoveCard(string ActorId, int InstanceId, string ToPlayerId, Zone ToZone)`, `ManualDiscard(string ActorId, int InstanceId)`, `ManualTakeFromDiscard(string ActorId, int InstanceId, string ToPlayerId)`, `ManualCombatAdjust(string ActorId, Side Side, int Delta)`, `RollDie(string ActorId)`.
  - Events: `ItemsSold(string PlayerId, int Credits, int LevelsGained)`, `ToolboxUsed(string ActorId, string Description)`, `DieRolled(string PlayerId, int Roll)`.
  - `CommandGuard`: Werkzeugkasten-Befehle sind auch bei offenen `PendingChoices` erlaubt, aber nicht in `GameOver`.

Regeln:
- `PlayCard` mit `LevelUp`: nur `IsOwnTurnOutsideCombat`; abgelehnt, wenn Stufe danach ≥ 10 („Stufe 10 gibt es nur im Kampf.“); Karte ablegen.
- `SellItems`: nur `IsOwnTurnOutsideCombat`; Karten aus eigener Hand/Equipped/Rucksack, alle mit `Value > 0`, keine doppelt; Summe < 1000 → abgelehnt; Stufen = `Summe / 1000` (Rest verfällt); Stufe danach ≥ 10 → abgelehnt; verkaufte Karten ablegen.
- Werkzeugkasten (ohne Regelprüfung, jede Aktion → `ToolboxUsed` + Log mit Präfix „[Werkzeugkasten]“): `ManualAdjustLevel` klemmt auf 1…10, Stufe 10 → Spielende wie beim Kampfsieg; `ManualMoveCard` findet die Karte in jeder Spielerzone, Kampfbereich oder Ablage und legt sie in die Zielzone; `ManualDiscard` aus jeder Spielerzone oder dem Kampf; `ManualTakeFromDiscard` aus einer der beiden Ablagen auf die Hand; `ManualCombatAdjust` nur wenn `Combat != null`; `RollDie` würfelt W6.

- [ ] **Step 1: Failing Tests schreiben**
  - `EconomyTests.LevelUpCard_RaisesLevel`, `LevelUpCard_AtNine_IsRejected`, `LevelUpCard_InOtherPlayersTurn_IsRejected`, `Sell_1100Credits_GivesOneLevel_RestLost` (`equip-ide-2h` 600 + `equip-feet` 400 + `equip-small` 100), `Sell_Under1000_IsRejected`, `Sell_ToTen_IsRejected`, `Sell_CardWithoutValue_IsRejected`, `Sell_DuringCombat_IsRejected`.
  - `ToolboxTests.AdjustLevel_ClampsToOne`, `AdjustLevel_ToTen_EndsGame`, `MoveCard_FromOtherPlayersEquipmentToMyHand`, `Discard_FromCombat_RemovesMonster`, `TakeFromDiscard_ToHand`, `CombatAdjust_ChangesSummary`, `CombatAdjust_WithoutCombat_IsRejected`, `Toolbox_AllowedWhileChoicePending`, `RollDie_EmitsValueInRange`, `ToolboxAction_IsLoggedWithPrefix`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Stufe-hoch, Verkaufen und Werkzeugkasten`

---

### Task 11: Spielersicht, Stufentitel, Spielsitzung

**Files:**
- Create: `src/DeveloperDungeon.Engine/Views/GameView.cs`, `Rules/LevelTitles.cs`, `Sessions/IGameSession.cs`, `Sessions/LocalGameSession.cs`
- Modify: `Game.cs` (`GetView`)
- Create: `tests/.../ViewTests.cs`, `tests/.../LocalGameSessionTests.cs`

**Interfaces:**
- Produces:
  - `public static class LevelTitles { static string For(int level); }` – exakt: 1 „Unbezahlter Praktikant“, 2 „Werkstudent“, 3 „Junior Developer“, 4 „Developer“, 5 „Senior Developer“, 6 „Senior Developer mit Meinung“, 7 „Software Architect“, 8 „Principal Engineer“, 9 „Tech Lead“, 10 „CTO“.
  - Records: `PublicPlayerView(string Id, string Name, string Color, int Level, string Title, int HandCount, IReadOnlyList<CardInstance> Equipped, IReadOnlyList<CardInstance> Backpack, IReadOnlyList<CardInstance> Departments, IReadOnlyList<CardInstance> Languages, int BasePower)`, `CombatView(CombatState State, CombatSummary Summary)`, `GameView(GamePhase Phase, string ActivePlayerId, int TurnNumber, IReadOnlyList<PublicPlayerView> Players, IReadOnlyList<CardInstance>? MyHand, CombatView? Combat, int TicketDeckCount, int LootDeckCount, CardInstance? TicketDiscardTop, CardInstance? LootDiscardTop, PendingChoice? CurrentChoice, string? WinnerId, IReadOnlyList<LogEntry> RecentLog)` – `RecentLog` = letzte 100 Einträge.
  - `public GameView GetView(string? viewerId)` auf `Game` – `MyHand` nur für `viewerId`, sonst `null`.
  - `public interface IGameSession { GameView GetView(string? viewerId); CardCatalog Catalog { get; } Task<CommandResult> SendAsync(GameCommand command); event Action? StateChanged; }`
  - `public sealed class LocalGameSession(Game game, Action<string>? onSaved = null) : IGameSession` – `SendAsync` führt synchron aus; bei `Accepted` → `onSaved?.Invoke(game.Save())`, dann `StateChanged`.

- [ ] **Step 1: Failing Tests schreiben**
  - `ViewTests.PublicView_HidesAllHands` (`GetView(null).MyHand == null`, `HandCount == 8`), `PlayerView_ShowsOnlyOwnHand`, `LevelTitles_MatchSpec` (Theory über alle 10), `View_IncludesCombatSummary_DuringCombat`, `RecentLog_IsCappedAt100`.
  - `LocalGameSessionTests.AcceptedCommand_RaisesStateChanged_AndSaves` (gespeichertes JSON lässt sich mit `Game.Load` laden), `RejectedCommand_RaisesNothing`.
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: Implementieren.**
- [ ] **Step 4: Tests laufen lassen** – PASS.
- [ ] **Step 5: Commit** – `[ADD] Spielersicht und lokale Spielsitzung`

---

### Task 12: Startkarten und Simulations-Test

**Files:**
- Create: `src/DeveloperDungeon.Engine/Cards/Data/starter.json`
- Create: `tests/.../EmbeddedCatalogTests.cs`, `tests/.../Simulation/SimpleBot.cs`, `tests/.../Simulation/SimulationTests.cs`

**Interfaces:**
- Consumes: alles Vorherige.
- Produces: `CardCatalog.LoadEmbedded()` liefert die Startkarten (von Plan 2 genutzt, in Plan 3 ersetzt/erweitert).

**Startkarten** – Texte (`flavor`, `rulesText`) schreibt der Implementierende auf Deutsch, frech und kurz (ein Gag pro Karte); die Werte sind fest:

| Id | Art | Werte / Wirkungen | Count |
|---|---|---|---|
| `monster-works-on-my-machine` | Monster | „Der ‚Geht bei mir lokal‘-Entwickler“, L3, 1/1, Tags Mensch, `ConditionalCombatBonus(2, playerDepartment qa)`, BadStuff `LoseSlot(Head)` | 3 |
| `monster-impatient-po` | Monster | „Der ungeduldige Product Owner“, L5, 1/2, Tags Mensch, BadStuff `LoseLevels(1)` | 3 |
| `monster-legacy-2012` | Monster | „Der veraltete Legacy-Code von 2012“, L8, 1/2, Tags Legacy, BadStuff `LoseSlot(Hands)` | 3 |
| `monster-endless-meeting` | Monster | „Die unendliche Meeting-Schleife“, L12, 2/3, Tags Meeting, `NoPursuitBelowLevel` 3, BadStuff `LoseLevels(2)` | 3 |
| `curse-unlocked-pc` | Fluch | „Du hast vergessen, deinen Rechner zu sperren …“, Tags Security, `GiveEquippedItemToNeighbor(Left)` | 2 |
| `curse-master-conflict` | Fluch | „Konflikt im Master-Branch“, `DiscardHand(2)` | 2 |
| `curse-pipeline-red` | Fluch | „Die Pipeline ist rot“, `NextTurnPenalty(2)` | 2 |
| `curse-monday-meeting` | Fluch | „Montagmorgen-Meeting um 8:00 Uhr“, `LoseLevels(1)` | 2 |
| `dept-cross-platform` | Abteilung | Key `cross-platform`, `EscapeBonus(1)` | 1 |
| `dept-native` | Abteilung | Key `native`, `CombatBonus(1)` | 1 |
| `dept-cloud-platform` | Abteilung | Key `cloud-platform`, `ConditionalCombatBonus(3, monsterTag Legacy)` | 1 |
| `dept-team-assistant` | Abteilung | Key `team-assistant`, `CurseImmunity("Security")`, `HelpingGrantsLevel` | 1 |
| `dept-qa` | Abteilung | Key `qa`, `TiesWin` | 1 |
| `dept-ai-team` | Abteilung | Key `ai-team`, `ConditionalCombatBonus(2, monsterTag Mensch)` | 1 |
| `lang-csharp` | Sprache | Key `csharp`, `ConditionalCombatBonus(1, monsterTag Legacy)` | 1 |
| `lang-kotlin` | Sprache | Key `kotlin`, `EscapeBonus(1)` | 1 |
| `lang-python` | Sprache | Key `python`, `CombatBonus(1)` | 1 |
| `special-full-stack` | Sonder (Ticket) | „Full-Stack“, `MaxDepartments(2)` | 1 |
| `special-polyglot` | Sonder (Ticket) | „Polyglott“, `MaxLanguages(2)` | 1 |
| `special-wandering-bug` | Sonder (Ticket) | „Wandernder Bug“, `AddMonsterToCombat` | 2 |
| `equip-rider` | Ausrüstung | „JetBrains Rider“, Hands 1, +3, 600 | 3 |
| `equip-vscode` | Ausrüstung | „VS Code (mit 45 Extensions)“, Hands 1, +2, 400 | 3 |
| `equip-visual-studio` | Ausrüstung | „Visual Studio“, Hands 2, +4, 800 | 3 |
| `equip-nc-headphones` | Ausrüstung | „Noise-Cancelling-Kopfhörer“, Head, +1, 300 | 3 |
| `equip-gaming-socks` | Ausrüstung | „Gaming-Socken“, Feet, +1, 200 | 3 |
| `equip-ultrawide` | Ausrüstung | „49-Zoll-Ultrawide-Monitor“, Big, +3, 700 | 3 |
| `oneshot-cold-coffee` | Einweg | „Kalter Filterkaffee von gestern“, `CombatBonus(2)`, `NextTurnPenalty(1)`, 100 | 5 |
| `levelup-raise` | Stufe-hoch | „Gehaltserhöhung“, `GainLevels(1)` | 4 |
| `oneshot-remote-day` | Einweg | „Remote-Tag“, `AutoEscape`, 200 | 3 |

(Ticket-Stapel 37, Loot-Stapel 30 Karten – reicht für 6 Spieler × 4.)

- [ ] **Step 1: Failing Tests schreiben**
  - `EmbeddedCatalogTests.EmbeddedCatalog_IsValid` (`Assert.Empty(CardCatalog.LoadEmbedded().Validate())`), `EmbeddedCatalog_ContainsAllUserRequiredCards` (alle Ids der Tabelle vorhanden), `EmbeddedCatalog_SupportsSixPlayers` (`Game.New` mit 6 Spielern wirft nicht).
  - `SimulationTests.RandomGames_NeverDeadlock`: für Seeds 1…50 und Spielerzahl `3 + seed % 4`: Spiel mit `LoadEmbedded()` und `SimpleBot` bis `GameOver` oder 400 Züge (`TurnNumber`) spielen. Pro Schritt erzeugt der Bot eine Liste möglicher Befehle; **mindestens einer muss angenommen werden**, sonst Fail mit `Save()`-Dump im Fehlertext. Keine Exception darf auftreten. Am Ende per `ITestOutputHelper` ausgeben: Anteil beendeter Partien, durchschnittliche Zuganzahl (Hilfe fürs Balancing in Plan 3; kein Assert darauf).
- [ ] **Step 2: Tests laufen lassen** – FAIL.
- [ ] **Step 3: `starter.json` schreiben und `SimpleBot` implementieren.** `SimpleBot.NextCommands(Game game) : IEnumerable<GameCommand>` in Prioritätsreihenfolge: offene Auswahl → `ResolveChoice` mit erster gültiger Karte des Wählenden; `Setup` → je Spieler alle Handkarten `EquipCard` versuchen, dann `StartGame`; `OpenTicket` → erst alle Handkarten des Aktiven `EquipCard` versuchen, dann `OpenTicket`; `AfterTicket` → `LookForTrouble` mit einem Hand-Monster, dessen Stufe < `BasePower`, sonst `WorkBacklog`; `Combat/Fighting` → wenn `!PlayerWouldWin` und Aktiver hat One-Shot → `PlayCard(Side.Player)`, dann `ResolveCombat`; `Combat/Fleeing` → `AttemptEscape` für ersten fliehenden Spieler; `EndTurn` → `EndTurn` mit den ersten `Hand.Count - 5` Handkarten. Abgelehnte Equip-Versuche zählen nicht als Deadlock; geprüft wird, dass der jeweils letzte, phasen-voranbringende Befehl angenommen wird.
- [ ] **Step 4: Tests laufen lassen** – `dotnet test` → alle PASS. Simulationsausgabe kurz ansehen; endet keine einzige Partie, ist das ein Balancing-Hinweis für Plan 3, kein Fehler dieses Plans.
- [ ] **Step 5: Commit** – `[ADD] Startkarten und Simulations-Test`
