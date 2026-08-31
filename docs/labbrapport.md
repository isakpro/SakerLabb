# Labbrapport: praktisk laboration

*Kunskapskontroll 2, IT-säkerhet för utvecklare.*

**Namn:** Isak Ramstedt
**Datum:** 2026-08-25
**Repo (länk till din fork):** https://github.com/isakpro/SakerLabb — branch `sakerhetsanalys`
**Applikation som analyserades:** SakerLabb Support 1.4.2 (.NET 10, Blazor, SQLite)

---

## 1. Kort om applikationen och analysen

SakerLabb Support är ett ärendehanteringssystem för intern IT-support. Kunder anmäler ärenden, agenter och administratörer hanterar dem, och systemet lagrar utöver ärenden och kommentarer också användarkonton med e-post, personnummer och lösenordshashar. Appen kördes lokalt med `dotnet run --project SakerLabb.Web` på `http://localhost:5080`.

Den statiska analysen gjordes med **GitHub code scanning, default setup, språk C#**. Baslinjen är körning `1646766283` från 2026-08-20, som gav **36 larm**. Den dynamiska gjordes med **OWASP ZAP 2.17**, körd headless via Automation Framework: 21 seedade URL:er, 71 spidrade adresser och därefter **både passiv och aktiv** skanning, vilket gav **18 larmtyper**. Körplanen ligger i repot, så att exakt samma skanning kan köras om — det är den upprepbarheten som gör verifieringen i avsnitt 4 meningsfull. Aktiv skanning uteslöts mot `/diagnostik/ping`, `/api/diag`, `/api/preview` och `/import/fetch`, som startar `cmd.exe`-processer respektive utgående anrop; kommandoinjektionen där täcks av fynd 2.

---

## 2. Fem fynd

| Nr | Källa (CodeQL/ZAP) | Regel-id eller alert | Allvarlighet (+ confidence för ZAP) | Fil och rad eller URL | Verkligt eller falskt positivt | Motivering |
|----|--------------------|----------------------|--------------------------------------|------------------------|--------------------------------|------------|
| 1 | CodeQL | `cs/sql-injection`, larm 2 | High | `Data/UserRepository.cs:21` | **Verkligt** | Inloggningsfrågan byggdes med strängkonkatenering: `' OR '1'='1' --` kommenterar bort lösenordskontrollen och loggar in som admin. ZAP bekräftade oberoende utifrån. 12 larm av samma typ. |
| 2 | CodeQL | `cs/command-line-injection`, larm 14 | **Critical** | `Services/ImportService.cs:57` | **Verkligt** | Värdnamnet konkatenerades in i `cmd.exe /c ping -n 2 <host>`, och cmd tolkar `&` som separator. `localhost & whoami` ger kodkörning som webbservern, utan inloggning. |
| 3 | ZAP | XSS, reflekterad (40012) och lagrad (40014) | High, conf. Medium | `/api/echo?q=`, `/tickets?search=`, POST `/ticket/comment` | **Verkligt** | Fyra vyer castade indata till `MarkupString` och stängde av Razors kodning. Den lagrade är värst: en kommentar körs hos alla som öppnar ärendet. CodeQL flaggade samma sidor. |
| 4 | ZAP | CSP Header Not Set (10038) | Medium, conf. **High** | Alla HTML-svar, t.ex. `/`, `/login` | **Verkligt** | Inget svar satte CSP, `X-Frame-Options` eller `nosniff`, så inget andra lager fanns om en XSS slinker igenom. Servern läckte ramverksversion via `X-Powered-By`. |
| 5 | ZAP | Format String Error (30002) | Medium, conf. Medium | POST `/account/login`, param `username` | **Falskt positivt** | Regeln letar efter printf-formatsträngar, ett C-problem; koden har inga `string.Format`-anrop med indata och all loggning använder konstanta mallar. ZAP:s bevisfält är tomt i båda körningarna, så larmet bygger på en svarsskillnad. Ingen åtgärd. |

**Bevis (utdrag), numrerade efter fyndet ovan:** se **bilagan** sist i rapporten, där varje fynd har ett ordagrant utdrag ur verktygets egen rapport, före och efter. Rådata ligger i repot under `docs/bevis/`: `codeql-alerts-fore.json`/`.tsv` (36 larm före åtgärd), `zap-fore.html`/`.json` (18 larmtyper), `zap-efter.html`/`.json` (10 larmtyper) och `zap-plan.yaml` (körplanen). Larmen kan även läsas direkt, t.ex. `https://github.com/isakpro/SakerLabb/security/code-scanning/2`.

---

## 3. Prioritering

Jag vägde tre faktorer mot varandra: allvarlighetsgrad, exponering och utnyttjbarhet.

**Först fynd 2, kommandoinjektionen.** Den är den enda som låter en angripare lämna applikationen. De övriga ger data eller sessioner; den här ger kommandokörning på värden, alltså filer utanför appen, kvarstående åtkomst och vidare rörelse i nätverket. Endpointen kräver ingen inloggning och angreppet är ett tecken långt.

**Sedan fynd 1, SQL-injektionen.** Konsekvensen är hela databasen, inklusive personnummer och lösenordshashar, plus förbikoppling av inloggningen. Den ligger efter kommandoinjektionen enbart därför att skadan stannar inom applikationen; exponeringen är lika stor, eftersom `/tickets` svarar utan inloggning.

**Därefter fynd 3, den lagrade XSS:en.** Den kräver att angriparen får posta en kommentar, men koden körs sedan hos varje läsare. Att sessionskakan samtidigt saknade `HttpOnly` gjorde den värre: en lagrad XSS blir ett övertaget administratörskonto.

**Fynd 4 sist**, eftersom det inte är en sårbarhet i egen rätt utan ett saknat lager. Det åtgärdades ändå, eftersom det är billigt och begränsar skadan när något annat brister. Fynd 5 kräver ingen åtgärd. Hade jag bara hunnit med en enda hade det varit kommandoinjektionen.

---

## 4. Åtgärder

### Åtgärd 1

```
Fynd 1:      cs/sql-injection, larm 2 av 12, High (CodeQL)
Plats:       Data/UserRepository.cs rad 21, samt TicketRepository.cs
Bevis före:  codeql-alerts-fore.tsv, 12 larm; ZAP 40018: near ";": syntax error
Bedömning:   verkligt, ' OR '1'='1' -- loggade in som admin, båda verktygen ense
Åtgärd:      parameteriserat med SqliteParameter, ORDER BY vitlistat eftersom
             parametrar bara gäller värden och inte identifierare, commit a15cee3
Bevis efter: körning 1668002768, alla 12 larmen står som Fixed, ZAP-larmet borta
```

### Åtgärd 2

```
Fynd 2:      cs/command-line-injection, larm 14, Critical (CodeQL)
Plats:       Services/ImportService.cs rad 57
Bevis före:  larm 14 i fliken Security, samt codeql-alerts-fore.tsv
Bedömning:   verkligt, /diagnostik/ping nås utan inloggning och cmd.exe tolkar &
Åtgärd:      skalanropet ersatt med System.Net.NetworkInformation.Ping, som inte
             går via en kommandotolk, commit 027ace3
Bevis efter: körning 1668002768, larm 14 står som Fixed; ?host=localhost & whoami
             ger nu "Ogiltigt värdnamn"
```

### Åtgärd 3

```
Fynd 3:      XSS reflekterad (40012, 5 inst.) och lagrad (40014, 1 inst.),
             High / Medium confidence (ZAP)
Plats:       /api/echo?q=, /tickets?search=, POST /ticket/comment
Bevis före:  zap-fore.html, </h2><scrIpt>alert(1);</scRipt><h2> kom tillbaka okodad
Bedömning:   verkligt, fyra vyer castade indata till MarkupString och stängde av
             Razors automatiska kodning
Åtgärd:      MarkupString borttaget så Razor kodar utdata, /api/echo använder
             HtmlEncoder, commit 6f181cd
Bevis efter: ny ZAP-körning, 40012 och 40014 borta ur zap-efter.html; CodeQL
             cs/web/xss står som Fixed
```

### Åtgärd 4

```
Fynd 4:      CSP Header Not Set (10038), Medium / High confidence (ZAP)
Plats:       samtliga HTML-svar, t.ex. http://localhost:5080/
Bevis före:  zap-fore.html, tre instanser; 10037: X-Powered-By: SakerLabb 1.4.2
Bedömning:   verkligt, inga säkerhetsheaders sattes någonstans i appen
Åtgärd:      mellanvara sätter CSP, X-Frame-Options, nosniff och Referrer-Policy,
             X-Powered-By borttagen, commit 7cd275d
Bevis efter: zap-efter.html, larmen 10038, 10020, 10021 och 10037 borta
```


**Verifiering.** CodeQL gick från 36 larm till 15, ZAP från 18 larmtyper till 10. Av de fem larmtyper som hade High i baslinjen är fyra borta; kvar står Path Traversal, se bortval 3. Två saker är värda att notera. CodeQL rapporterar 22 larm som Fixed, men larm 32 (`cookie-secure-not-set`) stängdes enbart därför att jag ändrade raderna omkring det, och samma brist öppnades direkt igen som **larm 37** — rätt siffra är alltså **21 genuint åtgärdade**. Omkörningen gav dessutom ett nytt larm, Application Error Disclosure, som jag spårade till `AuthService.Current()`: en trasig `sl_session`-kaka ger 500 med stacktrace. Buggen fanns hela tiden men doldes av bruset från SQL-injektionerna.

---

## 5. Eventuella bortval

**1. `Secure`-flaggan på sessionskakan** (CodeQL larm 37, Medium). *Risk:* kakan skickas i klartext och kan avlyssnas. *Motiv:* labbmiljön kör HTTP mot localhost, så `Secure = true` hade gjort appen obrukbar för den som rättar. *Kompenserande kontroll:* `HttpOnly` och `SameSite = Lax` sattes på kakan i commit `4ae049f`, vilket också fick CodeQL larm 33 att stå som Fixed. *Omprövning:* `Secure` sätts till `true` samma dag appen ställs bakom TLS, senast 2026-09-30.

**2. Anti-CSRF-tokens** (ZAP larm 10202, Medium, fem instanser). *Risk:* en inloggad användare kan luras skicka ett formulär omedvetet. *Motiv:* kräver tokens i sex formulär och validering i tre controllers. *Kompenserande kontroll:* `SameSite = Lax`. **Reservation:** larmet försvann ur ZAP-rapporten efter att `SameSite` sattes till `Lax`, men inte för att jag åtgärdade det — formulären saknar fortfarande tokens, och en POST till `/account/login` utan token ger 302, alltså lyckad inloggning. Jag tar därför inte åt mig äran. *Omprövning:* 2026-09-30.

**3. `cs/path-injection`** (larm 36, High, `FileService.cs:19`). *Risk:* `/api/attachment?name=` läser godtyckliga filer; bekräftat med `?name=c:/Windows/system.ini`. *Motiv:* färre åtgärder med ordentlig verifiering framför fler med tunn bevisning. Fyndet blev det enda som blev *tydligare* efteråt — ZAP höjde sin confidence från Low till Medium när bruset försvann. *Kompenserande kontroll:* saknas, vilket är skälet till att detta ligger först i åtgärdslistan. *Omprövning:* omgående, senast 2026-09-08.

**Övrigt kvarstående.** 15 CodeQL-larm står kvar: `unvalidated-url-redirection` (4), `log-forging` (5), `ecb-encryption` (2), `path-injection` (1), `unsafe-deserialization` (1), `insecure-dtd-handling` (1) och `cookie-secure-not-set` (1). Byggloggen visar även en känd sårbarhet i det transitiva beroendet `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, som saknar Dependabot-uppdatering; `Newtonsoft.Json` och `SixLabors.ImageSharp` uppdaterades till versioner utan kända sårbarheter.

---


## Bilaga: rapportutdrag per fynd

Ordagranna utdrag ur verktygens egna rapporter. Fullständiga rapporter i `docs/bevis/`.

```
Fynd 1 — CodeQL cs/sql-injection, larm 2, high, Data/UserRepository.cs:21
  FÖRE   "SQL query built from user-controlled sources"
         ZAP 40018, High/Medium: ?ticketId=;  evidence: near ";": syntax error
  EFTER  state: fixed 2026-08-25, samtliga 12 larm. ZAP-larmet borta.

Fynd 2 — CodeQL cs/command-line-injection, larm 14, critical, ImportService.cs:57
  FÖRE   "This command line depends on a user-provided value."
  EFTER  state: fixed 2026-08-25.

Fynd 3 — ZAP XSS Reflected (40012) High/Medium 5 inst., Persistent (40014) 1 inst.
  FÖRE   /api/echo?q=  attack och evidence: </h2><scrIpt>alert(1);</scRipt><h2>
  EFTER  båda borta ur zap-efter.html. CodeQL cs/web/xss: fixed.

Fynd 4 — ZAP CSP Header Not Set (10038), Medium/High, 3 inst.
  FÖRE   http://localhost:5080/ — headern saknas helt
         10037 evidence: X-Powered-By: SakerLabb 1.4.2 (ASP.NET Core 10.0)
  EFTER  10038, 10020, 10021 och 10037 borta ur zap-efter.html.

Fynd 5 — ZAP Format String Error (30002), Medium/Medium, 1 inst.
  FÖRE   POST /account/login, param username, attack ZAP%n%s%n%s...
         evidence: (tomt)
  EFTER  kvarstår, 2 inst. Bedömt falskt positivt, ingen åtgärd gjord.
```
