# Plan : Ligue des champions et Europa League

Statut : **plan validé dans son principe, non implémenté** (à reprendre).

## Contexte
L'application ne gère que 4 championnats (PL, LL, L1, BL). Objectif : ajouter la **Ligue des champions (CL)** et l'**Europa League (EL)**, puis, à terme, un **modèle entraîné pour les coupes**.

Décisions prises :
- **Modèle séparé pour les coupes** : les 4 modèles actuels (`models/*`) ne sont pas touchés, donc aucun risque de régression sur les championnats.
- **Données : résultats gratuits + collecte progressive des cotes.** Les résultats historiques viennent d'openfootball (domaine public, CL et EL, format Football.TXT, sans cotes ni entraîneurs). Les cotes sont enregistrées par l'application au fil des semaines. Un historique exploitable demande environ **400 matchs avec cotes (≈ 2 saisons de CL)** : **l'entraînement est donc différé** jusqu'à ce seuil. D'ici là, les matchs de coupes sont prédits avec le modèle actuel et un **avertissement de fiabilité**.
- Étape facultative d'accélération : récupérer 2022-2024 via l'offre gratuite d'API-Football (≈ 570 matchs de CL), **si** les cotes passées y sont accessibles (non confirmé : à tester sur une saison avant de s'engager).

Constats sur le code existant :
- Schéma du CSV d'entraînement (`documentation/matchs_2015_2025_coachs_cotes_football_data.csv`, 14 211 lignes, 126 équipes) : `Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result,HomeCoach,AwayCoach,Bet365_*,Pinnacle_*,WilliamHill_*`. `CsvParserService` accepte ce schéma pour toute valeur de `League`.
- Le modèle utilise : one-hot (League, Season, équipes, coachs), cotes Bet365/Pinnacle normalisées, forme sur 5 matchs, ancienneté du coach. Une valeur inconnue est encodée à zéro (pas d'erreur, mais prédictions peu fiables).
- Écart entraînement/service existant : à la prédiction, forme et ancienneté sont à 0 (`MatchDataBuilder.cs:51-57`, `PredictionService.cs:178-181`), alors que l'entraînement utilise les vraies valeurs.
- football-data.org gratuit : CL (code `CL`) en saison courante ; EL non incluse d'après des sources tierces (non confirmé officiellement) → message clair en cas de 403.
- The Odds API : clés `soccer_uefa_champs_league` et `soccer_uefa_europa_league`. L'historique y est payant (≈ 119 $/mois selon des tiers, non confirmé), d'où le choix de la collecte progressive.

## Étape 1 : coupes dans l'application (avec avertissement)
Un seul référentiel de ligues pilote le reste.
- `src/FootballPrediction.Application/Services/ChampionshipService.cs:22-28` : ajouter `CL` / `Champions League` et `EL` / `Europa League` à `DefaultChampionships`. Ajouter à `src/FootballPrediction.Domain/Entities/Championship.cs` un indicateur `IsCup`.
- `src/FootballPrediction.Web/Services/FootballDataClient.cs:20-23` : `CL → CL`, `EL → EL`. Une erreur 403/404 pour une compétition non incluse dans la formule de la clé devient un message lisible (« Europa League non disponible avec votre clé football-data.org »), sans faire échouer les autres ligues du lot.
- `src/FootballPrediction.Web/Services/OddsFetcherService.cs:24-32` : `Champions League → soccer_uefa_champs_league`, `Europa League → soccer_uefa_europa_league`.
- `src/FootballPrediction.Application/Services/TeamNameMapper.cs:17-75` : alias des clubs européens (noms football-data.org et The Odds API → noms canoniques).
- Vues : `Views/Prediction/Index.cshtml:29-33` (liste en dur, Serie A incluse sans modèle) devient pilotée par `ChampionshipService` ; `Views/Home/Index.cshtml:13,41` mis à jour. `Views/Batch/Index.cshtml:57-60` est déjà piloté par les données.
- **Avertissement** dans `Views/Batch/Result.cshtml` (réutilise le bloc `Warnings`) pour tout match de coupe prédit sans modèle dédié : « Modèle entraîné sur 4 championnats : fiabilité réduite pour la Ligue des champions / l'Europa League ».
- Tests : `ChampionshipServiceTests.cs:29-34` (quatre → six), alias de `TeamNameMapper`, `FootballDataClient` (codes CL/EL, 403 lisible, avec le gestionnaire HTTP factice de `FootballDataClientAndBatchTests`), correspondance de ligue dans `OddsFetcherService`.

## Étape 2 : collecte progressive des cotes et des résultats
Objectif : constituer, sans coût, le fichier `matchs_coupes_europe.csv` au même schéma que le CSV existant.
1. **Magasin d'historique des cotes** `OddsHistoryStore` (nouveau, dans `src/FootballPrediction.Web/Services/`, sur le modèle de `MatchFileStore`) : enregistre dans `data/cups/odds_snapshots.csv` une ligne par match à venir de CL/EL (clé = date + domicile + extérieur, ligue, cotes Bet365/Pinnacle/WilliamHill, date de collecte). Pour un même match, la dernière collecte avant le coup d'envoi remplace les précédentes (cotes les plus proches de la clôture). Branché dans `MatchFetchService`, qui récupère déjà les cotes via `OddsFetcherService`.
2. **Collecteur quotidien** `CupsCollectorService` (nouveau `BackgroundService`, désactivé par défaut, option `CupsCollector:Enabled=true`) : une fois par jour, récupère les matchs des 7 prochains jours (CL, et EL si la clé le permet) et leurs cotes, et les enregistre ; collecte aussi les matchs terminés (score) via `FootballDataClient`. Coût en quota : environ 2 appels The Odds API par jour (≈ 60 crédits par mois sur 500 gratuits) ; football-data.org gratuit : 10 appels par minute, largement suffisant.
3. **Commande CLI `build-cups-dataset`** (`src/FootballPrediction.Cli/Program.cs`) : fusionne les résultats historiques (**importés depuis openfootball** par un parseur du format Football.TXT, nouveau `OpenFootballImporter`) et les cotes enregistrées, pour produire `matchs_coupes_europe.csv`. Les lignes sans cotes sont signalées, pas supprimées ; `Unknown` pour les entraîneurs.
4. **Commande CLI `validate-csv`** (réutilise `CsvParserService`) : lignes par ligue et saison, lignes sans cotes, doublons, noms d'équipes **inconnus de `TeamNameMapper`** (échec si des équipes ne sont pas mappées).
5. **Étape facultative d'accélération (à tester sur une saison avant engagement)** : script `scripts/backfill-api-football` qui récupère les saisons 2022-2024 de la CL (résultats, cotes pré-match, entraîneurs) via l'offre gratuite d'API-Football (limite annoncée : 100 requêtes par jour). Les lignes sont versées dans le même fichier. Si les cotes passées ne sont pas disponibles, on abandonne cette étape sans conséquence sur le reste.
6. Tests : `OddsHistoryStore` (remplacement par la dernière collecte avant le match, pas de doublon), `OpenFootballImporter` (extrait de fichier réel), `build-cups-dataset`, `validate-csv`.

## Étape 3 : entraînement du modèle des coupes (déclenché quand le seuil est atteint)
Seuil de lancement : **au moins 400 matchs de coupes avec cotes** (`validate-csv` l'indique). Fichiers : `src/FootballPrediction.Cli/Program.cs` (`train`, lignes 115-153), `src/FootballPrediction.ML/Training/ModelTrainer.cs`, `src/FootballPrediction.ML/FeatureEngineering/FeatureEngineer.cs`.
1. Option `--train-leagues "Champions League,Europa League"` pour `train` et `compare` : caractéristiques calculées sur le fichier fusionné (championnats + coupes), seules les lignes de coupes servent à l'entraînement et au test. Réutilise `FeatureEngineer.BuildFeatures(matches, binaryMode)` avec un filtre après calcul.
2. Pour ce modèle, **pas de forme, d'ancienneté ni de coach** (entraîneurs inconnus pour les coupes, forme à zéro au service comme aujourd'hui) : les variables retenues sont les cotes, les équipes et la ligue. Cela évite l'écart entraînement/service existant.
3. Modèle **binaire** (domicile / extérieur, défaut de l'application), ML.NET uniquement ; comparer `sdca`, `lbfgs`, `lightgbm` avec la commande `compare` existante. Pas de réseau de neurones : le CLI ne sait pas le sauvegarder, et son vocabulaire inconnu retombe silencieusement sur l'indice 0 (`NeuralNetworkTrainer.cs:206-211`).
4. Découpage chronologique 80/20, graine 42 (comportement existant). Ajouter une évaluation par ligue dans `ModelEvaluator` et une **référence de comparaison** : exactitude de « parier sur le favori des cotes ».
5. **Critère d'acceptation** : exactitude et log-loss meilleures que la référence, avec au moins 100 matchs de test ; sinon le modèle n'est pas livré et l'avertissement reste.
6. Scripts reproductibles `scripts/retrain-cups.ps1` et `.sh` : `validate-csv`, `compare`, `train --binary --output models/model_cups_binary.zip`, copie dans `modeles_entraines/`.
7. Tests ML (`tests/FootballPrediction.ML.Tests`) : filtre `--train-leagues`, évaluation par ligue, référence « favori des cotes ». Aucun test n'exerce `ModelTrainer` aujourd'hui.

## Étape 4 : service du modèle des coupes (après l'étape 3)
- `src/FootballPrediction.Web/Models/ModelSettings.cs` et `appsettings.json` : `CupsBinaryModelPath = "models/model_cups_binary.zip"`.
- `src/FootballPrediction.Web/Program.cs:70-95` : second `MatchPredictor` (« cups »), chargé seulement si le fichier existe (mêmes chemins candidats que le modèle principal).
- `src/FootballPrediction.Web/Services/PredictionService.cs` et `BatchPredictionService` : choisir le prédicteur selon la ligue ; repli sur le modèle principal avec avertissement si le modèle des coupes est absent.
- Le Dockerfile copie déjà `models/` et `docker-compose.yml` le monte en lecture seule : rien à changer.
- Test de routage (mock des deux prédicteurs).

## Documentation et déploiement
- `documentation/DEPLOYMENT.md` (vers les lignes 965-1030) : section « coupes d'Europe » (collecte, seuil, réentraînement, métriques) ; `README.md:82` à corriger (le découpage est chronologique, pas « aléatoire stratifié »).
- Configuration du collecteur : `CupsCollector:Enabled`, plage de collecte, rappel du quota The Odds API (`CacheMinutes` à 60) ; mention de la formule de la clé football-data.org pour l'EL.
- Déploiement habituel : `git pull`, puis `sudo ./deploy/deploy.sh --domain prediction.dbdesignandcodereview.com --skip-fail2ban` (voir `documentation/DEPLOIEMENT_VPS.html`). Le dossier `data/cups/` vit dans le volume `./data` : il survit aux redéploiements.

## Ordre d'exécution (commits séparés)
1. Étape 1 : coupes dans l'application et avertissement.
2. Étape 2 (points 1, 2, 4) : stockage des cotes, collecteur, `validate-csv`.
3. Étape 2 (points 3, 5) : import openfootball, `build-cups-dataset`, essai API-Football sur une saison.
4. Étape 3 : outils d'entraînement et scripts (livrables tout de suite, exécution plus tard).
5. Étape 4 : routage du modèle des coupes, quand un modèle accepté existe.

## Vérification
- `dotnet build` et `dotnet test` après chaque étape.
- Étape 1 : en local, cocher CL dans la page Batch avec une clé football-data.org valide ; les matchs apparaissent avec cotes et bandeau d'avertissement. Avec une clé sans accès à l'EL : message clair, les autres ligues fonctionnent.
- Étape 2 : lancer le collecteur deux jours de suite en local ; `data/cups/odds_snapshots.csv` ne contient qu'une ligne par match, avec la collecte la plus récente. `build-cups-dataset` puis `validate-csv` sur un échantillon openfootball : zéro équipe inconnue.
- Étape 3 : exécuter `compare` et `train --train-leagues` sur le fichier courant (même sous le seuil, à titre d'essai) ; vérifier les métriques par ligue et la référence « favori des cotes ». Le modèle n'est publié que si le critère d'acceptation est rempli.
- Étape 4 : CLI `predict` avec un match de CL, avec et sans `model_cups_binary.zip` : routage et avertissement corrects.

## Risques
- **Délai** : sans l'accélération API-Football, il faut environ deux saisons pour atteindre le seuil. D'ici là, les prédictions de coupes restent peu fiables, d'où l'avertissement.
- **Collecte interrompue** : si le service ne tourne pas les jours de match, des cotes manquent ; `validate-csv` les signale.
- **Quota The Odds API** : à surveiller (500 crédits gratuits par mois).
- **Disponibilité de l'EL** sur la clé football-data.org : non confirmée, gérée par un message clair.
- Hors périmètre : l'écart entraînement/service des 4 modèles existants, et le vocabulaire inconnu du réseau de neurones.
