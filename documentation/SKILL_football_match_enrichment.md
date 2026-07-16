# Skill — Enrichir une liste de matchs avec coachs et cotes historiques gratuites

## Objectif

Cette skill décrit la procédure suivie pour transformer un fichier CSV de matchs de football en un fichier enrichi contenant :

- les noms des coachs de l’équipe domicile et de l’équipe extérieure au jour du match ;
- les cotes historiques 1X2 disponibles gratuitement ;
- un fichier Excel final structuré avec les données, le résumé, les sources et le dictionnaire des colonnes.

La procédure est adaptée aux championnats suivants :

- Premier League ;
- LaLiga ;
- Bundesliga.

Période traitée dans notre cas : 2023, 2024 et 2025.

---

## Entrées attendues

### Fichier principal

Un fichier CSV contenant une ligne par match.

Exemple de fichier d’entrée utilisé :

```text
matchs_2023_2025.csv
```

Le fichier doit contenir au minimum :

- la compétition ou le championnat ;
- la saison ou l’année ;
- la date du match ;
- l’équipe domicile ;
- l’équipe extérieure ;
- le score ou résultat si disponible ;
- les colonnes coach domicile / coach extérieur, même si elles sont vides ou à compléter.

### Format constaté

Dans notre cas, le CSV était séparé par `;` et contenait 2 511 matchs.

---

## Sorties produites

La procédure produit plusieurs fichiers.

### 1. CSV enrichi avec coachs

```text
matchs_2023_2025_coachs_maj.csv
```

Contenu :

- tous les matchs d’origine ;
- colonnes coach domicile mises à jour ;
- colonnes coach extérieur mises à jour.

### 2. Table de correspondance des coachs

```text
mapping_coachs_intervalles.csv
```

Contenu :

- championnat ;
- club ;
- coach ;
- date de début ;
- date de fin ;
- source ou justification ;
- remarque éventuelle.

Cette table est essentielle, car elle permet de déterminer quel coach était en poste à la date exacte du match.

### 3. Excel enrichi avec coachs

```text
matchs_2023_2025_coachs_maj.xlsx
```

Feuilles :

- `Matchs` ;
- `Mapping Coachs` ;
- `Résumé`.

### 4. CSV enrichi avec coachs et cotes

```text
matchs_2023_2025_coachs_cotes_football_data.csv
```

Contenu :

- matchs ;
- coachs ;
- cotes 1X2 gratuites disponibles.

### 5. Excel final enrichi avec coachs et cotes

```text
matchs_2023_2025_coachs_cotes_football_data.xlsx
```

Feuilles recommandées :

- `Matchs + Coachs + Cotes` ;
- `Résumé` ;
- `Dictionnaire` ;
- `Sources` ;
- éventuellement `Lignes à vérifier`.

---

## Étape 1 — Lire et analyser le CSV initial

### But

Comprendre la structure réelle du fichier avant tout enrichissement.

### Actions

1. Charger le fichier CSV.
2. Détecter le séparateur.
3. Lister les colonnes.
4. Compter les lignes.
5. Identifier les colonnes utiles :
   - date du match ;
   - championnat ;
   - saison ;
   - équipe domicile ;
   - équipe extérieure ;
   - coach domicile ;
   - coach extérieur.
6. Identifier les valeurs à remplacer, par exemple :
   - `A scraper (cf plan B)` ;
   - cellule vide ;
   - `NON TROUVÉ` ;
   - `À vérifier`.

### Contrôle qualité

Vérifier que le nombre de lignes chargé correspond au nombre attendu.

Dans notre cas :

```text
Nombre de matchs : 2 511
```

---

## Étape 2 — Construire le mapping des coachs par club et par période

### But

Pouvoir répondre à la question suivante pour chaque ligne :

> Quel était l’entraîneur de cette équipe à la date exacte de ce match ?

### Principe

On construit une table d’intervalles.

Exemple :

```text
Championnat;Club;Coach;Date_Debut;Date_Fin
Premier League;Chelsea;Mauricio Pochettino;2023-07-01;2024-05-21
Premier League;Chelsea;Enzo Maresca;2024-07-01;...
```

### Sources utilisées

Pour la version coachs, les sources recherchées sont principalement :

- historiques de changements d’entraîneurs par saison ;
- pages de clubs ;
- pages de saisons ;
- sources sportives reconnues lorsque nécessaire.

Sources typiques :

- Transfermarkt ;
- Wikipedia pour certains tableaux saisonniers ;
- sites officiels de clubs si nécessaire ;
- articles fiables pour confirmer une date de nomination ou de départ.

### Règle d’intervalle

Pour chaque club :

1. Trier les coachs par date de début.
2. Déduire la date de fin d’un coach à partir de la date de début du coach suivant, moins un jour.
3. Si la date de fin officielle est connue, l’utiliser.
4. Si un intérimaire couvre quelques matchs, créer un intervalle distinct.

### Exemple de logique

```text
Si Date_Debut <= Date_Match <= Date_Fin
Alors Coach_Match = Coach
```

---

## Étape 3 — Enrichir les colonnes coachs

### But

Remplir les colonnes :

- coach domicile ;
- coach extérieur.

### Algorithme

Pour chaque match :

1. Lire la date du match.
2. Lire l’équipe domicile.
3. Lire l’équipe extérieure.
4. Normaliser les noms d’équipes.
5. Chercher dans `mapping_coachs_intervalles.csv` l’intervalle correspondant au club domicile.
6. Chercher dans `mapping_coachs_intervalles.csv` l’intervalle correspondant au club extérieur.
7. Remplir les colonnes coachs.
8. Si aucun coach n’est trouvé, marquer la cellule comme `À vérifier`.

### Normalisation des noms d’équipes

Créer une table de synonymes lorsque les noms diffèrent entre les fichiers.

Exemples :

```text
Man United -> Manchester United
Man Utd -> Manchester United
Spurs -> Tottenham Hotspur
Bayern Munich -> Bayern München
Leverkusen -> Bayer Leverkusen
```

### Contrôle qualité

Après enrichissement :

1. Compter les cellules vides.
2. Compter les `A scraper (cf plan B)`.
3. Compter les `NON TROUVÉ`.
4. Compter les `À vérifier`.

Dans notre résultat final coachs :

```text
Aucune cellule ne contenait encore :
- A scraper (cf plan B)
- NON TROUVÉ
- À vérifier
```

---

## Étape 4 — Générer un premier Excel avec coachs

### But

Fournir un fichier lisible et vérifiable.

### Feuilles recommandées

#### Feuille `Matchs`

Contient toutes les lignes enrichies.

#### Feuille `Mapping Coachs`

Contient la table des coachs par intervalle.

#### Feuille `Résumé`

Contient :

- nombre total de matchs ;
- nombre de matchs enrichis ;
- nombre de coachs non trouvés ;
- date de génération ;
- remarques.

---

## Étape 5 — Ajouter les cotes historiques gratuites

### But

Ajouter les cotes 1X2 disponibles gratuitement.

### Source gratuite retenue

La source gratuite retenue est :

```text
Football-Data.co.uk
```

Raison :

- propose des fichiers CSV historiques ;
- couvre les grands championnats européens ;
- inclut souvent des cotes 1X2 de plusieurs bookmakers ;
- les colonnes sont déjà structurées.

### Championnats ciblés

Correspondance généralement utilisée :

```text
Premier League -> E0
LaLiga -> SP1
Bundesliga -> D1
```

### Saisons à récupérer

Pour une période 2023–2025, il faut généralement récupérer plusieurs saisons sportives :

```text
2022-2023
2023-2024
2024-2025
2025-2026, si des matchs de 2025-2026 sont présents
```

Exemples de fichiers Football-Data :

```text
https://www.football-data.co.uk/mmz4281/2324/E0.csv
https://www.football-data.co.uk/mmz4281/2324/SP1.csv
https://www.football-data.co.uk/mmz4281/2324/D1.csv
```

Le code saison change selon l’année :

```text
2223
2324
2425
2526
```

---

## Étape 6 — Comprendre les colonnes de cotes Football-Data

Football-Data utilise plusieurs abréviations.

### Bet365

```text
B365H = cote victoire domicile
B365D = cote match nul
B365A = cote victoire extérieur
```

### Pinnacle

```text
PSH = cote victoire domicile
PSD = cote match nul
PSA = cote victoire extérieur
```

### William Hill

```text
WHH = cote victoire domicile
WHD = cote match nul
WHA = cote victoire extérieur
```

### Betfair Exchange

Selon disponibilité :

```text
BFH = cote victoire domicile
BFD = cote match nul
BFA = cote victoire extérieur
```

### Colonnes finales recommandées

Dans notre fichier final, les colonnes ont été renommées de manière lisible :

```text
Bet365_1
Bet365_X
Bet365_2

Pinnacle_1
Pinnacle_X
Pinnacle_2

Bookmaker3_Nom
Bookmaker3_1
Bookmaker3_X
Bookmaker3_2
```

Avec :

```text
1 = victoire domicile
X = match nul
2 = victoire extérieur
```

---

## Étape 7 — Matcher les matchs avec les fichiers de cotes

### But

Relier chaque match du fichier principal à la bonne ligne Football-Data.

### Clés de matching

La correspondance se fait avec :

- championnat ;
- date du match ;
- équipe domicile ;
- équipe extérieure.

### Matching exact

Priorité au matching exact après normalisation :

```text
Date_Match + Equipe_Domicile_Normalisee + Equipe_Exterieur_Normalisee
```

### Matching fuzzy

Si le matching exact échoue, utiliser un matching approximatif contrôlé.

Exemples de cas :

```text
Nott'm Forest -> Nottingham Forest
Man City -> Manchester City
Ath Madrid -> Atletico Madrid
Real Betis -> Betis
```

### Règle de prudence

Les correspondances fuzzy doivent être marquées ou comptées pour vérification.

Dans notre résultat :

```text
144 correspondances fuzzy ont été signalées comme à vérifier.
```

Cela ne veut pas dire qu’elles sont fausses, mais qu’elles ont été rapprochées grâce à une similarité de noms plutôt qu’une égalité parfaite.

---

## Étape 8 — Ajouter les cotes au fichier principal

Pour chaque match trouvé :

1. Copier les cotes Bet365 si disponibles.
2. Copier les cotes Pinnacle si disponibles.
3. Copier les cotes William Hill si disponibles.
4. Si William Hill n’est pas disponible, utiliser Betfair Exchange comme troisième bookmaker.
5. Ajouter le nom du troisième bookmaker utilisé.
6. Ajouter éventuellement une colonne `Source_Cotes`.

### Exemple de colonnes ajoutées

```text
Bet365_1
Bet365_X
Bet365_2
Pinnacle_1
Pinnacle_X
Pinnacle_2
Bookmaker3_Nom
Bookmaker3_1
Bookmaker3_X
Bookmaker3_2
Source_Cotes
Matching_Cotes
```

---

## Étape 9 — Produire le fichier Excel final

### Feuille principale

Nom recommandé :

```text
Matchs + Coachs + Cotes
```

Contenu :

- matchs ;
- scores ;
- coachs ;
- cotes ;
- informations de matching.

### Feuille `Résumé`

Contenu recommandé :

```text
Nombre total de matchs
Nombre de matchs enrichis avec coachs
Nombre de matchs enrichis avec cotes
Nombre de matchs non matchés
Nombre de correspondances exactes
Nombre de correspondances fuzzy
Date de génération
```

### Feuille `Dictionnaire`

Contient la signification des colonnes.

Exemple :

```text
Bet365_1 = cote Bet365 pour victoire domicile
Bet365_X = cote Bet365 pour match nul
Bet365_2 = cote Bet365 pour victoire extérieur
Pinnacle_1 = cote Pinnacle pour victoire domicile
Pinnacle_X = cote Pinnacle pour match nul
Pinnacle_2 = cote Pinnacle pour victoire extérieur
```

### Feuille `Sources`

Contient :

- nom de la source ;
- URL ou description ;
- championnat ;
- saison ;
- date d’accès ou de génération.

---

## Étape 10 — Contrôles qualité finaux

### Contrôle coachs

Vérifier :

```text
Nombre de Coach Domicile vides
Nombre de Coach Extérieur vides
Nombre de A scraper (cf plan B)
Nombre de NON TROUVÉ
Nombre de À vérifier
```

### Contrôle cotes

Vérifier :

```text
Nombre total de matchs
Nombre de matchs avec Bet365_1/Bet365_X/Bet365_2
Nombre de matchs avec Pinnacle_1/Pinnacle_X/Pinnacle_2
Nombre de matchs avec Bookmaker3_1/Bookmaker3_X/Bookmaker3_2
Nombre de matchs non matchés
Nombre de matchs fuzzy
```

### Contrôle des dates

Vérifier que :

- les dates sont bien converties au même format ;
- il n’y a pas d’inversion jour/mois ;
- les saisons sportives sont correctement rattachées.

### Contrôle des équipes

Vérifier les clubs souvent problématiques :

```text
Manchester United
Manchester City
Tottenham Hotspur
Nottingham Forest
Brighton
Atletico Madrid
Athletic Bilbao
Real Betis
Bayern München
Borussia Mönchengladbach
Koln / Köln
```

---

## Limites connues

### Pour les coachs

Certaines périodes peuvent nécessiter une vérification manuelle, surtout lorsqu’il y a :

- coach intérimaire ;
- changement très proche du jour du match ;
- date officielle ambiguë ;
- différence entre date d’annonce et date de prise de fonction.

### Pour les cotes

Les cotes gratuites ne garantissent pas la présence de tous les bookmakers souhaités.

Dans notre cas, on a utilisé les bookmakers disponibles dans Football-Data, notamment :

- Bet365 ;
- Pinnacle ;
- William Hill ou Betfair Exchange selon disponibilité.

Les bookmakers comme :

- 1XBet ;
- Betclic ;

ne sont pas garantis dans les sources gratuites. Pour les obtenir de manière fiable, il faudrait probablement utiliser une API ou un fournisseur payant spécialisé dans les cotes historiques.

---

## Pseudo-code global

```python
# 1. Charger les matchs
matches = read_csv("matchs_2023_2025.csv", sep=";")

# 2. Charger ou construire le mapping coachs
coach_mapping = read_csv("mapping_coachs_intervalles.csv")

# 3. Pour chaque match, trouver les coachs
for match in matches:
    match["Coach Domicile"] = find_coach(
        club=match["Equipe Domicile"],
        date=match["Date"]
    )
    match["Coach Extérieur"] = find_coach(
        club=match["Equipe Extérieur"],
        date=match["Date"]
    )

# 4. Télécharger les fichiers Football-Data nécessaires
odds_files = download_football_data(
    leagues=["E0", "SP1", "D1"],
    seasons=["2223", "2324", "2425", "2526"]
)

# 5. Normaliser les noms d'équipes
matches = normalize_team_names(matches)
odds_data = normalize_team_names(odds_files)

# 6. Matcher les matchs avec les cotes
for match in matches:
    odds_row = find_odds_exact(match, odds_data)
    if odds_row is None:
        odds_row = find_odds_fuzzy(match, odds_data)

    if odds_row:
        add_odds_columns(match, odds_row)
    else:
        mark_unmatched(match)

# 7. Exporter CSV et Excel
write_csv(matches, "matchs_2023_2025_coachs_cotes_football_data.csv")
write_excel(
    matches=matches,
    mapping=coach_mapping,
    summary=summary,
    dictionary=dictionary,
    sources=sources,
    output="matchs_2023_2025_coachs_cotes_football_data.xlsx"
)
```

---

## Bonnes pratiques

1. Toujours conserver le fichier source original.
2. Ne jamais écraser le CSV initial.
3. Ajouter une feuille `Résumé` dans l’Excel final.
4. Ajouter une feuille `Sources`.
5. Conserver la table `mapping_coachs_intervalles.csv`.
6. Marquer les correspondances fuzzy.
7. Séparer les coachs et les cotes en étapes distinctes.
8. Documenter les abréviations des bookmakers.
9. Vérifier les matchs aux dates de changement de coach.
10. Vérifier les clubs dont les noms changent selon les sources.

---

## Résultat obtenu dans notre exécution

Dans notre cas, la procédure a produit :

```text
2 511 matchs enrichis
0 match non trouvé pour l’enrichissement des cotes Football-Data
3 jeux de cotes 1X2 ajoutés
144 correspondances fuzzy signalées pour vérification
```

Bookmakers ajoutés :

```text
Bet365
Pinnacle
William Hill quand disponible, sinon Betfair Exchange
```

Fichier final obtenu :

```text
matchs_2023_2025_coachs_cotes_football_data.xlsx
```

---

## Rejouer la procédure

Pour rejouer la procédure sur un nouveau fichier :

1. Préparer un CSV de matchs propre.
2. Vérifier les colonnes.
3. Construire ou mettre à jour `mapping_coachs_intervalles.csv`.
4. Enrichir les coachs.
5. Télécharger les fichiers Football-Data correspondant aux saisons.
6. Normaliser les noms d’équipes.
7. Matcher les cotes.
8. Exporter un CSV et un Excel.
9. Vérifier les lignes fuzzy ou non matchées.
10. Documenter les sources utilisées.

---

## Nom recommandé de la skill

```text
football-match-enrichment-skill
```

## Fichier principal de skill recommandé

```text
SKILL.md
```
