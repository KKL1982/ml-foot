# FootballPrediction — 1X2 Match Prediction

Application C# / .NET de prédiction de résultats de matchs de football en classes **1X2** (domicile / nul / extérieur) utilisant ML.NET.

## Architecture

```
ml-foot/
├── src/
│   ├── FootballPrediction.Domain/       # Modèles, enums, value objects
│   ├── FootballPrediction.Application/  # Services, DTOs, parsing CSV
│   ├── FootballPrediction.ML/           # ML.NET : features, entraînement, prédiction
│   ├── FootballPrediction.Cli/          # CLI : train / predict
│   └── FootballPrediction.Web/          # Web (reporté)
├── tests/
│   ├── FootballPrediction.Tests/        # Tests unitaires Domain + Application
│   └── FootballPrediction.ML.Tests/     # Tests unitaires ML
└── documentation/                       # Données et spécifications
```

## Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Fonctionne sur CPU uniquement (pas de GPU requis)

## Installation

```bash
git clone <repo-url>
cd ml-foot
dotnet restore
dotnet build
```

## Utilisation

### Entraîner un modèle

```bash
dotnet run --project src/FootballPrediction.Cli -- train \
  --input data/matches.csv \
  --output models/model.zip
```

**Format CSV attendu** :

| Colonne | Description |
|---------|-------------|
| Date | Date du match (YYYY-MM-DD) |
| League | Championnat |
| Season | Saison |
| HomeTeam | Équipe domicile |
| AwayTeam | Équipe extérieure |
| HomeGoals | Buts domicile |
| AwayGoals | Buts extérieur |
| Result | Résultat 1X2 (1/X/2) |
| HomeCoach | Coach domicile |
| AwayCoach | Coach extérieur |
| Bet365_1, Bet365_X, Bet365_2 | Cotes Bet365 |
| Pinnacle_1, Pinnacle_X, Pinnacle_2 | Cotes Pinnacle |
| WilliamHill_1, WilliamHill_X, WilliamHill_2 | Cotes William Hill |

### Prédire des matchs

```bash
dotnet run --project src/FootballPrediction.Cli -- predict \
  --model models/model.zip \
  --input data/to_predict.csv \
  --output predictions.csv
```

### Lancer les tests

```bash
dotnet test
```

## Modèle

- **Algorithme** : `SdcaMaximumEntropy` (ML.NET)
- **Features** : probabilités bookmakers, forme récente (5 matchs), moyennes buts, différence de forme
- **Split** : 80% train / 20% test (aléatoire stratifié)

## Métriques

Le modèle affiche après entraînement :
- Micro Accuracy
- Macro Accuracy  
- Log Loss
- Matrice de confusion

## Avertissement

> Les prédictions sportives sont incertaines. Les résultats fournis ne constituent pas un conseil financier ou une garantie de gain. Cet outil est expérimental.

## Licence

MIT
