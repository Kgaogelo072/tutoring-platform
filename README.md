# Engineered Tutors — web

Landing page, privacy policy and (soon) the registration + diagnostic flow for Engineered Tutors.
Angular 21 with SSR and prerendering.

## Run locally

```bash
npm install
npm start            # dev server on http://localhost:4200
npm run build        # production build (prerenders /, /diagnostic, /privacy)
```

Angular 21 runs on Node 22.22.0+. Angular 22 needs Node 22.22.3+ or 24.15+.

## Where things live

| What | Where |
|---|---|
| All page copy, prices, contact details | `src/app/content/site-content.ts` |
| Brand colours and fonts | `src/styles.scss` (`:root` tokens) |
| Landing page | `src/app/pages/home/` |
| Diagnostic page (placeholder until the API is ready) | `src/app/pages/diagnostic/diagnostic.ts` |
| Privacy policy (draft) | `src/app/pages/privacy/privacy.ts` |
| Header, footer, WhatsApp button, icons | `src/app/shared/` |
| Images (WebP, 640/1280 widths) | `public/img/` |
| robots.txt, sitemap.xml | `public/` |

## Before going live

- Fill every `TODO` in `site-content.ts` (WhatsApp number, email, domain).
- Add a real photo of yourself and set `tutor.photo`.
- Replace `engineeredtutors.co.za` in `src/index.html`, `public/robots.txt` and `public/sitemap.xml` if the domain differs.
- Have the privacy policy reviewed.

## Notes

- Font inlining is disabled in `angular.json` (`optimization.fonts: false`) so builds work without network access. Turn it back on in CI for slightly faster first paint.
- All prerendered routes are configured in `src/app/app.routes.server.ts`.

## Next (Phase 1)

1. ASP.NET Core API: registration, attempts, scoring, question import.
2. Registration form on `/diagnostic` (REG-01 to REG-08).
3. Test-taking UI at `/d/:token` with KaTeX (DX-01 to DX-13).
4. Admin area.
