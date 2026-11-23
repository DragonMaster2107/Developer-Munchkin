# Developer Dungeon – Design

Stand: 2026-11-23

## 1. Worum es geht und warum

**Developer Dungeon** ist ein humorvolles, kartenbasiertes Stufen-Kampfspiel im Entwickleralltag: Spieler steigen vom „Unbezahlten Praktikanten“ (Stufe 1) zum „CTO“ (Stufe 10) auf, indem sie Release-Blocker (Monster) besiegen, Ausrüstung (IDEs, Hardware, Kaffee) sammeln und sich gegenseitig mit Flüchen (Bugs, rote Pipelines, ungesperrte Rechner) sabotieren.

- **Zweck:** rein privates Spaßprojekt zum Spielen mit Freunden/Kollegen. Keine Veröffentlichung, keine kommerzielle Nutzung.
- **Gefühl:** schnell, frech, viel Tischgespräch und Sabotage, Humor in jedem Kartentext und jedem Bild. Balanciert wie ein klassisches Stufen-Kampf-Kartenspiel.
- **Eigenständigkeit:** eigener Name, eigene Begriffe, eigene Regeltexte, eigene Karten. Die Genre-Mechanik (Stufen, Monster, Flüche, Ausrüstung, Klassen-artige Rollen) wird übernommen, keine Namen, Texte oder Grafiken eines bestehenden Spiels.

## 2. Ziele, Nicht-Ziele, Anti-Ziele

**Ziele (Version 1)**
- Komplett spielbare Partie für 3–6 Spieler auf **einem Gerät** (Gerät wird herumgereicht), im Browser als installierbare PWA, offline-fähig.
- Die App führt den Zugablauf, berechnet Kämpfe und setzt einfache Kartenwirkungen automatisch um (Regel-Variante „C – Mischform“).
- ~90 eigene Karten mit Vektor-Illustration im Comic-Stil und kleinen Animationen.
- Spielstand überlebt ein Neuladen der Seite.
- Automatisches Deployment auf GitHub Pages, installierbar auf Tablet/Handy.
- Architektur so, dass später ein Server (ASP.NET Core + SignalR) dieselbe Spiellogik nutzen kann, ohne Umbau des Kerns.

**Nicht-Ziele (Version 1)**
- Online-Mehrspieler über Server (kommt in einer späteren Phase, siehe Abschnitt 10).
- Benutzerkonten, Lobby, Matchmaking, Bestenlisten.
- Computer-Gegner (Bots).
- Sound/Musik.
- Kartenbaukasten/Editor in der App.

**Anti-Ziele (wäre ein Fehlschlag, auch wenn es technisch läuft)**
- Das Tischgespräch wird erstickt (man kann nicht mehr eingreifen, feilschen oder Hausregeln anwenden). Die App blockiert zwar regelwidrige Aktionen hart (siehe 7.2), aber Eingreifen ist jederzeit möglich, und der Werkzeugkasten (5.7) erlaubt jede Korrektur, auf die sich der Tisch einigt.
- Neue Karten erfordern neuen Code. Normale Karten sind reine Daten.
- Unlustige oder generische Texte. Jede Karte braucht einen Gag.
- Unbalanciert: ein Spieler läuft nach drei Runden davon oder niemand kommt über Stufe 5.

## 3. Rahmenbedingungen

- **Technik:** C#, .NET 10 (LTS, auf dem Rechner installiert: SDK 10.0.202), Blazor WebAssembly Standalone als PWA.
- **Sprache der Oberfläche:** Deutsch.
- **Geräte:** primär Tablet/Laptop quer auf dem Tisch; Smartphone muss funktionieren (responsive).
- **Grafiken:** werden als SVG im Code erstellt (flacher Comic-Vektorstil, kräftige Farben, dicke Formen, kleine CSS-Endlosanimationen). Keine Rasterbilder nötig; optionaler Platz für ein eigenes Bild pro Karte.
- **Rechtliches:** keine Marken, Namen, Texte oder Grafiken bestehender Spiele.

## 4. Lösungsansätze (Approaches)

Erwogen wurden:

1. **Spiellogik als reine C#-Bibliothek, zuerst lokal im Browser (gewählt).** Engine ohne Abhängigkeit zu Browser/Server; die Blazor-App spricht sie über eine `IGameSession`-Schnittstelle an. Später implementiert ein SignalR-Client dieselbe Schnittstelle, und der Server hostet die Engine. Vorteil: schnell spielbar, offline, kein Server nötig (nur statisches Hosting); Server ist Zusatz statt Umbau. Nachteil: beim Herumreichen kurz sichtbare Hände (abgemildert durch „Gerät übergeben“-Bildschirm).
2. **Von Anfang an Server-basiert** (ASP.NET Core + SignalR, Server hält den Zustand). Vorteil: echter Mehrspieler und versteckte Hände sofort. Nachteil: deutlich mehr Aufwand bis zur ersten spielbaren Karte, Hosting früh nötig. Verworfen, weil Mehrspieler bewusst nach hinten geschoben wird.
3. **Peer-to-Peer (WebRTC)** – verworfen, in C#/Blazor unnötig umständlich.

**Regel-Tiefe** (separate Entscheidung): gewählt wurde **C – Mischform**: App steuert Zugablauf, rechnet Kämpfe, wendet einfache Wirkungen automatisch an; exotische Karten zeigen ihren Text und werden von Hand angewendet. Verworfen: **A – reiner digitaler Spieltisch** (zu wenig Hilfe) und **B – vollständige Regel-Engine mit Reaktionsfenstern für jede Aktion** (zu aufwendig, jede Karte braucht Code).

## 5. Spielregeln (Version 1)

Alle Begriffe sind eigene; Zahlenwerte sind Startwerte für das Balancing und dürfen beim Testen angepasst werden.

### 5.1 Stufenleiter

| Stufe | Titel |
|---|---|
| 1 | Unbezahlter Praktikant |
| 2 | Werkstudent *(Vorschlag)* |
| 3 | Junior Developer *(Vorschlag)* |
| 4 | Developer *(Vorschlag)* |
| 5 | Senior Developer |
| 6 | Senior Developer mit Meinung *(Vorschlag)* |
| 7 | Software Architect *(Vorschlag)* |
| 8 | Principal Engineer *(Vorschlag)* |
| 9 | Tech Lead |
| 10 | CTO |

Wer Stufe 10 erreicht, gewinnt sofort. **Stufe 10 kann nur durch einen gewonnenen Kampf erreicht werden** (nicht durch Karten oder Verkaufen). Stufe sinkt nie unter 1.

### 5.2 Stapel

- **Ticket-Stapel** (entspricht „Türen“): Monster, Monster-Verstärker, Flüche, Abteilungen, Sprachen, Sonderkarten.
- **Loot-Stapel** (entspricht „Schätzen“): Ausrüstung, Einweg-Gegenstände (Tränke), Stufe-hoch-Karten, Sonderkarten.
- Jeder Stapel hat einen eigenen Ablagestapel. Ist ein Stapel leer, wird sein Ablagestapel gemischt und wird zum neuen Stapel.

### 5.3 Start

- 3–6 Spieler, jeder gibt einen Namen ein und wählt eine Farbe/Avatar.
- Jeder startet auf Stufe 1, ohne Abteilung und Sprache, und erhält 4 Ticket- und 4 Loot-Karten verdeckt auf die Hand.
- Vor dem ersten Zug darf jeder Spieler Abteilung, Sprache und Ausrüstung von der Hand auslegen (Aufbau-Runde, reihum).
- Startspieler wird zufällig bestimmt.

### 5.4 Charakter

- **Abteilung** (max. 1, mit Sonderkarte „Full-Stack“ max. 2): Cross Platform, Native, Cloud Platform, Teamassistenz, QA, AI-Team. Jede hat 1–2 Fähigkeiten (siehe Kartenliste).
- **Sprache** (max. 1, mit Sonderkarte „Polyglott“ max. 2): C#, Kotlin, Python, TypeScript, Swift, Java. Jede hat 1 Fähigkeit.
- Abteilung/Sprache darf jederzeit im eigenen Zug abgelegt oder gewechselt werden (außerhalb eines Kampfes).
- **Ausrüstungsslots:** Kopf (1), Körper (1), Füße (1), Hände (2 Hände; Werkzeuge belegen 1 oder 2 Hände), Großes Item (1), beliebig viele kleine Items ohne Slot. Manche Items sind auf Abteilungen/Sprachen beschränkt („nur für QA“).
- Ausrüstung kann jederzeit im eigenen Zug außerhalb eines Kampfes angelegt, gewechselt oder in den „Rucksack“ (ausliegend, aber nicht aktiv) gelegt werden.

### 5.5 Ablauf eines Zuges

1. **Ticket öffnen:** Oberste Ticket-Karte wird offen gezogen.
   - Monster → Kampf (5.6).
   - Fluch → wirkt sofort auf den aktiven Spieler, dann abwerfen (bzw. bleibt liegen, wenn dauerhaft).
   - Alles andere → auf die Hand.
2. **Wenn kein Kampf stattfand**, wählt der Spieler eins:
   - **Ärger suchen:** ein Monster von der eigenen Hand ausspielen und bekämpfen.
   - **Backlog abarbeiten:** eine Ticket-Karte verdeckt auf die Hand ziehen.
3. **Feierabend:** Handlimit 5 Karten. Überzählige Karten gibt der Spieler dem Spieler mit der niedrigsten Stufe (bei Gleichstand aufteilen; ist er selbst der niedrigste, werden sie abgeworfen).

**Jederzeit im eigenen Zug (außerhalb von Kämpfen):** Ausrüstung/Abteilung/Sprache auslegen, Items verkaufen (je volle 1000 Credits = 1 Stufe, nicht auf Stufe 10), Stufe-hoch-Karten spielen.
**Jederzeit, auch in fremden Zügen:** Flüche auf beliebige Spieler spielen, Einweg-Gegenstände in Kämpfen spielen, Monster-Verstärker in Kämpfe spielen.

### 5.6 Kampf

- **Spielerstärke** = Stufe + Boni getragener Ausrüstung + Fähigkeitsboni + Einweg-Boni in diesem Kampf (+ Stärke eines Helfers).
- **Monsterstärke** = Monsterstufe + Verstärker + Einweg-Boni für das Monster + situative Boni (z. B. „+3 gegen QA“).
- **Sieg:** Spielerstärke **größer** als Monsterstärke. Gleichstand gewinnt das Monster (Ausnahmen per Fähigkeit möglich).
- **Sieg-Belohnung:** Stufen laut Monsterkarte (meist 1, Bosse 2) und Loot-Karten laut Monsterkarte (verdeckt ziehen). Ein Helfer bekommt keine Stufe, außer eine Fähigkeit sagt es; die Aufteilung des Loots wird vor dem Helfen frei verhandelt und in der App festgehalten (siehe unten).
- **Niederlage → Flucht:** Würfelwurf (W6) pro Monster, bei 5–6 entkommen. Fähigkeiten/Karten können das ändern. Misslingt die Flucht, treten die „Schlimmen Dinge“ der Monsterkarte ein.
- **Hilfe:** Der aktive Spieler darf genau einen Mitspieler um Hilfe bitten. Mitspieler können Hilfe anbieten mit einer Forderung (Anzahl Loot-Karten, die sie bekommen). Der aktive Spieler nimmt ein Angebot an oder lehnt alle ab. Bei Sieg verteilt die App das Loot entsprechend der Absprache (der Helfer wählt zuerst seine Karten). Bei Niederlage flieht auch der Helfer separat.
- **Mehrere Monster** (z. B. durch „Wandernder Bug“-Karten): Stärken werden addiert, Belohnungen auch; bei Flucht wird für jedes Monster einzeln gewürfelt.
- **Eingreifen („Moment, ich spiel noch was!“):** Während eines Kampfes zeigt die App eine Leiste mit allen Mitspielern. Jeder kann seinen Button antippen, bekommt (nach Gerät-übergeben-Bildschirm) seine Hand und darf passende Karten in den Kampf spielen (Verstärker, Einweg-Gegenstände, Flüche, Hilfe anbieten). Der aktive Spieler beendet den Kampf mit „Kampf auswerten“; danach läuft ein **5-Sekunden-Countdown** („Letzte Chance!“), in dem noch jemand eingreifen kann. Jedes Eingreifen setzt den Countdown zurück.

**Eingreifen außerhalb von Kämpfen** *(Entscheidung des Bauenden)*: In der Spielerleiste hat jeder Spieler jederzeit einen „Ich spiel was“-Button (z. B. um einen Fluch auf jemanden zu spielen). Antippen → Gerät-übergeben-Bildschirm → eigene Hand → Karte spielen und Ziel wählen. Die Engine nimmt Befehle deshalb in jeder Phase von jedem Spieler an, soweit die Karte es erlaubt; **jeder Befehl trägt die Id des handelnden Spielers** (wichtig für die spätere Server-Version).

### 5.7 Manueller Werkzeugkasten

Weil Variante C nicht alles automatisiert, gibt es jederzeit einen Werkzeugkasten (für jeden Spieler zugänglich, Aktionen werden im Spielprotokoll sichtbar vermerkt):
- Stufe eines Spielers ±1
- Karte von Spieler A zu Spieler B verschieben (Hand oder Ausrüstung)
- Karte abwerfen / Karte vom Ablagestapel nehmen
- Kampfstärke eines Monsters oder Spielers für den aktuellen Kampf ±N
- Würfeln

Exotische Karten (Wirkungstyp `Manuell`) zeigen beim Ausspielen ihren Text groß an; die Spieler setzen sie mit dem Werkzeugkasten um.

### 5.8 Spielprotokoll

Jede Aktion erzeugt einen Eintrag in einem sichtbaren, scrollbaren Protokoll („Anna öffnet ein Ticket: Der ‚Geht bei mir lokal‘-Entwickler (Stufe 3)“). Das dient der Nachvollziehbarkeit am Tisch und als Grundlage für Animationen.

## 6. Kartensystem

### 6.1 Kartenarten

| Art | Stapel | Kern-Eigenschaften |
|---|---|---|
| Monster | Ticket | Stufe, Belohnung (Stufen, Loot-Anzahl), Schlimme Dinge, situative Boni, optional „verfolgt nicht unter Stufe X“ |
| Monster-Verstärker | Ticket | ± Stufe auf ein Monster, ± Loot-Belohnung (z. B. „Mit Deadline morgen: +5, +1 Loot“) |
| Fluch | Ticket | Sofortwirkung oder dauerhafte Wirkung, Ziel (aktiver Spieler bzw. beliebiger Spieler, wenn von der Hand gespielt) |
| Abteilung | Ticket | Fähigkeiten |
| Sprache | Ticket | Fähigkeit |
| Sonderkarte | Ticket/Loot | z. B. Full-Stack, Polyglott, „Wandernder Bug“ (zusätzliches Monster in den Kampf) |
| Ausrüstung | Loot | Slot, Hände, Bonus, Wert in Credits, Einschränkungen, optional Fähigkeit |
| Einweg-Gegenstand | Loot | Bonus für einen Kampf (für Spieler oder Monster), Wert, Nebenwirkung |
| Stufe-hoch | Loot | +1 Stufe (z. B. „Gehaltserhöhung“, „Positives Code-Review“) |

### 6.2 Karten als Daten

Jede Karte ist ein Datensatz mit:
- `Id` (stabil, kebab-case, z. B. `monster-works-on-my-machine`)
- `Name`, `Typ`, `Stapel`, `FlavorText` (der Gag), `RegelText` (was passiert, für Menschen)
- typ-spezifische Felder (Stufe, Slot, Bonus, Wert …)
- `Wirkungen`: Liste strukturierter Wirkungen aus einem festen Katalog
- `Anzahl` im Stapel (Standard 1)
- `Grafik`: Verweis auf die SVG-Illustration (Konvention: gleiche Id)

**Wirkungskatalog (Version 1, erweiterbar):**
`Kampfbonus(n)`, `KampfbonusGegen(Bedingung, n)` (z. B. gegen Monster mit Tag „Legacy“, gegen Abteilung QA), `VerliereStufe(n)`, `ErhalteStufe(n)`, `VerliereSlot(Slot)`, `VerliereKleinesItem`, `VerliereHandkarten(n | alle)`, `GibItemAnNachbarn(Richtung)`, `ZieheKarten(Stapel, n)`, `FluchtBonus(n)`, `AutoFlucht`, `GleichstandGewinnt`, `HilfeBringtStufe`, `MaxAbteilungen(n)`, `MaxSprachen(n)`, `NaechsteRundeMalus(n)` (dauerhafter Effekt bis Ende der nächsten Runde), `Manuell` (nur Text).
Monster haben zusätzlich **Tags** (z. B. `Legacy`, `Meeting`, `Mensch`, `Bug`, `Infrastruktur`), auf die sich Boni beziehen.

Neue Karten, die nur Wirkungen aus dem Katalog nutzen, brauchen keinen Code. Nur neue Wirkungstypen erfordern Engine-Code.

### 6.3 Kartenpool Version 1 (~90 Karten)

Richtwerte; die konkrete Liste mit Texten wird beim Bauen erstellt und vom Nutzer gegengelesen.

- **Ticket-Stapel (~50):** 24 Monster (Stufe 1–20, Bosse 12+), 6 Monster-Verstärker, 10 Flüche, 6 Abteilungen, 6 Sprachen (je 1×), 3 Sonderkarten (Full-Stack, Polyglott, Wandernder Bug ×1–2).
- **Loot-Stapel (~40):** 20 Ausrüstung (davon 6 Werkzeuge/IDEs), 10 Einweg-Gegenstände, 5 Stufe-hoch, 5 Sonderkarten (z. B. „Remote-Tag“ = automatische Flucht).

Vom Nutzer vorgegebene Karten, die enthalten sein müssen:
- Ausrüstung: JetBrains Rider, VS Code (mit 45 Extensions), Visual Studio, Noise-Cancelling-Kopfhörer, Gaming-Socken, 49-Zoll-Ultrawide-Monitor
- Einweg: Kalter Filterkaffee von gestern
- Monster: Der „Geht bei mir lokal“-Entwickler (3), Der ungeduldige Product Owner (5), Der veraltete Legacy-Code von 2012 (8), Die unendliche Meeting-Schleife (12, Boss)
- Flüche: „Du hast vergessen, deinen Rechner zu sperren …“, Konflikt im Master-Branch, Die Pipeline ist rot, Montagmorgen-Meeting um 8:00 Uhr
- Abteilungen: Cross Platform, Native, Cloud Platform, Teamassistenz, QA, AI-Team
- Sprachen: C#, Kotlin, Python (+ TypeScript, Swift, Java als Vorschlag)

**Balancing-Leitlinien:** Monsterstufe ≈ erwartete Spielerstärke der Zielphase; Belohnung 1 Loot bei Stufe 1–4, 2 bei 5–9, 3 bei 10–14, 4–5 bei 15+; Bosse ab Stufe 12 geben 2 Stufen. Ausrüstungsboni +1 bis +4 (+5 nur bei großen/zweihändigen Items mit Einschränkung), Werte 100–800 Credits. Flüche sollen wehtun, aber nie einen Spieler komplett ausschalten.

### 6.4 Grafiken

- Stil wie die abgenommenen Beispielkarten (Referenz: [assets/2026-11-23-sample-cards.html](assets/2026-11-23-sample-cards.html)): flacher Comic-Vektorstil, kräftige Farben, einfache Figuren, Szenenhintergrund pro Karte, kleine Endlosanimation (Wackeln, Blinken, Dampf …).
- Pro Karte eine SVG-Datei mit eingebetteten Animationen. Wiederverwendbare Bausteine (Entwickler-Figur, Monitor, Tasse, Laptop, Büro-Hintergrund) werden als gemeinsame SVG-Teile gepflegt, damit der Stil einheitlich bleibt.
- Kartenrahmen farblich nach Art (Monster rot, Loot amber, Fluch violett, Abteilung/Sprache blau/grün o. ä.), mit Typ-Zeile, Bildbereich, Name, Regeltext, Fußzeile (Belohnung/Wert/Slot).
- Optional: eigenes Bild pro Karte (`{id}.png/.webp`) ersetzt die SVG, falls vorhanden.
- **Effekt-Animationen beim Spielen:** Karte fliegt aus der Hand, Monster „knallt“ beim Aufdecken auf den Tisch, Bildschirmwackeln bei Flüchen, Konfetti/Titel-Einblendung bei Stufenaufstieg, große Siegesanimation bei CTO. Animationen dürfen das Spiel nie länger als ~1,5 s blockieren und sind in den Einstellungen abschaltbar (respektiert `prefers-reduced-motion`).

## 7. Architektur

### 7.1 Projekte (Solution `DeveloperDungeon`)

- `DeveloperDungeon.Engine` – Klassenbibliothek, **keine** Abhängigkeit zu Blazor/ASP.NET. Spielzustand, Befehle, Regeln, Wirkungskatalog, Kartendefinitionen-Laden und -Validierung.
- `DeveloperDungeon.Engine.Tests` – Unit-Tests (xUnit) für Regeln, Kampf, Wirkungen, Kartendaten-Validierung.
- `DeveloperDungeon.Web` – Blazor WebAssembly Standalone, PWA (Service Worker, Manifest, offline). UI, Animationen, Grafiken, lokale `IGameSession`-Implementierung, Speichern im Browser.
- *Später:* `DeveloperDungeon.Server` – ASP.NET Core + SignalR, hostet die Engine; Web-Projekt bekommt eine zweite `IGameSession`-Implementierung.

### 7.2 Engine-Prinzipien

- **Befehle rein, Ereignisse raus:** Die UI schickt Befehle (`OpenTicket`, `PlayCard`, `EquipItem`, `OfferHelp`, `ResolveCombat`, `RunAway`, `ManualAdjustLevel` …). Die Engine prüft sie gegen den Zustand, wendet sie an und liefert Ereignisse (`TicketOpened`, `CombatStarted`, `LevelChanged`, …) und den neuen Zustand.
- **Regelverstöße werden hart blockiert** (Entscheidung des Nutzers): z. B. Item mit Einschränkung „nur für QA“ ohne passende Abteilung, belegter Slot, Karte in falscher Phase, Stufe 10 ohne Kampf. Ungültige Befehle liefern eine verständliche, gern humorvolle Ablehnung („Nur für QA. Du bist Cloud Platform.“), und die UI bietet regelwidrige Aktionen möglichst gar nicht erst an (ausgegraut mit Begründung). Einzige Ausnahme sind die Befehle des Werkzeugkastens (5.7): Sie sind bewusst nicht regelgeprüft, damit der Tisch Fehler und Hausregeln korrigieren kann. Ein „Rückgängig“-Button ist in Version 1 nicht vorgesehen *(Entscheidung des Bauenden; Fehler korrigiert der Werkzeugkasten)*.
- **Zustand ist vollständig serialisierbar** (JSON) – Voraussetzung für Speichern im Browser und spätere Server-Synchronisation.
- **Zufall injizierbar:** Mischen und Würfeln über eine austauschbare Zufallsquelle mit Seed (deterministische Tests; später würfelt der Server).
- **Spielersicht:** Die Engine kann pro Spieler eine Sicht erzeugen, in der fremde Handkarten verborgen sind. Lokal wird sie für den „Gerät übergeben“-Ablauf genutzt; später schickt der Server nur diese Sicht an jeden Client.
- **Zeit/Countdown** gehört nicht in die Engine; der 5-Sekunden-Countdown ist UI-Logik, die danach `ResolveCombat` bestätigt.

### 7.3 `IGameSession` (Grenze zwischen UI und Spiellogik)

Sinngemäß: aktuellen (Spieler-)Zustand abfragen, Befehl senden (asynchron, Ergebnis = angenommen/abgelehnt), Ereignis „Zustand geändert“ abonnieren. Lokal: direkter Aufruf der Engine im Browser. Später: SignalR-Aufruf an den Server.

### 7.4 Hosting und Deployment (Version 1)

- **GitHub Pages** (Entscheidung des Nutzers), damit die PWA per HTTPS auf Tablet/Handy installiert und offline genutzt werden kann.
- Deployment per **GitHub Actions** bei jedem Push auf `main`: `dotnet publish` des Web-Projekts, Ausgabe nach GitHub Pages.
- Zu beachten: `<base href>` auf den Repo-Pfad setzen (aktuell `/Developer-Munchkin/`, im Build-Schritt ersetzen, lokal `/`), `.nojekyll`-Datei, `404.html`-Fallback für Blazor-Routing, Service-Worker-Scope und Manifest-`start_url` passend zum Base-Pfad.
- **Voraussetzung auf Nutzerseite:** GitHub Pages braucht ein öffentliches Repo oder einen bezahlten GitHub-Plan. Eine Pages-Seite ist in beiden Fällen öffentlich erreichbar, wer die URL kennt, kann spielen.

### 7.5 Speichern

- Laufendes Spiel wird nach jeder Aktion in `localStorage` (oder IndexedDB, falls zu groß) gespeichert. Beim Start: „Laufendes Spiel fortsetzen“ oder „Neues Spiel“.
- Einstellungen (Animationen an/aus, Countdown-Länge) ebenfalls lokal.

## 8. Oberfläche (Version 1)

- **Startbildschirm:** Logo „Developer Dungeon“, Neues Spiel / Fortsetzen / Regeln / Kartengalerie / Einstellungen.
- **Spieler-Setup:** 3–6 Spieler, Name + Farbe.
- **Spieltisch (quer, Tablet/Laptop):**
  - Oben: Spielerleiste – je Spieler Avatar, Name, Stufe + Titel, Abteilung/Sprache-Icons, Kampfstärke, Handkartenanzahl.
  - Mitte: Ticket- und Loot-Stapel mit Ablagen, Kampfbereich (Monster, Verstärker, Spieler/Helfer, Stärkevergleich groß).
  - Unten: Ausrüstung des aktiven Spielers (Slots) und Hand (nur nach „Gerät übergeben“ sichtbar).
  - Seitlich/ausklappbar: Spielprotokoll, Werkzeugkasten.
  - Kampf: Eingreif-Leiste mit allen Mitspielern, „Kampf auswerten“, Countdown.
- **Gerät übergeben:** Vollbild „Gib das Gerät an Anna“ mit Tippen-zum-Aufdecken, wann immer eine andere Hand sichtbar würde.
- **Smartphone:** gleicher Inhalt gestapelt, Hand als horizontal wischbare Leiste, Protokoll/Werkzeugkasten als Unterseiten.
- **Kartengalerie:** alle Karten ansehen (gut zum Gegenlesen und Testen der Grafiken).
- **Karte vergrößern:** Tippen/Halten zeigt die Karte groß mit vollem Text und Animation.

## 9. Qualität und Tests

- Engine-Regeln test-getrieben (xUnit): Zugphasen, Kampfberechnung, Gleichstand, Flucht (mit festem Seed), Hilfe/Loot-Verteilung, Handlimit/Spende, Stufe-10-Regel, jede Wirkung im Katalog.
- Ein Test validiert **alle Kartendaten**: Pflichtfelder, gültige Wirkungen, Grafik vorhanden, Ids eindeutig.
- Simulations-Test (optional, hilft beim Balancing): zufällige Partien mit einfacher Bot-Logik durchspielen und prüfen, dass Partien enden und durchschnittliche Länge plausibel ist.

## 10. Spätere Phasen (nicht Version 1)

1. **Online-Mehrspieler:** `DeveloperDungeon.Server` (ASP.NET Core, SignalR), Räume per Code, jeder Spieler auf eigenem Gerät, Server ist maßgeblich. Hosting-Frage wird dann geklärt.
2. Kartenpool auf 150+ erweitern, ggf. Erweiterungs-Sets.
3. Sound-Effekte, Bots, Statistiken.

## 11. Dem Bauenden überlassen

- Konkrete Ordnerstruktur innerhalb der Projekte, Benennung von Klassen.
- Format der Kartendaten (JSON-Dateien vs. C#-Definitionen), solange Karten reine Daten bleiben und validiert werden.
- Wahl von CSS-Ansatz und ob eine UI-Komponentenbibliothek verwendet wird (Empfehlung: keine, eigenes CSS passt besser zum Comic-Stil).
- Konkrete Kartentexte, Gags und Illustrationen (werden dem Nutzer zum Gegenlesen vorgelegt).
- Genaue Animationstechnik (CSS-Animationen bevorzugt).
