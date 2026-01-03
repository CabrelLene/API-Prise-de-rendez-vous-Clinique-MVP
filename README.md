![CI](https://github.com/CabrelLene/<API-Prise-de-rendez-vous-Clinique-MVP/actions/workflows/ci.yml/badge.svg)

# ClinicBooking API (MVP) — Prise de rendez-vous clinique

API REST en .NET 8 + PostgreSQL (EF Core) pour gérer des rendez-vous de clinique, avec :
- **API Key auth** (header `X-API-KEY`)
- **Rate limiting** (global + policy endpoints sensibles)
- **Migrations EF Core** + seed contrôlé
- **Endpoints publics** (portfolio-friendly) : `/`, `/health`, `/version`
- **Swagger** pour la doc interactive
- **Tests xUnit** (smoke + rate limit) + CI GitHub Actions (optionnel mais recommandé)

## ✅ Live (Render)
Base URL :
- `https://api-prise-de-rendez-vous-clinique-mvp.onrender.com`

Endpoints publics (sans clé) :
- `GET /`
- `GET /health`
- `GET /version`

Swagger :
- `GET /swagger`

> Les endpoints métiers (ex: `/appointments`) nécessitent une API Key.

---

## 🔐 Auth — API Key

Header attendu :
- `X-API-KEY: <YOUR_KEY>`

Erreurs possibles :
- `401 API_KEY_MISSING` : header absent
- `403 API_KEY_INVALID` : header présent mais clé invalide
- `429 RATE_LIMITED` : trop de requêtes

---

## ⚡ Rate limiting (résumé)
- Global : limite “raisonnable” par clé
- Policy `appointments-10rpm` : 10 requêtes/minute (exemple)
- PreAuth limiter (anti-abus) : limite IP même si la clé est invalide/absente

Objectif : empêcher un spam basique sans complexifier le MVP.

---

##  Quickstart (local)

### Prérequis
- .NET SDK 8
- PostgreSQL (option 1) ou une DB distante (option 2)

### 1) Configuration (appsettings.json)
Par défaut, le projet contient :
- `ApiKey.Keys`: `dev-secret-123`, `dev-secret-456`
- ConnectionString locale : `Host=localhost;Port=5432;Database=clinicbooking;Username=clinic;Password=clinicpass`

### 2) Lancer l’API
Depuis la racine `ClinicBooking/` :

```bash
dotnet run --project ClinicBooking.Api

##VERSION
https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/version
####Health check
https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/health
##Live API (racine)
https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/
##Swagger / Docs
https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/swagger
