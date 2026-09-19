# Les items

## Ce qu'est un item

Un prefab avec un `NetworkObject`, un `GrabbableItem`, et éventuellement une règle
d'interaction qui lui donne un pouvoir. **Tout item est lançable** : c'est la règle de
base du jeu (`ThrowRule` accepte tout ce qu'on tient, avec une charge au maintien du
clic). Un item « qui ne fait rien » est donc déjà un projectile — c'est suffisant pour
exister.

Un item se décrit par cinq choses :

| Champ | Sens |
|---|---|
| **Poids** | léger / moyen / lourd. Un item lourd ralentit, se voit, ne se cache pas. |
| **Bruit** | rayon d'attraction des adultes quand il sert ou qu'il tombe. `0` = silencieux. |
| **Réput'** | ce que rapporte son usage détourné. `0` = outil, pas bêtise. |
| **Source** | la salle/phase où il apparaît. |
| **Effet** | la règle d'interaction associée, s'il y en a une. |

Les valeurs ci-dessous sont des **points de départ**, à régler en jouant. Elles vivent
dans les prefabs et les `CourseDefinition`, pas en dur dans le code.

---

## Catalogue

### Partout / décor (déjà en place)

| Item | Poids | Bruit | Réput' | Effet |
|---|---|---|---|---|
| Alarme incendie | fixe | très fort | élevée | `PrankTarget` : vide un couloir, attire tous les adultes |
| Tableau | fixe | nul | moyenne | `PrankTarget` : tag, effet durable et visible |
| Pétard | léger | fort | moyenne | bêtise à effet différé, publie un `MischiefReport` propre |
| Clé | léger | nul | faible | ouvre une salle (cf. [`rooms-and-keys.md`](rooms-and-keys.md)) ; la voler est déjà une bêtise |

### EPS — [`courses/eps.md`](courses/eps.md)

| Item | Poids | Bruit | Réput' | Effet |
|---|---|---|---|---|
| Ballon | moyen | moyen | moyenne | rebondit, se lance loin ; toucher un adulte compte |
| Balle | léger | faible | faible | rapide et précise, la bêtise discrète |
| Sifflet | léger | **très fort, à volonté** | nulle | **diversion** : attire les adultes vers un point choisi |
| Tapis / plot | lourd | faible | nulle | cachette, blocage de porte |

### Techno — [`courses/techno.md`](courses/techno.md)

| Item | Poids | Bruit | Réput' | Effet |
|---|---|---|---|---|
| PC | **lourd** | faible | **très élevée** | objectif secondaire : le sortir de l'école sans être vu |
| Arduino | léger | nul à la pose | élevée | **bêtise différée** : posé sur un poste, part plus tard, ailleurs |
| Câble / souris | léger | faible | faible | sabotage d'un poste, projectile |

### Cours généraux — [`courses/general.md`](courses/general.md)

| Item | Poids | Bruit | Réput' | Effet |
|---|---|---|---|---|
| Craie | léger | nul | faible | le projectile de base, tache |
| Effaceur | moyen | moyen | faible | effacer le tableau en plein cours est une bêtise |
| Sarbacane | léger | **nul** | moyenne | **tir chargé à distance** : la seule bêtise qui ne demande pas d'être à côté |
| Trousse / cahier | léger | faible | nulle | remplissage, projectile |

### Physique-Chimie — [`courses/physique-chimie.md`](courses/physique-chimie.md)

| Item | Poids | Bruit | Réput' | Effet |
|---|---|---|---|---|
| Fiole / bécher | léger | faible | dépend du contenu | contient un réactif, se verse et se mélange |
| Réactifs A/B/C | léger | nul | nulle seuls | **couples** : fumée, mousse, explosion |
| Pissette à eau | moyen | faible | faible | jet à distance, déclenche une préparation, rechargeable |
| Plaque chauffante | fixe | faible | — | source de chaleur : chauffer une fiole = réaction |

### Nourriture (self) — [`phases/04-dej.md`](phases/04-dej.md)

| Item | Poids | Bruit | Réput' | Effet |
|---|---|---|---|---|
| Plateau | moyen | fort | moyenne | se porte, se vide, se lance entier |
| Purée / plat du jour | léger | faible | faible | tache, glisse au sol |
| Fruit | léger | faible | faible | dur, rebondit, précis |
| Dessert / brique | léger | faible | faible | remplissage de bataille |

Le **menu du jour est tiré au sort** : la composition du plateau change chaque jour,
comme les cours.

---

## Règles transversales

### Ce qui fait un bon item

1. Il apporte un **verbe** que rien d'autre n'apporte (siffler, différer, tirer de
   loin, mélanger), **ou** il est un projectile assumé et il est là pour le volume.
2. Son pouvoir est lisible en une phrase à l'écran (`CanApply` remplit l'invite).
3. Il a un **coût** : bruit, poids, visibilité, ou rareté. Un item sans coût casse la
   phase où il apparaît.

### Comment on en ajoute un

Recette complète dans `CLAUDE.md` §5. En résumé :

1. Prefab + `NetworkObject` + `GrabbableItem`, dans `SchoolPrefabs`.
2. Ajouté à `HoldMyBeerNetworkPrefabs.asset` — **sinon déconnexion sèche du client**.
3. S'il a un pouvoir : une `IInteractionRule`, enregistrée **entre `GrabRule` et
   `ThrowRule`** (l'ordre est la priorité, `ThrowRule` accepte tout).
4. S'il rapporte : il publie un `MischiefReport` sur le `MischiefBus`. `DayState` le
   crédite, les adultes l'entendent, sans que personne se connaisse.
5. Référencé dans la `CourseDefinition` de sa matière pour être spawné avec la salle.

### Réseau — ce qui compte vraiment

- **Compte d'objets vivants.** Les items sont spawnés à l'ouverture d'une salle et
  dépawnés à sa fermeture, spawn et despawn étalés sur quelques frames. Un plafond
  global protège le host ; au-delà, les plus anciens partent.
- **Projectiles : impulsion répliquée, simulation locale, position de repos corrigée.**
  Pas de réplication continue de rigidbody sur de la nourriture ou des craies. Un objet
  *en vol* n'a pas besoin d'être identique au centimètre ; un objet *posé*, si.
  Exception : les items d'objectif (le PC) sont répliqués sérieusement.
- **État vs événement.** Un Arduino armé, une fiole pleine, une porte ouverte, un PC
  porté : ce sont des **états** → `NetworkVariable`. Un sifflet, une explosion, un
  impact : ce sont des **événements** → RPC. Un joueur qui rejoint en cours de journée
  doit voir tous les états, il ne rattrapera jamais les événements.
- **Autorité.** Le client vise et demande, le serveur applique. `CanApply` tourne aussi
  côté client, mais **uniquement pour afficher l'invite**, et sans aucun effet de bord.
