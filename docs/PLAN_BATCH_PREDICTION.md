# PLAN — Fonctionnalités batch de prédiction (championnats + coachs + affichage)

**Repo** : `ml-foot` — **Branche** : `feat/alignement-ml-foot`
**Date** : 13/09/2026
**Méthode** : TDD (chaque fonctionnalité = test rouge → implémentation → test vert). Objectif : **tout vert**.

---

## 0. Contexte & données (vérifié sur le repo)

Le projet `FootballPrediction` prédit des résultats de football **1X2** via ML.NET (SdcaMaximumEntropy, LightGbm, NeuralNetwork).

**Données disponibles** (`documentation/matchs_2015_2025_coachs_cotes_football_data.csv`) — vérifié :
- **14 211 matchs** sur **4 championnats** :
  | Championnat | Matchs |
  |---|---|
  | Premier League | 3800 |
  | LaLiga | 3800 |
  | Ligue 1 | 3551 |
  | Bundesliga | 3060 |
- **126 équipes** distinctes
- **Coachs présents sur 100% des lignes** (14 211/14 211) — déjà utilisés en feature (`HomeCoachTenure` / `AwayCoachTenure` dans `FeatureEngineer.GetCoachTenure`)

**Ce qui existe déjà** :
- `CsvParserService` : parse le CSV (équipes, date, buts, résultat, coachs, cotes Bet365/Pinnacle/WilliamHill)
- `FeatureEngineer` : construit les features dont la **séniorité des coachs** (`coach tenure`)
- `ModelTrainer`/`NeuralNetwork`/`MatchPredictor` : entraînement + prédiction
- CLI `predict` : prédiction batch → CSV
- Web `BatchController` : upload CSV → prédictions (version simple)

**Besoins exprimés** (améliorés ci-dessous) :
1. Mise à jour des entraîneurs des équipes des championnats concernés
2. Chargement des matchs des championnats concernés
3. Prédiction des résultats des matchs en batch
4. Affichage des résultats dans un tableau (min. équipes, date, résultat)

---

## 1. Architecture cible (améliorée)

### 1.1 Entités / DTOs (Domain + Application)

Nouveaux objets de la couche Application :
- `CoachUpdate` : représentation d'un changement d'entraîneur
  - `Team`, `Coach`, `Season` (ou `EffectiveFrom` date), `League`
- `BatchPredictionItem` (DTO de sortie riche, pour l'affichage) :
  - `Date`, `League`, `HomeTeam`, `AwayTeam`, `HomeCoach`, `AwayCoach`
  - `PredictedResult` (1/X/2), `Probability1`, `ProbabilityX`, `Probability2`, `Confidence`
  - `Bet` (HOME/AWAY/SKIP selon seuil), `Comment`
  - (optionnel) `Cotes` moyennées disponibles

### 1.2 Services (Application)

- `IChampionshipService` / `ChampionshipService` :
  - `GetChampionships()` → liste des championnats supportés (4 par défaut)
  - `GetTeams(championship)` → équipes d'un championnat
  - `LoadMatches(championship, dateFrom, dateTo)` → chargement des matchs d'un championnat
  - `GetCoach(team, asOfDate)` → entraîneur actuel d'une équipe à une date donnée
  - `UpdateCoaches(updates)` → applique les mises à jour d'entraîneurs (persistées)

- `ICoachRepository` (persistance) :
  - persiste l'historique des coachs par équipe (`team / coach / effectiveFrom`)
  - permet au `FeatureEngineer` de retrouver le bon coach à une date donnée

- `IBatchPredictionService` / `BatchPredictionService` :
  - `PredictBatch(items)` → applique le modèle sur une liste de matchs non joués
  - retourne des `BatchPredictionItem` enrichis (prédiction + coachs à jour + cotes)

### 1.3 Modèle ML (ML)

- Le `FeatureEngineer` doit consommer les coachs **à jour** (via `ICoachRepository`) au lieu du seul CSV figé, pour que la prédiction reflète un changement d'entraîneur récent.
  - refactor `GetCoachTenure` pour accepter une source de coachs par date (au lieu du dictionnaire interne figé).

### 1.4 CLI (Cli)

- Nouvelle sous-commande `coach` :
  - `coach list --league` → liste les équipes + entraîneur actuel
  - `coach update --team <T> --coach <C> --season <S>` → met à jour un entraîneur
  - `coach refresh --input <csv>` → synchronise les coachs depuis un CSV externe (ex. mises à jour hebdo)
- `batch predict` (ou enrichir `predict`) :
  - `--league` (filtre par championnat)
  - `--league-file <csv>` (liste des championnats concernés)
  - sortie table console + CSV
- `load-matches` :
  - charge les matchs d'un/des championnat(s) depuis le CSV de référence + applique les coachs à jour
  - écrit un CSV prêt pour la prédiction

### 1.5 Web (Web) — affichage

- Page `Batch` améliorée : upload CSV ou sélection de championnat → prédictions rendues dans un **tableau riche** :
  - Colonnes : **Date · Championnat · Équipe domicile (entraîneur) · Équipe extérieure (entraîneur) · Prédiction 1X2 · Prob. 1 / X / 2 · Confiance · Bet · Commentaire**
  - Tri par date / championnat
  - Filtre par championnat (les 4 concernés)
  - Mise en évidence visuelle du résultat prédit (1 = vert, X = gris, 2 = rouge), badges de confiance
- Endpoint API `GET /api/batch/predict?league=...` (JSON) pour une intégration propre

---

## 2. Championnats concernés (par défaut)

Les 4 championnats présents dans les données (configurables via `appsettings` / options) :

| Clé | Nom | Code |
|-----|-----|------|
| `england` | Premier League | `PL` |
| `spain` | LaLiga | `LL` |
| `france` | Ligue 1 | `L1` |
| `germany` | Bundesliga | `BL` |

Le code supporte l'ajout de championnats via configuration (pas codés en dur).

---

## 3. Découpé en TDD (chaque étape a ses tests)

> Convention : chaque fonctionnalité commence par un test (rouge), puis l'implémentation minimale, puis le test vert. On ne passe à la suivante que si la suite est verte.

### Étape 1 — `ChampionshipService` (Domain + Application)
- Tests :
  - `GetChampionships` retourne les 4 championnats attendus (PL/LL/L1/BL)
  - `GetTeams("england")` retourne les équipes de Premier League issues du CSV
  - `LoadMatches("england", range)` retourne les matchs filtrés sur la plage de dates, triés chronologiquement
  - `LoadMatches` ignore les championnats inconnus (retourne vide, ne lève pas)

### Étape 2 — `CoachRepository` (persistance des entraîneurs)
- Tests :
  - `GetCoach(team, asOfDate)` retourne le coach actif à la date donnée
  - historique multi-coachs : retourne l'ancien coach pour une date passée, le nouveau pour une date récente
  - `UpdateCoaches` applique et rend le nouveau coach actif
  - équipe sans coach → `null`/`Unknown` sans lever

### Étape 3 — Service de mise à jour des coachs
- Tests :
  - `UpdateCoaches` depuis une liste de `CoachUpdate` persistée correctement
  - doublon (même team+coach+season) → idempotent, pas de doublon
  - `refresh` depuis un CSV externe (coachs à jour) met à jour les entraîneurs concernés
  - changement d'entraîneur détecté → la feature `coach tenure` remet à zéro pour le nouveau coach

### Étape 4 — `FeatureEngineer` utilise les coachs à jour
- Tests :
  - à partir de matchs à prédire + `ICoachRepository` : la feature `HomeCoachTenure`/`AwayCoachTenure` reflète la tenure au moment du match
  - un changement d'entraîneur récent → tenure du nouveau coach = 0 (ou proche de 0)
  - comportement inchangé si coach absent / repository vide (régression)

### Étape 5 — `BatchPredictionService`
- Tests :
  - `PredictBatch(items)` retourne un `BatchPredictionItem` par match d'entrée (mêmes équipes/date)
  - `PredictedResult` ∈ {1, X, 2} et probabilités dans [0,1], somme ≈ 1
  - `Confidence` = probabilité max
  - `Bet` = HOME/AWAY si confiance ≥ seuil, sinon SKIP ; cohérent avec la prédiction
  - les coachs à jour sont injectés dans la feature (branchement avec l'Étape 4)
  - gère un modèle chargé ou un stub de modèle injectable pour les tests unitaires

### Étape 6 — CLI `batch predict` + `coach update` + `load-matches`
- Tests (via le CLI ou des tests d'intégration légers sur les handlers) :
  - `coach update` : met à jour le coach d'une équipe (persistance vérifiée)
  - `coach list --league england` : retourne équipes + coachs
  - `load-matches --league <L> --out file.csv` : écrit un CSV contenant les matchs à prédire
  - `batch predict --model m --input in.csv --out out.csv --table` : produit la sortie CSV et la table console

### Étape 7 — Web : tableau d'affichage + endpoint API
- Tests :
  - `BatchResultViewModel` : mapping d'une prédiction → modèle de vue (date, équipes, prédiction, prob, confiance, commentaire, badges)
  - tri par date, filtre par championnat
  - endpoint `GET /api/batch/predict` : requête par championnat → JSON des `BatchPredictionItem`
  - vue HTML : rendu de la table avec les colonnes attendues (équipes + date + résultat + coachs + probabilités)

### Étape 8 — Non-régression finale
- `dotnet test` complet : tous les projets (`FootballPrediction.Tests` + `FootballPrediction.ML.Tests`) **verts**

---

## 4. Amélioration de la proposition d'affichage (tableau)

Au lieu d'un simple tableau (équipes, date, résultat), le plan enrichit l'affichage :

| Date | Championnat | Domicile (entraîneur) | Extérieur (entraîneur) | Prédiction | 1 | X | 2 | Confiance | Bet | Commentaire |
|------|------------|----------------------|----------------------|-----------|-----|-----|-----|-----------|-----|-------------|
| 2026-09-20 | Premier League | Arsenal (Arteta) | Man City (Guardiola) | **1** | 0.55 | 0.24 | 0.21 | 55% | HOME | Home win likely |

- **Visuel** : résultat prédit encadré (1 vert, X gris, 2 rouge), probabilités en barres proportionnelles, badge de confiance (élevée/moyenne/faible), `Bet` clair pour pari éventuel.
- **Coachs affichés** pour vérifier au premier coup d'œil qu'un changement d'entraîneur est bien pris en compte.
- **Filtres** : championnat (les 4), plage de dates, tri.
- Export CSV de la même table.

---

## 5. Risques & mitigations

| Risque | Mitigation |
|---|---|
| Changer la feature `coach tenure` casse la régression ML | Test d'Étape 4 « comportement inchangé si repository vide » ; la suite ML existante reste verte |
| Persistance des coachs dans un nouveau store | Repository avec implémentation en mémoire + fichier JSON (léger, pas de base) ; testé |
| Modèle non dispo en test unitaire | `BatchPredictionService` accepte un prédicteur injectable — les tests utilisent un stub, l'Étape 8 valide avec le modèle réel |
| Championnats codés en dur | Passage via configuration (appsettings), avec 4 par défaut |

---

## 6. Livrables

1. `ChampionshipService` + `CoachRepository` + service de mise à jour des coachs
2. `FeatureEngineer` branché sur les coachs à jour
3. `BatchPredictionService` (prédiction batch enrichie)
4. CLI : `coach update/list/refresh`, `load-matches`, `batch predict` (table console + CSV)
5. Web : tableau riche + filtre championnat + endpoint API
6. Tests TDD **tous verts** (Domain, Application, ML, Web)

---

## 7. Statut

- [x] Étape 1 — ChampionshipService
- [x] Étape 2 — CoachRepository
- [x] Étape 3 — Mise à jour des coachs
- [x] Étape 4 — FeatureEngineer (coachs à jour)
- [x] Étape 5 — BatchPredictionService
- [x] Étape 6 — CLI (coach, load-matches, batch predict)
- [x] Étape 7 — Web (tableau + API)
- [x] Étape 8 — Non-régression (tout vert)

## 8. Récap de la réalisation (TDD, tout vert)

### Suite de tests
- ML.Tests : **28** verts (24 baseline + 4 FeatureEngineer-coachs)
- Tests : **69** verts (34 baseline + 35 nouveaux)
- Total : **97 tests verts, 0 échec** (baseline : 58)

### Fichiers nouvellement créés
| Fichier | Rôle |
|---|---|
| `Domain/Entities/Championship.cs` | Championnat (code + nom) |
| `Domain/Entities/CoachAssignment.cs` | Affectation coach (team/coach/date) |
| `Application/Interfaces/IChampionshipService.cs` | Service championnats |
| `Application/Interfaces/ICoachRepository.cs` | Persistance coachs |
| `Application/Services/ChampionshipService.cs` | Championnats, équipes, chargement matchs |
| `Application/Services/InMemoryCoachRepository.cs` | Repo coachs en mémoire |
| `Application/Services/JsonCoachRepository.cs` | Repo coachs persisté JSON |
| `Application/Services/CoachService.cs` | Mise à jour / synchronisation coachs |
| `Application/Services/BatchResultFormatter.cs` | Table console + CSV |
| `Application/DTOs/CoachUpdate.cs` | Demande mise à jour coach |
| `Application/DTOs/BatchPredictionItem.cs` | Prédiction enrichie (affichage) |
| `ML/FeatureEngineering/FeatureEngineer.cs` (modif) | Coachs à jour injectables (tenure) |
| `Web/Services/BatchPredictionService.cs` | Orchestration prédiction batch enrichie |
| `Cli/MatchDataBuilder.cs` | Feature pour match à prédire |
| `Cli/Program.cs` (modif) | Commandes `coach`, `load-matches`, `batch-predict` |
| `Web/Controllers/BatchController.cs` (modif) | Tableau enrichi + coachs |
| `Web/Controllers/ApiController.cs` (modif) | Endpoint `POST /api/batch/predict` |
| `Web/Views/Batch/Result.cshtml` (modif) | Colonnes coachs |
| `Web/Program.cs` (modif) | DI coach repo + batch service |

### Points de vigilance
- `BatchPredictionService.Predict` prédit des matchs **à venir** (non joués) sans passer par `BuildFeatures` (qui ne traite que les matchs joués) — validé par test.
- Le mode par défaut du Batch Web est passé en **Multiclass** (1/X/2 + coachs + bet), contrairement à l'ancien chemin binary. La vue gère déjà les deux modes.
- Le parsing de date du CSV préexistant (format `DD/MM/YYYY`) tombe sur `0001-01-01` pour certains CSV — bug **préexistant**, hors périmètre (non introduit par ce chantier).
