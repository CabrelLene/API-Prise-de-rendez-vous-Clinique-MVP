# ClinicBooking API (MVP) — .NET 8 + PostgreSQL + Render

API de prise de rendez-vous pour clinique (MVP) construite en **ASP.NET Core (.NET 8)**, **EF Core**, **PostgreSQL (Neon)**, déployée sur **Render**.
Objectif : démontrer une API propre, documentée, avec auth minimale, rate limit, et endpoints de health pour un portfolio.

## ✅ Live
- Base URL (prod) : https://api-prise-de-rendez-vous-clinique-mvp.onrender.com
- Swagger : `/swagger`
- Endpoints publics :
  - `GET /` (infos)
  - `GET /health` (healthcheck)
  - `GET /version` (env + commit si Render)

## 🔐 Sécurité (minimaliste mais sérieuse)
- **API Key obligatoire** sur les endpoints métiers (ex: `/appointments`)
- Header attendu : `X-API-KEY`
- Rate limiting (anti-abus)
- Gestion d’erreurs JSON uniforme

> ⚠️ L’API Key n’est pas une “auth utilisateur”. C’est une protection simple pour un MVP et un portfolio.  
> Pour du production-grade : OAuth/JWT + rôles + audit + rotation des clés.

## 🧪 Quick tests (curl)
### 1) Vérifier que le service répond (public)
```bash
curl -i "https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/"
curl -i "https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/health"
curl -i "https://api-prise-de-rendez-vous-clinique-mvp.onrender.com/version"
