Część epiku #75 (faza 4). Integracja UI (#81, #82) z backendem (#76–#80, PR #85–#89 zmergowane do main). Oparte na #91.

## Zakres

- `playwright.real.config.ts` + `npm run test:e2e:real`: testy e2e na prawdziwym API (docker compose, `NUXT_PUBLIC_API_BASE=http://localhost:5000`, mocki Buyer home / Merchant feed wyłączone).
- `tests/e2e-real/posts.real.spec.ts` (chromium + Mobile Chrome):
  - formularz -> szczegóły -> zero-match (job w tle) -> przedłużenie -> "Znalazłem" -> zakładka Zakończone,
  - 409 przy zamykaniu zapytania zakończonego gdzie indziej,
  - zapytanie w subskrybowanym tagu: sprzedawca powiadomiony, brak popupu zero-match.
- `docs/testing.md`: sekcja o testach z prawdziwym API.

## Wynik integracji

Kontrakt backendu (`/api/posts`, `/status`, `/fulfil`, `/close`, `/long-lived`) zgadza się z typami w `usePostsApi` – zmian w kodzie UI nie było. Smoke test przez HTTP: create 201, list, status Pending -> Dispatched, long-lived 200, fulfil 204, close po fulfil 409, brak tytułu 400.

Testy z prawdziwym API: 6/6 przechodzą.

## Uwagi

- Tokeny Dev (mock accounts) są podpisane sekretem z `.env.example`; serwis uruchomiony z innym `JWT_SECRET` odpowiada 401 (lokalnie ustawiłem sekret tylko w powłoce przy `docker compose up`).
- API przepuszcza CORS tylko dla `http://localhost:3000`, więc config e2e startuje własny serwer na tym porcie.
