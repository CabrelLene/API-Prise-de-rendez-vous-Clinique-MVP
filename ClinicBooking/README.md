# ClinicBooking API (MVP) — .NET 8 + PostgreSQL (Neon) + Render

API de prise de rendez-vous pour une clinique (MVP) avec :
- Auth par **API Key** (`X-API-KEY`)
- **Rate limiting** (pré-auth + global + policy)
- PostgreSQL (Neon / Supabase)
- Swagger (docs)
- Endpoints publics: `/`, `/health`, `/version`

## Démo (Render)
Base URL: https://api-prise-de-rendez-vous-clinique-mvp.onrender.com

Endpoints publics (pas de clé):
- GET `/`
- GET `/health`
- GET `/version`

Endpoints protégés (clé requise):
- GET `/appointments?page=1&pageSize=10`
- ... (ajoute ceux que tu as)

---

## Sécurité (minimal mais sérieux)
- Toutes les routes métier exigent `X-API-KEY`
- Les endpoints publics sont limités à: `/`, `/health`, `/version`
- Rate limit:
  - Clé invalide => **429** après un certain volume
  - Clé valide => quotas distincts
- Erreurs JSON normalisées (`code`, `message`, `errors`...)

> ⚠️ Ne jamais commit une vraie clé ni une vraie connection string.

---

## Prérequis
- .NET SDK 8
- Une base PostgreSQL (local ou Neon)

---

## Configuration (variables d’environnement)

### Option A — Local + DB Neon (recommandé pour être identique à prod)
Définir :
- `ConnectionStrings__Default` = connection string Npgsql (format **Host=...;Username=...;Password=...;Ssl Mode=Require;...**)
- `ApiKey__HeaderName` = `X-API-KEY` (optionnel)
- `ApiKey__Keys__0` = ta clé locale (ex: `dev-secret-123`)
- `ApiKey__Keys__1` = autre clé (optionnel)

#### Git Bash (Windows)
```bash
export ConnectionStrings__Default="Host=...;Database=...;Username=...;Password=...;Ssl Mode=Require;"
export ApiKey__Keys__0="dev-secret-123"
