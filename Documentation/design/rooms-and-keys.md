# Les salles, les portes et les clés

## Les salles

| Salle | Rôle | Ouverte quand |
|---|---|---|
| Couloir / cour | circulation | toujours |
| Salle générale | cours de français / maths / philo, **fixe pour le cycle** | pendant son cours |
| Salle de techno | cours de techno | pendant son cours |
| Labo | physique-chimie | pendant son cours |
| Gymnase | EPS | pendant son cours |
| Réfectoire | self | pendant le déjeuner |
| WC | planque, respiration | toujours |
| Bureau du CPE | salle de colle | c'est une prison, pas une salle |
| Loge | où traînent des clés | fermée, 20 % ouverte |

Toutes doivent être construites **sous la racine `School`** dans `SchoolBuilder` :
`RuntimeNavMeshBuilder` ne prend que ce qui est dessous, sinon les adultes traversent
les murs ou restent bloqués.

## Les portes

Une salle hors cours est fermée **80 % du temps** — tirage par salle et par jour, côté
serveur, pendant [l'arrivée](phases/01-arrivee.md).

- Fermée ≠ inaccessible : il y a toujours un chemin (clé, fenêtre, diversion).
- La salle du cours en cours est **forcée ouverte** pendant sa phase, quel qu'ait été
  le tirage.
- Une salle ouverte le reste jusqu'à la fin de la phase : pas de porte qui se referme
  dans le dos du joueur.

### Autorité — le point à ne pas rater

```
client : « j'essaie d'ouvrir »   ──RPC(SendTo.Server)──►   serveur : ai-je la clé ?
                                                            └─► NetworkVariable<bool> _open
```

- `Door : NetworkBehaviour` avec `NetworkVariable<bool> _open` et
  `NetworkVariable<bool> _locked`.
- L'ouverture passe par une `OpenDoorRule : IInteractionRule`, enregistrée **entre
  `GrabRule` et `ThrowRule`**.
- L'identité vient de `rpcParams.Receive.SenderClientId`, **jamais** d'un id envoyé.
- Un client n'écrit jamais l'état d'une porte. Sinon n'importe qui ouvre tout, et ça
  ne se verra qu'en partie publique.

## Les clés

- Une clé est un `GrabbableItem` portant un `RoomId`.
- Où elles sont : **trousseau du surveillant** (il faut le suivre, ou le distraire),
  **loge**, **bureau du CPE** (donc accessible… quand on est collé, ce qui est le gag).
- Voler une clé publie un `MischiefReport` : c'est une bêtise, ça rapporte, et ça rend
  *recherché*.
- Une clé se prête, se lance, se perd. Elle circule dans l'équipe — c'est le levier
  coopératif de cette mécanique.

## Réseau

- L'état verrouillé/ouvert de chaque porte est **répliqué** : un joueur qui rejoint à
  14h doit voir l'école telle qu'elle est.
- Le tirage 80/20 est serveur, une seule fois par jour. Jamais de `Random` client sur
  du gameplay.
- Les clés sont des objets réseau ordinaires, mais **importantes** : réplication
  sérieuse (pas le modèle « projectile » de la nourriture).

## À implémenter

- [ ] `Door` + `OpenDoorRule` + `RoomId`.
- [ ] Tirage 80/20 serveur, stocké dans `DayState`.
- [ ] Nouvelles salles dans `SchoolBuilder`, sous `School`.
- [ ] `SchoolLayout` étendu : une ancre + des marqueurs d'items par salle.

## Ouvert

- Fenêtres / passages alternatifs : prévus dans l'esprit (« fermée ≠ inaccessible »),
  pas dans le proto. À ajouter dès qu'une salle devient frustrante.
- Crocheter une porte (mini-jeu, temps d'action, bruit) ? Bonne alternative à la clé,
  mais c'est une mécanique de plus : après le proto.
