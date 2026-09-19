# Les cours

## Le principe

Deux cours par jour, **tirés au sort par le serveur**, jamais les mêmes le même jour.
Chaque cours = une matière, une salle, un jeu d'items, un multiplicateur.

Une matière est une **donnée**, pas du code : `CourseDefinition` (ScriptableObject).
Ajouter une matière = ajouter un asset et ses prefabs à la liste réseau. Rien à
recompiler côté systèmes.

```csharp
// Documentation seulement : la forme visée.
public sealed class CourseDefinition : ScriptableObject
{
    public string DisplayName;      // « Physique-Chimie »
    public RoomId Room;             // la salle qui lui correspond
    public GameObject[] Items;      // spawnés à l'ouverture, dépawnés à la fermeture
    public float ReputationScale;   // ×1,5 par défaut dans sa propre salle
    public SideObjective Objective; // optionnel
}
```

## Le tirage

- Il a lieu pendant [l'arrivée](../phases/01-arrivee.md), côté **serveur**.
- Ce qui transite, c'est l'**index dans le catalogue** (un `byte`), jamais l'objet :
  les builds sont identiques, donc l'index suffit. Un `Random` côté client sur du
  gameplay est un bug, pas une optimisation.
- Contrainte : cours 1 ≠ cours 2.
- Les **cours généraux** (français, maths, philo) partagent **une salle fixe pour tout
  le cycle**, tirée au premier jour. Les joueurs doivent pouvoir apprendre une carte.

## Les matières

| Matière | Salle | Doc |
|---|---|---|
| EPS | gymnase | [`eps.md`](eps.md) |
| Techno | salle de techno | [`techno.md`](techno.md) |
| Français / Maths / Philo | salle générale (fixe pour le cycle) | [`general.md`](general.md) |
| Physique-Chimie | labo | [`physique-chimie.md`](physique-chimie.md) |

## Ce qu'une matière doit apporter

Une matière n'est intéressante que si elle change **la façon de jouer**, pas seulement
le décor. Grille de validation avant d'en ajouter une :

1. Elle apporte au moins un item qui n'existe nulle part ailleurs.
2. Cet item ouvre une bêtise qu'on ne peut pas faire ailleurs.
3. La salle a une contrainte spatiale propre (un gymnase ouvert ≠ un labo étroit).

## Réseau

- Index répliqué, catalogue identique partout.
- Items spawnés/dépawnés par phase, spawn étalé, plafond global.
- **Tous** les prefabs d'items dans `HoldMyBeerNetworkPrefabs.asset`, même ordre partout.
