# Engineered Tutors — web

Landing page, privacy policy, parent registration, the timed diagnostic and the tutor's admin area for Engineered
Tutors. Phase 1 scope is defined in the Diagnostic MVP Requirements v1.2; requirement IDs (REG-07, DX-11, …) are used
in code comments and test names.

- **Web:** Angular 21 with SSR. Landing pages are prerendered; the test (`/d/:token`) and admin (`/admin`) render in
  the browser.
- **API:** ASP.NET Core 8 + EF Core on SQL Server (Azure SQL in production), in [`api/`](api/).

## Run locally

You need Node 20.19+ (22 recommended), the .NET 8 SDK and SQL Server LocalDB (comes with Visual Studio or the SQL
Server Express installer).

```bash
# 1. API on http://localhost:5080
cd api
cp .env.example .env        # then fill in the values (see the comments in the file)
cd src/EngineeredTutors.Api
dotnet run --launch-profile http

# 2. Web on http://localhost:4200 (proxies /api to the API)
npm install
npm start
```

On first run in Development the API applies migrations and, if `Seed__MathsCsv` / `Seed__PhysicalSciencesCsv` point
to the question CSVs, imports and publishes both diagnostics. The CSVs hold the answers, so keep them out of git.

- Registration: <http://localhost:4200/diagnostic>
- Emails are written to `api/src/EngineeredTutors.Api/mail-pickup/` instead of being sent; open one to get the test
  links.
- Admin: <http://localhost:4200/admin>. Locally, sign in with `AdminAuth__DevKey` from `api/.env`.

### Tests

```bash
cd api
dotnet test        # 101 tests, including every section 8 acceptance criterion, against a throwaway LocalDB database
```

Set `ET_QUESTIONS_DIR` to the folder with the v1.1 CSVs to also validate the real question files.

## Where things live

| What | Where |
|---|---|
| All page copy, prices, contact details | `src/app/content/site-content.ts` |
| API URL, Turnstile site key, Entra sign-in settings | `src/app/content/app-settings.ts` |
| Brand colours and fonts | `src/styles.scss` (`:root` tokens) |
| Landing page | `src/app/pages/home/` |
| Registration form | `src/app/pages/diagnostic/` |
| Test page and question view | `src/app/pages/test/` |
| Admin area | `src/app/admin/` |
| Privacy policy (draft) | `src/app/pages/privacy/privacy.ts` |
| Header, footer, WhatsApp button, icons, KaTeX rendering | `src/app/shared/` |
| Images (WebP, 640/1280 widths) | `public/img/` |
| Question images (named as in the CSV `image_ref`) | `public/question-images/` |
| robots.txt, sitemap.xml | `public/` |
| API: registration, test engine, admin | `api/src/EngineeredTutors.Api/Features/` |
| API: data model and migrations | `api/src/EngineeredTutors.Api/Data/` |
| API: settings (time limits, bands, grace period…) | `api/src/EngineeredTutors.Api/appsettings.json` |

## Configuration (API)

Set these as App Service settings in Azure, with secrets as Key Vault references. `api/.env.example` documents each.

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Default` | Azure SQL (South Africa North) |
| `Security__EncryptionKey`, `Security__IdHashKey` | Two different random 32-byte keys (base64). Never change them once data exists. |
| `Site__BaseUrl` | Public web URL used in test links |
| `Site__AdminAlertEmail` | Where completion alerts go |
| `Email__*` | SMTP settings (`Email__Mode=Smtp`) |
| `Turnstile__SecretKey` | Cloudflare Turnstile secret |
| `AdminAuth__TenantId`, `AdminAuth__ApiClientId`, `AdminAuth__AllowedObjectIds__0` | Entra ID: tenant, API app registration, and the tutor's account object ID |
| `Cors__Origins__0` | The web origin, if the API is on a different host |

Scoring bands (`Scoring:StrongMinPct`, `Scoring:DevelopingMinPct`), default time limits, the 30-second grace period,
token lifetime and the per-IP registration limit are in `appsettings.json`.

## Before going live

- Fill every `TODO` in `site-content.ts` (WhatsApp number, email, domain) and `app-settings.ts` (API URL, Turnstile
  site key, Entra IDs).
- Add a real photo of yourself and set `tutor.photo`.
- Replace `engineeredtutors.co.za` in `src/index.html`, `public/robots.txt` and `public/sitemap.xml` if the domain
  differs.
- Add `geo_m24.png` (Maths question M24) to `public/question-images/`.
- Have the privacy policy reviewed. If its version changes, update `Site:ConsentTextVersion` in the API to match.
- Entra ID: register an API app (expose a scope such as `access_as_admin`) and a SPA app (redirect URI
  `https://<site>/admin`) that requests that scope.

## Notes

- Font inlining is disabled in `angular.json` (`optimization.fonts: false`) so builds work without network access. Turn
  it back on in CI for slightly faster first paint.
- Render modes per route are in `src/app/app.routes.server.ts`.
- KaTeX (script, stylesheet and fonts) loads only on the test and admin pages, so landing pages don't pay for it.
- Only a SHA-256 of each test token is stored, so "resending" links to a parent who registers again issues new
  tokens and the old links stop working. Links issued in the last 15 minutes are kept, so a double-tapped submit
  doesn't break the links on screen.
