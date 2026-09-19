# Phase — Pause du matin

`DayPhase.MorningBreak` · durée par défaut **90 s**

## But

Le temps de repérage. On sait quels cours vont tomber et où ; on a 1 à 2 minutes pour
se placer, chercher une clé, planquer un objet, ou faire une première bêtise à faible
valeur. C'est la phase qui apprend la carte aux nouveaux joueurs.

## Contenu

- Couloirs, cour, WC ouverts. Salles de cours fermées (sauf tirage 20 %).
- Le surveillant fait sa ronde normale.
- Les bêtises « de décor » (alarme, tableau, pétards) sont disponibles, au
  multiplicateur de pause.

## Règles serveur

- Multiplicateur de réput' **×0,5** : faire une bêtise quand personne n'est en cours,
  c'est facile, donc ça vaut peu.
- Les clés sont déjà en place (trousseau du surveillant, loge, bureau CPE) : voler une
  clé pendant la pause est le jeu prévu de cette phase.

## Réseau

- Rien de neuf : c'est la phase « proto actuel ». Elle sert de garde-fou — si quelque
  chose désynchronise ici, c'est la machine à phases elle-même.
- Les items de cours ne sont **pas** spawnés : on garde le compte d'objets réseau bas
  tant qu'aucune salle n'est ouverte.

## À implémenter

- [ ] Multiplicateur de réput' par phase, lu depuis la `DaySchedule`.
- [ ] Les clés comme `GrabbableItem` + `MischiefReport` au vol.

## Ouvert

- Une sonnerie d'avertissement 15 s avant la fin ? Oui, c'est gratuit et ça évite la
  frustration d'être pris hors salle sans prévenir.
