# Guide de Déploiement — FootballPrediction 1X2

Documentation complète pour le déploiement de l'application FootballPrediction en environnement de production.

## Table des matières

- [Prérequis](#prérequis)
- [Architecture de déploiement](#architecture-de-déploiement)
- [Déploiement Docker (recommandé)](#déploiement-docker-recommandé)
- [Déploiement bare-metal](#déploiement-bare-metal)
- [Configuration](#configuration)
- [Utilisation de la CLI](#utilisation-de-la-cli)
- [Volumes et persistance](#volumes-et-persistance)
- [Monitoring et santé](#monitoring-et-santé)
- [CI/CD](#cicd)
- [Sécurité](#sécurité)
- [Troubleshooting](#troubleshooting)

---

## Prérequis

### Déploiement Docker

- **Docker Engine** ≥ 24.0
- **Docker Compose** ≥ v2.20
- **RAM** : 2 Go minimum, 4 Go recommandés
- **CPU** : 2 cœurs minimum (l'inférence ML est CPU-only)
- **Espace disque** : 1 Go pour l'image Docker + volume pour les modèles

### Déploiement bare-metal

- **.NET 8 SDK** (build) ou **.NET 8 Runtime** (exécution seule)
- **OS supportés** :
  - Windows 10+ / Windows Server 2019+
  - Ubuntu 22.04+ / Debian 12+
  - macOS 13+ (Ventura)

---

## Architecture de déploiement

```
┌─────────────────────────────────────────────────┐
│                  Client HTTP                     │
│            (navigateur, API, curl)               │
└─────────────────┬───────────────────────────────┘
                  │ :7575
┌─────────────────▼───────────────────────────────┐
│           Reverse Proxy (optionnel)              │
│         nginx / Caddy / Traefik / IIS            │
└─────────────────┬───────────────────────────────┘
                  │ :7575 (interne)
┌─────────────────▼───────────────────────────────┐
│          FootballPrediction.Web                  │
│          ASP.NET Core 8 MVC + API                │
│                                                  │
│  ┌───────────────────────────────────────────┐  │
│  │           MatchPredictor                   │  │
│  │  (ML.NET — SdcaMaximumEntropy)            │  │
│  │  Modèles : models/model_nn.bin            │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  Endpoints :                                     │
│  • GET  /            → Page d'accueil            │
│  • GET  /Prediction  → Formulaire prédiction     │
│  • POST /Prediction  → Résultat prédiction       │
│  • GET  /Batch       → Formulaire batch          │
│  • POST /Batch       → Résultat batch            │
│  • POST /api/predict → API JSON (programmatique) │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│          FootballPrediction.Cli                  │
│  (entraînement hors-ligne, prédictions batch)    │
│                                                  │
│  Commandes :                                     │
│  • train    → Entraîner un modèle                │
│  • predict  → Prédictions depuis un CSV          │
│  • compare  → Comparer les algorithmes           │
│  • tune     → Optimisation hyperparamètres       │
└─────────────────────────────────────────────────┘
```

**Deux modes de déploiement** :

| Mode | Composant | Usage |
|------|-----------|-------|
| **Web** | `FootballPrediction.Web` | Interface utilisateur + API REST |
| **CLI** | `FootballPrediction.Cli` | Entraînement hors-ligne, prédictions batch |

---

## Déploiement Docker (recommandé)

Le projet inclut un `Dockerfile` multi-stage et un `docker-compose.yml` prêts pour la production.

### Structure du Dockerfile

| Étape | Image de base | Rôle |
|-------|--------------|------|
| **build** | `mcr.microsoft.com/dotnet/sdk:8.0` | Restauration NuGet + compilation Release |
| **runtime** | `mcr.microsoft.com/dotnet/aspnet:8.0` | Exécution de l'application |

Points clés du Dockerfile :
- **Layer caching** optimisé : les `.csproj` sont copiés avant les sources pour maximiser le cache Docker
- **Utilisateur non-root** : l'application tourne sous l'utilisateur `app`
- **Healthcheck** intégré : `curl http://localhost:7575/` toutes les 30 secondes
- **Port exposé** : `7575`
- **Modèle pré-entraîné** : copié depuis `models/` dans l'image

### Build et exécution avec Docker Compose

```bash
# 1. Cloner le dépôt
git clone <repo-url>
cd ml-foot

# 2. Placer les modèles entraînés dans le dossier models/
#    (les fichiers model_nn.bin, model.zip, model_binary.zip)
mkdir -p models data

# 3. Builder et démarrer
docker compose up -d --build

# 4. Vérifier que le service tourne
docker compose ps
curl http://localhost:7575/
```

### Commandes Docker utiles

```bash
# Démarrer / arrêter
docker compose up -d        # Démarrer en arrière-plan
docker compose down         # Arrêter et supprimer les conteneurs
docker compose restart      # Redémarrer

# Logs
docker compose logs -f web            # Logs en temps réel
docker compose logs --tail=100 web    # 100 dernières lignes

# Vérifier la santé
docker inspect --format='{{.State.Health.Status}}' football-prediction
# Attendu : healthy

# Entrer dans le conteneur (debug)
docker compose exec web /bin/bash
```

### Variables d'environnement Docker

| Variable | Valeur par défaut | Description |
|----------|-------------------|-------------|
| `ASPNETCORE_URLS` | `http://+:7575` | Adresse d'écoute |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environnement (`Production`, `Staging`, `Development`) |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | *(vide)* | Chemin certificat HTTPS |
| `ASPNETCORE_Kestrel__Certificates__Default__Password` | *(vide)* | Mot de passe certificat |

### Build sans Docker Compose

```bash
# Build de l'image
docker build -t football-prediction:latest .

# Exécution
docker run -d \
  --name football-prediction \
  -p 7575:7575 \
  -v $(pwd)/models:/app/models:ro \
  -v $(pwd)/data:/app/data:rw \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --restart unless-stopped \
  football-prediction:latest
```

---

## Déploiement bare-metal

### Windows (IIS)

#### 1. Publication

```powershell
# Publier l'application web
dotnet publish src/FootballPrediction.Web/FootballPrediction.Web.csproj `
  -c Release `
  -o C:\inetpub\FootballPrediction `
  /p:UseAppHost=false
```

#### 2. Configuration IIS

```powershell
# Installer le module ASP.NET Core (si pas déjà fait)
# Télécharger depuis : https://dotnet.microsoft.com/permalink/dotnetcore-windows-runtime-bundle

# Créer le pool d'applications
New-WebAppPool -Name "FootballPrediction" -Force
Set-ItemProperty IIS:\AppPools\FootballPrediction -Name managedRuntimeVersion -Value ""

# Créer le site
New-Website -Name "FootballPrediction" `
  -Port 7575 `
  -PhysicalPath "C:\inetpub\FootballPrediction" `
  -ApplicationPool "FootballPrediction"

# Démarrer le site
Start-Website -Name "FootballPrediction"
```

#### 3. Service Windows (alternative)

```powershell
# Créer un service Windows avec sc.exe
sc.exe create "FootballPrediction" `
  binPath="C:\Program Files\dotnet\dotnet.exe C:\apps\FootballPrediction.Web.dll" `
  start=auto

sc.exe start FootballPrediction
```

### Linux (systemd)

#### 1. Installation du runtime

```bash
# Ubuntu / Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0

# Vérifier
dotnet --version
```

#### 2. Publication

```bash
# Publier en autonome (self-contained)
dotnet publish src/FootballPrediction.Web/FootballPrediction.Web.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o /opt/football-prediction

# Ou publication dépendante du runtime (framework-dependent)
dotnet publish src/FootballPrediction.Web/FootballPrediction.Web.csproj \
  -c Release \
  -o /opt/football-prediction \
  /p:UseAppHost=false
```

#### 3. Service systemd

```bash
sudo tee /etc/systemd/system/football-prediction.service << 'EOF'
[Unit]
Description=FootballPrediction 1X2 Web Service
After=network.target

[Service]
Type=simple
User=www-data
Group=www-data
WorkingDirectory=/opt/football-prediction
ExecStart=/usr/bin/dotnet /opt/football-prediction/FootballPrediction.Web.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_URLS=http://localhost:7575
Environment=ASPNETCORE_ENVIRONMENT=Production

# Sécurité
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/football-prediction/data

[Install]
WantedBy=multi-user.target
EOF

# Activer et démarrer
sudo systemctl daemon-reload
sudo systemctl enable football-prediction
sudo systemctl start football-prediction
sudo systemctl status football-prediction
```

#### 4. Logs

```bash
# Logs applicatifs
sudo journalctl -u football-prediction -f

# Logs des 50 dernières lignes
sudo journalctl -u football-prediction -n 50 --no-pager
```

---

## Configuration

### Fichier de configuration principal

Le fichier `src/FootballPrediction.Web/appsettings.json` contrôle le comportement de l'application :

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ModelSettings": {
    "ModelDirectory": "../../models",
    "BinaryModelPath": "../../models/model_binary.zip",
    "MulticlassModelPath": "../../models/model.zip",
    "DefaultMode": "Binary"
  }
}
```

### Paramètres ModelSettings

| Paramètre | Valeur par défaut | Description |
|-----------|-------------------|-------------|
| `ModelDirectory` | `../../models` | Dossier racine des modèles |
| `BinaryModelPath` | `../../models/model_binary.zip` | Modèle binaire (1/2, sans Nul) |
| `MulticlassModelPath` | `../../models/model.zip` | Modèle multiclasse (1/X/2) |
| `DefaultMode` | `"Binary"` | Mode par défaut (`"Binary"` ou `"Multiclass"`) |

> **Note sur les chemins** : les chemins `../../models` sont relatifs au répertoire de sortie (`bin/Release/net8.0/`).  
> En production Docker, le répertoire de travail est `/app` et les modèles sont dans `/app/models/`.

### Configuration par environnement

Créez des fichiers de configuration par environnement :

```
src/FootballPrediction.Web/
├── appsettings.json              # Configuration de base
├── appsettings.Development.json  # Surcharge développement
├── appsettings.Staging.json      # Surcharge staging
└── appsettings.Production.json   # Surcharge production
```

Exemple `appsettings.Production.json` :

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Error"
    }
  },
  "ModelSettings": {
    "ModelDirectory": "/app/models",
    "BinaryModelPath": "/app/models/model_binary.zip",
    "MulticlassModelPath": "/app/models/model.zip",
    "DefaultMode": "Multiclass"
  }
}
```

---

## Utilisation de la CLI

La CLI permet l'entraînement et les prédictions hors-ligne.

### Commandes disponibles

```bash
# Entraînement
dotnet run --project src/FootballPrediction.Cli -- train \
  --input data/matches.csv \
  --output models/model.zip \
  --trainer sdca \
  --binary

# Prédiction
dotnet run --project src/FootballPrediction.Cli -- predict \
  --model models/model.zip \
  --input data/to_predict.csv \
  --output predictions.csv \
  --threshold 0.5

# Comparaison d'algorithmes
dotnet run --project src/FootballPrediction.Cli -- compare \
  --input data/matches.csv \
  --binary

# Optimisation hyperparamètres
dotnet run --project src/FootballPrediction.Cli -- tune \
  --input data/matches.csv \
  --output models/model.zip
```

### Algorithmes supportés

| Option `--trainer` | Algorithme | Usage |
|-------------------|------------|-------|
| `sdca` *(défaut)* | SDCA Maximum Entropy | Rapide, bon compromis |
| `lbfgs` | L-BFGS Maximum Entropy | Convergence plus lente, parfois plus précis |
| `lightgbm` | LightGBM | Meilleure précision, plus lent |
| `nn` | Neural Network | Réseau de neurones custom |
| `all` | Tous les algorithmes | Comparaison automatique |

### Format CSV d'entrée

| Colonne | Description | Exemple |
|---------|-------------|---------|
| `Date` | Date du match | `2024-08-15` |
| `League` | Championnat | `Premier League` |
| `HomeTeam` | Équipe domicile | `Arsenal` |
| `AwayTeam` | Équipe extérieure | `Chelsea` |
| `HomeGoals` | Buts domicile | `2` |
| `AwayGoals` | Buts extérieur | `1` |
| `Result` | Résultat 1X2 | `1`, `X`, ou `2` |
| `HomeCoach` | Coach domicile | `Mikel Arteta` |
| `AwayCoach` | Coach extérieur | `Mauricio Pochettino` |
| `Bet365_1/X/2` | Cotes Bet365 | `2.10` |
| `Pinnacle_1/X/2` | Cotes Pinnacle | `2.05` |
| `WilliamHill_1/X/2` | Cotes William Hill | `2.15` |

---

## Volumes et persistance

### Structure de volumes recommandée

```
football-prediction/
├── models/             # Volume : modèles entraînés (lecture seule)
│   ├── model_nn.bin
│   ├── model_binary.zip
│   └── model.zip
├── data/               # Volume : données d'entrée et sorties (lecture/écriture)
│   ├── matches.csv      # Données d'entraînement
│   ├── to_predict.csv   # Matchs à prédire
│   └── predictions.csv  # Résultats
└── logs/               # Optionnel : logs persistants
```

### Montage dans docker-compose.yml

```yaml
services:
  web:
    volumes:
      - ./models:/app/models:ro     # Lecture seule
      - ./data:/app/data:rw         # Lecture/écriture
```

### Sauvegarde des modèles

```bash
# Sauvegarder les modèles entraînés
tar -czf models-backup-$(date +%Y%m%d).tar.gz models/

# Restaurer
tar -xzf models-backup-YYYYMMDD.tar.gz
```

---

## Monitoring et santé

### Healthcheck Docker

Le conteneur inclut un healthcheck HTTP :

```yaml
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:7575/ || exit 1
```

**États possibles** :

- `starting` : en cours de démarrage (≤10s)
- `healthy` : l'application répond en HTTP 2xx
- `unhealthy` : 3 échecs consécutifs → Docker peut redémarrer le conteneur

### Vérification manuelle

```bash
# Depuis le host
curl -o /dev/null -s -w "%{http_code}\n" http://localhost:7575/
# Attendu : 200

# Depuis l'intérieur du conteneur
docker compose exec web curl -f http://localhost:7575/
```

### Logs applicatifs

Le logging utilise le framework standard ASP.NET Core. Les logs sont émis sur `stdout` / `stderr`.

Niveaux de log configurables :

| Niveau | Usage recommandé |
|--------|-----------------|
| `Information` | Développement |
| `Warning` | Staging |
| `Error` | Production |

### Métriques (optionnel)

Pour une surveillance avancée, exposer les métriques avec `prometheus-net` :

```bash
# Ajouter le package NuGet
dotnet add src/FootballPrediction.Web package prometheus-net.AspNetCore

# Les métriques seront disponibles sur /metrics
```

---

## CI/CD

### GitHub Actions — Build & Test

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore FootballPrediction.sln

      - name: Build
        run: dotnet build FootballPrediction.sln -c Release --no-restore

      - name: Test
        run: dotnet test FootballPrediction.sln -c Release --no-build --verbosity normal
```

### GitHub Actions — Build & Push Docker

```yaml
# .github/workflows/docker.yml
name: Docker

on:
  push:
    branches: [main]
    tags: ['v*']

jobs:
  docker:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Docker meta
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ghcr.io/${{ github.repository }}
          tags: |
            type=ref,event=branch
            type=semver,pattern={{version}}

      - name: Login to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
```

### Azure DevOps — Pipeline simplifié

```yaml
# azure-pipelines.yml
trigger:
  - main

pool:
  vmImage: ubuntu-latest

steps:
  - task: UseDotNet@2
    inputs:
      version: '8.0.x'

  - script: |
      dotnet restore FootballPrediction.sln
      dotnet build FootballPrediction.sln -c Release --no-restore
      dotnet test FootballPrediction.sln -c Release --no-build
    displayName: 'Build & Test'

  - script: |
      docker build -t football-prediction:$(Build.BuildId) .
    displayName: 'Docker build'
```

---

## Sécurité

### Bonnes pratiques appliquées

- **Utilisateur non-root** : le conteneur tourne sous l'utilisateur `app` (UID 1654)
- **NoNewPrivileges** : le service systemd empêche l'escalade de privilèges
- **Volumes en lecture seule** : les modèles sont montés en `ro`
- **Pas de stockage de secrets** : aucune clé API ou mot de passe dans le code

### Exposition publique

Pour une exposition publique, placez un **reverse proxy** devant l'application :

#### nginx (recommandé)

```nginx
server {
    listen 443 ssl http2;
    server_name prediction.mondomaine.com;

    ssl_certificate     /etc/letsencrypt/live/prediction.mondomaine.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/prediction.mondomaine.com/privkey.pem;

    location / {
        proxy_pass http://localhost:7575;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```bash
# Certificat SSL gratuit avec Let's Encrypt
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d prediction.mondomaine.com
```

#### Caddy (alternative simple)

```
prediction.mondomaine.com {
    reverse_proxy localhost:7575
}
```

### Firewall

```bash
# Limiter l'accès au port 7575 depuis l'extérieur
sudo ufw allow 443/tcp     # HTTPS (nginx)
sudo ufw allow 80/tcp      # HTTP (redirection Let's Encrypt)
sudo ufw deny 7575/tcp     # Bloquer l'accès direct à l'application
sudo ufw enable
```

---

## Troubleshooting

### Problèmes courants

#### Le conteneur redémarre en boucle

```bash
# Vérifier les logs
docker compose logs web --tail=50

# Causes fréquentes :
# - Modèle manquant dans models/
# - Chemin de modèle incorrect
# - Port déjà utilisé
```

#### Erreur `Model file not found`

```bash
# Vérifier que le modèle existe
ls -la models/
# Attendu : model_nn.bin, model_binary.zip, model.zip

# Vérifier les chemins dans appsettings.json
docker compose exec web cat appsettings.json
```

#### L'application tourne mais renvoie 502 via nginx

```bash
# Vérifier que le backend répond
curl http://localhost:7575/

# Vérifier les logs nginx
sudo tail -f /var/log/nginx/error.log
```

#### Port 7575 déjà utilisé

```bash
# Identifier le processus
sudo lsof -i :7575
# ou sur Windows
netstat -ano | findstr :7575

# Solution : changer le port dans docker-compose.yml
ports:
  - "7576:7575"   # host:conteneur
```

#### Erreurs de build Docker

```bash
# Nettoyer le cache Docker
docker builder prune -a

# Reconstruire sans cache
docker compose build --no-cache
```

#### Métriques de performance

```bash
# Voir la consommation de ressources
docker stats football-prediction

# Mémoire et CPU du conteneur
docker compose exec web top
```

### Logs détaillés

Pour activer les logs détaillés temporairement :

```bash
# En variable d'environnement
docker compose run -e ASPNETCORE_ENVIRONMENT=Development web

# Ou modifier appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

---

## Résumé des commandes essentielles

```bash
# === DÉPLOIEMENT ===
docker compose up -d --build              # Build + démarrage
docker compose down                        # Arrêt complet

# === SURVEILLANCE ===
docker compose ps                          # État des services
docker compose logs -f web                 # Logs en direct
docker inspect football-prediction --format='{{.State.Health.Status}}'

# === MAINTENANCE ===
docker compose restart                     # Redémarrage
docker compose pull && docker compose up -d  # Mise à jour

# === CLI (entraînement hors-ligne) ===
dotnet run --project src/FootballPrediction.Cli -- train --input data/matches.csv --output models/model.zip
dotnet run --project src/FootballPrediction.Cli -- predict --model models/model.zip --input data/to_predict.csv --output predictions.csv

# === TESTS ===
dotnet test FootballPrediction.sln
```

---

## Schéma de déploiement complet

```
┌──────────────────────────────────────────────────────────────┐
│                     Internet                                  │
└──────────────────────┬───────────────────────────────────────┘
                       │ :443 (HTTPS)
┌──────────────────────▼───────────────────────────────────────┐
│               Reverse Proxy (nginx / Caddy)                   │
│         • Terminaison SSL (Let's Encrypt)                     │
│         • Rate limiting (optionnel)                           │
│         • Compression gzip                                    │
└──────────────────────┬───────────────────────────────────────┘
                       │ :7575 (interne)
┌──────────────────────▼───────────────────────────────────────┐
│               Docker Host (Linux / Windows)                   │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │          football-prediction (conteneur)                  │ │
│  │          ASP.NET Core 8 — port 7575                      │ │
│  │          User: app (non-root)                            │ │
│  │          Healthcheck: toutes les 30s                     │ │
│  │                                                          │ │
│  │  Volumes :                                               │ │
│  │  • ./models → /app/models (ro)                           │ │
│  │  • ./data   → /app/data   (rw)                           │ │
│  └─────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

---

## Authentification

### Compte admin par défaut

Au premier démarrage, un compte administrateur est créé automatiquement :

- **Email** : `admin@football-prediction.local`
- **Mot de passe** : `Admin123!`

> ⚠️ Changez ce mot de passe après la première connexion.

### Base de données Identity

L'authentification utilise **ASP.NET Core Identity** avec **SQLite** (fichier `app.db` généré automatiquement).

```bash
# Sauvegarder la base utilisateurs
cp app.db app.db.backup

# Réinitialiser (supprime tous les utilisateurs)
rm app.db app.db-shm app.db-wal
# Redémarrer l'application → le seed admin sera recréé
```

Politique de mot de passe (configurable dans `Program.cs`) :
- 8 caractères minimum
- 1 majuscule, 1 minuscule, 1 chiffre requis
- Verrouillage après 5 tentatives échouées (15 minutes)

---

## Live Odds (Récupération des cotes en ligne)

La fonctionnalité utilise **The Odds API** pour récupérer les cotes Bet365 et Pinnacle en temps réel.

### Inscription et clé API

1. S'inscrire sur [the-odds-api.com](https://the-odds-api.com)
2. Récupérer la clé API (free tier : 500 requêtes/mois)
3. Configurer la clé :

```bash
# Option 1 : Variable d'environnement (recommandé en production)
export OddsApi__ApiKey="votre-clé-api"

# Option 2 : appsettings.Production.json
{
  "OddsApi": {
    "ApiKey": "votre-clé-api",
    "Region": "eu",
    "CacheMinutes": 5
  }
}

# Option 3 : User Secrets (développement uniquement)
dotnet user-secrets set "OddsApi:ApiKey" "votre-clé-api"
```

### Ligues supportées

| Ligue | Sport Key API |
|-------|--------------|
| Premier League | `soccer_epl` |
| LaLiga | `soccer_spain_la_liga` |
| Bundesliga | `soccer_germany_bundesliga` |
| Ligue 1 | `soccer_france_ligue_one` |
| Serie A | `soccer_italy_serie_a` |

### Cache

Les résultats sont mis en cache **5 minutes** (configurable via `CacheMinutes`) pour respecter le rate limit de l'API (500 req/mois en free tier).

### Fallback

Si l'API est indisponible ou la clé non configurée :
- Un message d'erreur s'affiche dans l'interface
- La saisie manuelle des cotes reste toujours disponible

### Endpoint AJAX

```
GET /Odds/fetch?league=Premier League&homeTeam=Arsenal&awayTeam=Chelsea
→ JSON : Bet365Home, Bet365Draw, Bet365Away, PinnacleHome, PinnacleDraw, PinnacleAway
```

---

*Documentation générée le 18 juillet 2026 — FootballPrediction v1.1*
