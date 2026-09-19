# Progression — cycles, quotas, paliers

## La boucle

```
Jour 1 ──► Jour 2 ──► Jour 3 ──► verdict du cycle
                                     │
                    quota atteint ───┴─── quota raté
                          │                    │
                  cycle suivant          retour au jour 1
                (quota ↑, palier ↑)       (même cycle)
```

- Le quota s'évalue **tous les trois jours de cours**, pas chaque jour. Une mauvaise
  journée se rattrape ; une mauvaise série, non.
- **Il n'y a pas de fin.** Rater renvoie au jour 1 du cycle, jamais à un game over.
  Le jeu est une boucle qu'on entretient, pas une campagne qu'on termine.

## Le quota

- Quota du cycle N = `base × croissance^(N-1)` (aujourd'hui : 100, ×1,5 — à régler).
- Il porte sur la **réput' d'équipe cumulée sur les trois jours**.
- Il est affiché en permanence (HUD) et détaillé au [récap](phases/07-recap.md).

## Les paliers de déblocage

C'est **la raison de relancer**. Chaque cycle franchi ouvre quelque chose de neuf, et
c'est annoncé à l'écran au moment du verdict.

Un palier est une donnée : `ProgressionTier` (ScriptableObject, liste ordonnée).

```csharp
// Documentation seulement : la forme visée.
public sealed class ProgressionTier : ScriptableObject
{
    public string Headline;               // « Le labo ouvre ses portes »
    public CourseDefinition[] AddCourses; // matières qui entrent dans le tirage
    public GameObject[] AddItems;         // items qui entrent dans les salles
    public int ExtraAdults;               // surveillance en plus
    public float AdultVigilanceScale;     // la difficulté qui monte
}
```

### Esquisse de courbe

| Cycle | Ce qui s'ouvre | Ce qui durcit |
|---|---|---|
| 1 | cours généraux + EPS | — |
| 2 | techno (et le vol de PC) | un surveillant de plus |
| 3 | physique-chimie et ses réactions | vue des adultes allongée |
| 4 | sarbacane / craft, items avancés | les salles ferment plus souvent (90/10) |
| 5+ | rotations d'items, nouveaux objectifs secondaires | vigilance croissante, plafonnée |

Deux règles pour ne pas casser le jeu :

1. **La difficulté est plafonnée**, le contenu ne l'est pas. Un jeu infini qui devient
   injouable n'est pas infini.
2. **Un palier ajoute toujours quelque chose à faire**, pas seulement quelque chose à
   subir. Un cycle qui n'apporte qu'un surveillant de plus est un cycle raté.

## Portée : par session, pas persistante

La progression vit **dans la session** : si le host quitte, tout repart à zéro. C'est
cohérent avec `CLAUDE.md` §8 (pas de persistance, pas de host migration), et ça évite
un chantier de sauvegarde pour un proto. Le jour où la persistance arrive, elle
s'ajoutera au-dessus de `ProgressionTier` sans le changer.

## Réseau

- Cycle courant, jour dans le cycle, quota, cumul d'équipe : **tous des
  `NetworkVariable`**. Un joueur qui rejoint au jour 2 doit voir où en est l'équipe.
- Le palier actif est un **index** répliqué dans la liste ordonnée : catalogues
  identiques partout puisque le build l'est.
- Le passage d'un cycle au suivant est décidé **serveur**, sur `RequestNextDay()`
  host-only déjà vérifié par `SenderClientId`.
- Un palier qui ajoute des prefabs ne change rien au hash : tout est déjà dans
  `HoldMyBeerNetworkPrefabs.asset` dès le départ, seul le **tirage** s'élargit.
  **Ne jamais** charger des prefabs réseau en cours de session.

## À implémenter

- [ ] Compteur jour-dans-le-cycle, verdict tous les trois jours.
- [ ] `ProgressionTier` + liste ordonnée + index répliqué.
- [ ] Annonce du palier au récap.
- [ ] Plafond de difficulté.

## Ouvert

- Trois jours est-il le bon rythme ? À tester : trois jours × ~17 min = ~50 min de
  partie par cycle, ce qui est long pour un premier essai. Peut-être raccourcir les
  phases avant de toucher au nombre de jours.
