# Phase — Arrivée

`DayPhase.Arrival` · durée par défaut **20 s**

## But

Poser les joueurs dans le monde en même temps, leur dire quel jour on est et ce qui
les attend, sans rien leur faire manquer. C'est la seule phase où il ne se passe
délibérément rien : elle absorbe les écarts de chargement entre clients.

## Contenu

- Spawn de tous les élèves au portail (`ISpawnPointProvider`, inchangé).
- HUD d'accueil : « Jour 2/3 — cycle 1 », quota du cycle, progression actuelle.
- Annonce des deux cours du jour **et de leurs salles** : les joueurs doivent pouvoir
  planifier avant que le chrono compte.
- Les portes de salles sont toutes fermées. Le couloir et la cour sont ouverts.

## Règles serveur

- Le tirage du jour a lieu **ici** : cours 1, cours 2 (distincts), verrouillage 80/20
  des salles, menu du self. Tout est figé avant que la phase suivante commence.
- Aucune bêtise n'est créditée pendant `Arrival` (multiplicateur 0). Sinon la phase
  gratuite devient la phase optimale.

## Réseau

- La phase et son `endsAt` sont des `NetworkVariable` sur `DayState`. Le client
  n'affiche que `endsAt - ServerTime.Time`.
- Un retardataire qui se connecte pendant `Arrival` voit le même tirage : il lit des
  `NetworkVariable`, il ne reçoit pas un RPC.
- Le host est un joueur : il spawn comme les autres, il n'a aucune avance.

## À implémenter

- [ ] `DayPhase.Arrival` dans la machine à phases.
- [ ] Tirage serveur du jour (cours, portes, menu), répliqué en index.
- [ ] Panneau HUD d'accueil, lu depuis `IDayState`.

## Ouvert

- Faut-il un « bus scolaire » / une animation d'arrivée, ou un spawn sec ? Spawn sec
  pour le proto.
