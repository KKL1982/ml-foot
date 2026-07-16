# Prompt Codex — Réseau de neurones C# pour prédiction football 1X2

Je veux créer une application complète en **C# / .NET** pour prédire les résultats de matchs de football en classes **1X2** :

- `1` = victoire de l’équipe domicile ;
- `X` = match nul ;
- `2` = victoire de l’équipe extérieure.

L’application doit être conçue pour fonctionner et s’entraîner sur un **simple laptop multimédia**, sans grosse carte graphique dédiée. Il faut donc éviter les modèles trop lourds et privilégier une approche efficace avec **ML.NET**, ou éventuellement un petit réseau de neurones léger si cela reste raisonnable en CPU.

---

## 1. Objectif général

Créer une solution .NET permettant de :

1. Charger un fichier CSV historique de matchs enrichi avec :
   - date du match ;
   - championnat ;
   - saison ;
   - équipe domicile ;
   - équipe extérieure ;
   - score final ;
   - résultat 1X2 ;
   - coach domicile ;
   - coach extérieur ;
   - cotes 1X2 disponibles, par exemple Bet365, Pinnacle, William Hill ou Betfair ;
   - autres colonnes utiles disponibles.

2. Préparer les données automatiquement.

3. Entraîner un modèle de classification multiclasses pour prédire `1`, `X` ou `2`.

4. Sauvegarder le modèle entraîné sur disque.

5. Permettre la prédiction à partir :
   - d’un fichier CSV contenant les matchs à prédire ;
   - ou d’une interface web simple ASP.NET Core où l’utilisateur saisit les informations du match.

6. Retourner pour chaque match :
   - la classe prédite : `1`, `X` ou `2` ;
   - les probabilités associées aux trois classes ;
   - éventuellement une interprétation simple du type : “victoire domicile probable”, “match équilibré”, etc.

---

## 2. Contraintes techniques

Utiliser de préférence :

- .NET 8 ou version stable récente ;
- C# ;
- ML.NET pour l’entraînement et l’inférence ;
- ASP.NET Core pour l’interface web ;
- Razor Pages, MVC ou Minimal API ;
- CSVHelper pour lire/écrire les CSV ;
- éventuellement SQLite pour stocker l’historique des entraînements et prédictions, mais ce n’est pas obligatoire.

Le projet doit être simple à exécuter localement.

Il doit fonctionner sur CPU.

Ne pas supposer la présence d’un GPU.

Ne pas utiliser de gros frameworks Python.

Ne pas dépendre d’un service cloud externe pour l’entraînement.

---

## 3. Architecture demandée

Créer une solution structurée ainsi :

```text
FootballPrediction/
│
├── FootballPrediction.Domain/
│   ├── Entities/
│   ├── Enums/
│   └── ValueObjects/
│
├── FootballPrediction.Application/
│   ├── DTOs/
│   ├── Interfaces/
│   ├── Services/
│   └── UseCases/
│
├── FootballPrediction.ML/
│   ├── DataModels/
│   ├── FeatureEngineering/
│   ├── Training/
│   ├── Prediction/
│   └── ModelEvaluation/
│
├── FootballPrediction.Web/
│   ├── Controllers ou Pages/
│   ├── Views/
│   ├── wwwroot/
│   └── Program.cs
│
├── FootballPrediction.Cli/
│   └── Program.cs
│
└── tests/
    ├── FootballPrediction.Tests/
    └── FootballPrediction.ML.Tests/
```

La partie CLI doit permettre :

```bash
dotnet run --project FootballPrediction.Cli -- train --input data/matches.csv --output models/model.zip

dotnet run --project FootballPrediction.Cli -- predict --model models/model.zip --input data/matches_to_predict.csv --output predictions.csv
```

La partie Web doit permettre :

- d’uploader un CSV de matchs à prédire ;
- de saisir manuellement un match ;
- d’afficher la prédiction ;
- d’afficher les probabilités `1`, `X`, `2`.

---

## 4. Préparation des données

Proposer et implémenter une préparation de données adaptée au football.

### 4.1 Colonnes minimales attendues

Le fichier historique peut contenir des colonnes comme :

```text
Date
League
Season
HomeTeam
AwayTeam
HomeGoals
AwayGoals
Result
HomeCoach
AwayCoach
Bet365_1
Bet365_X
Bet365_2
Pinnacle_1
Pinnacle_X
Pinnacle_2
Bookmaker3_1
Bookmaker3_X
Bookmaker3_2
```

Adapter le code pour que les noms de colonnes soient configurables si nécessaire.

### 4.2 Variable cible

Créer ou utiliser la colonne `Result`.

Si elle n’existe pas, la calculer ainsi :

```text
si HomeGoals > AwayGoals => Result = 1
si HomeGoals = AwayGoals => Result = X
si HomeGoals < AwayGoals => Result = 2
```

### 4.3 Features de base

Utiliser comme variables d’entrée :

- championnat ;
- saison ;
- équipe domicile ;
- équipe extérieure ;
- coach domicile ;
- coach extérieur ;
- cotes bookmakers disponibles ;
- différence implicite entre les cotes ;
- probabilité implicite issue des cotes ;
- avantage domicile.

### 4.4 Features dérivées recommandées

Créer des features statistiques calculées uniquement à partir des matchs précédents, jamais à partir du futur.

Pour chaque équipe avant le match :

- nombre de matchs joués récemment ;
- forme sur les 5 derniers matchs ;
- forme sur les 10 derniers matchs ;
- moyenne de buts marqués sur les 5 derniers matchs ;
- moyenne de buts encaissés sur les 5 derniers matchs ;
- nombre de victoires sur les 5 derniers matchs ;
- nombre de nuls sur les 5 derniers matchs ;
- nombre de défaites sur les 5 derniers matchs ;
- différence de buts récente ;
- performance domicile de l’équipe domicile ;
- performance extérieur de l’équipe extérieure ;
- nombre de jours depuis le dernier match ;
- changement récent de coach, si détectable ;
- nombre de matchs du coach avec cette équipe avant ce match.

Créer aussi des features comparatives :

```text
Home_Form5 - Away_Form5
Home_GoalsFor5 - Away_GoalsFor5
Home_GoalsAgainst5 - Away_GoalsAgainst5
Home_ImpliedProbability - Away_ImpliedProbability
```

### 4.5 Cotes bookmakers

Pour chaque bookmaker :

```text
ImpliedProb_1 = 1 / Cote_1
ImpliedProb_X = 1 / Cote_X
ImpliedProb_2 = 1 / Cote_2
```

Puis normaliser pour enlever la marge du bookmaker :

```text
Total = ImpliedProb_1 + ImpliedProb_X + ImpliedProb_2

NormalizedProb_1 = ImpliedProb_1 / Total
NormalizedProb_X = ImpliedProb_X / Total
NormalizedProb_2 = ImpliedProb_2 / Total
```

Créer des features comme :

```text
AvgProb_1
AvgProb_X
AvgProb_2
MaxProb
FavoriteClass
OddsSpread
HomeVsAwayProbabilityGap
```

### 4.6 Encodage des variables catégorielles

Encoder les colonnes texte :

- championnat ;
- équipe domicile ;
- équipe extérieure ;
- coach domicile ;
- coach extérieur.

Avec ML.NET, utiliser par exemple :

- `MapValueToKey` ;
- `OneHotEncoding` ;
- ou une stratégie d’encodage adaptée pour éviter une explosion excessive du nombre de colonnes.

### 4.7 Gestion des valeurs manquantes

Prévoir une stratégie claire :

- cotes absentes : utiliser moyenne du championnat ou valeur neutre ;
- coach absent : valeur `Unknown`;
- stats historiques insuffisantes : valeur neutre ou moyenne championnat ;
- date invalide : rejeter la ligne avec message clair.

### 4.8 Éviter la fuite de données

Très important : ne jamais utiliser une information connue seulement après le match.

Interdictions :

- utiliser le score final comme feature ;
- utiliser le résultat final comme feature ;
- utiliser des statistiques calculées avec des matchs postérieurs ;
- mélanger aléatoirement les données temporelles sans précaution.

Le split train/test doit être chronologique :

```text
Train : anciennes saisons
Validation/Test : matchs les plus récents
```

Par exemple :

```text
Train : 2023 + 2024
Test : 2025
```

ou :

```text
Train : 80 % premiers matchs chronologiquement
Test : 20 % derniers matchs chronologiquement
```

---

## 5. Modèles à tester

Comme l’entraînement doit fonctionner sur laptop, proposer plusieurs modèles légers.

### Option principale ML.NET

Tester au minimum :

- `SdcaMaximumEntropy`
- `LbfgsMaximumEntropy`
- `LightGbmMulticlassTrainer`, si disponible et raisonnable sur CPU

Comparer les performances.

Même si je parle de réseau de neurones, privilégier une solution pragmatique. Si un modèle ML.NET classique donne de meilleurs résultats et s’entraîne plus vite, l’utiliser comme baseline.

### Option réseau de neurones léger

Si possible en C#, proposer un petit MLP CPU-friendly avec :

- couche d’entrée ;
- 1 ou 2 couches cachées maximum ;
- ReLU ;
- Softmax en sortie ;
- 3 classes : `1`, `X`, `2`.

Mais ne pas rendre cette option obligatoire si elle complique trop le projet. L’objectif est d’avoir une solution fonctionnelle, maintenable et entraînable localement.

---

## 6. Évaluation du modèle

Afficher au minimum :

- accuracy globale ;
- macro accuracy ou balanced accuracy ;
- log loss ;
- matrice de confusion ;
- précision/rappel par classe `1`, `X`, `2`.

Important : comme les matchs nuls sont souvent plus difficiles à prédire, afficher les métriques spécifiques pour la classe `X`.

Comparer également le modèle à des baselines :

1. prédire toujours la victoire domicile ;
2. prédire selon la plus petite cote bookmaker ;
3. prédire selon la probabilité moyenne des bookmakers.

Le modèle est utile seulement s’il bat au moins une baseline simple.

---

## 7. Sortie de prédiction

Pour chaque match à prédire, produire :

```text
Date
League
HomeTeam
AwayTeam
PredictedResult
Probability_1
Probability_X
Probability_2
Confidence
Comment
```

Exemple :

```text
2025-08-17
Premier League
Chelsea
Liverpool
2
0.31
0.26
0.43
0.43
Liverpool légèrement favori
```

---

## 8. Interface Web

Créer une interface web simple permettant deux modes.

### 8.1 Mode saisie manuelle

Formulaire avec :

- championnat ;
- date du match ;
- équipe domicile ;
- équipe extérieure ;
- coach domicile ;
- coach extérieur ;
- cotes 1X2 Bet365 ;
- cotes 1X2 Pinnacle ;
- cotes 1X2 troisième bookmaker ;
- bouton “Prédire”.

Afficher ensuite :

- classe prédite ;
- probabilités ;
- niveau de confiance ;
- résumé lisible.

### 8.2 Mode CSV

Permettre :

- upload d’un fichier CSV ;
- prédiction en batch ;
- téléchargement d’un CSV de sortie.

---

## 9. Qualité du code

Respecter les points suivants :

- code propre ;
- séparation claire entre domaine, préparation des données, entraînement et prédiction ;
- services testables ;
- logs utiles ;
- gestion des erreurs ;
- validation des fichiers CSV ;
- commentaires utiles, mais pas excessifs ;
- README complet avec instructions d’exécution.

---

## 10. Tests

Ajouter des tests unitaires pour :

- calcul du résultat `1X2` à partir du score ;
- calcul des probabilités implicites ;
- normalisation des probabilités ;
- calcul de forme sur les derniers matchs ;
- absence de fuite de données temporelle ;
- parsing CSV ;
- prédiction sur une ligne d’exemple.

---

## 11. Livrables attendus

Générer :

1. la solution .NET complète ;
2. un projet CLI ;
3. un projet Web ;
4. un module ML ;
5. un exemple de fichier CSV d’entraînement ;
6. un exemple de fichier CSV de prédiction ;
7. un README ;
8. des tests unitaires ;
9. un script ou des commandes pour entraîner le modèle ;
10. un script ou des commandes pour prédire à partir d’un CSV.

---

## 12. README attendu

Le README doit expliquer :

- l’objectif du projet ;
- la structure de la solution ;
- le format CSV attendu ;
- comment entraîner le modèle ;
- comment lancer l’interface web ;
- comment faire une prédiction CSV ;
- quelles métriques lire ;
- les limites du modèle ;
- les règles pour éviter la fuite de données.

---

## 13. Remarques importantes

Ce modèle ne doit pas être présenté comme une garantie de gain aux paris sportifs.

Il doit être présenté comme un outil expérimental d’analyse prédictive.

Ajouter un avertissement clair :

> Les prédictions sportives sont incertaines. Les résultats fournis ne constituent pas un conseil financier ou une garantie de gain.

---

## 14. Priorité d’implémentation

Procéder dans cet ordre :

1. créer la structure de solution ;
2. créer les modèles de données ;
3. créer le lecteur CSV ;
4. créer le calcul de la cible 1X2 ;
5. créer la préparation des features ;
6. créer les features de forme historique ;
7. créer l’entraînement ML.NET ;
8. créer l’évaluation ;
9. créer la sauvegarde/chargement du modèle ;
10. créer la prédiction CSV ;
11. créer l’interface web ;
12. ajouter les tests ;
13. rédiger le README.

Commencer par une version simple et fonctionnelle, puis améliorer progressivement.

---

## Recommandation d’exécution pour Codex

Pour un premier développement, livrer d’abord :

1. la version CLI ;
2. l’entraînement ML.NET ;
3. la prédiction à partir d’un CSV ;
4. les tests unitaires de base.

Ensuite seulement, ajouter l’interface Web ASP.NET Core.
