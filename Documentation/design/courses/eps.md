# Cours — EPS

**Salle :** gymnase · **Multiplicateur :** ×1,5 dans sa salle

## Ce que la matière apporte

Le seul grand volume du jeu. Pas de tables, pas de couloirs : de l'espace, de la
hauteur, et des objets faits pour être lancés loin. C'est le cours où la physique est
le sujet, pas l'accident.

## Items

| Item | Ce qu'il fait |
|---|---|
| **Ballon** | gros, rebondit bien, se lance loin. Toucher un adulte = bêtise sonore. |
| **Balle** | petite, rapide, précise. Peu de bruit : la bêtise discrète. |
| **Sifflet** | **fait sonner un faux coup de sifflet** : attire les adultes vers un point choisi. L'outil de diversion du jeu. |
| **Tapis / plot** | mobilier déplaçable, sert de cachette et de blocage de porte. |

Détail des valeurs : [`../items.md`](../items.md).

## Bêtises propres

- Envoyer un ballon dans les projecteurs / au panier hors cours.
- Siffler pour vider un couloir, puis passer ailleurs — la diversion vaut peu en
  réput' directe mais beaucoup en possibilités.
- Bloquer une porte avec un tapis pour retarder un poursuivant.

## Contrainte spatiale

Le gymnase est grand et dégagé : **on y est vu de loin**. C'est le cours où la vue du
prof porte le plus, et où la fuite est la moins évidente.

## Réseau

- Le ballon est le pire objet physique unitaire du jeu (gros, rapide, rebondissant) :
  il justifie à lui seul le modèle « impulsion répliquée + simulation locale + position
  de repos corrigée » décrit dans [`../phases/04-dej.md`](../phases/04-dej.md).
- Le sifflet publie un événement de bruit sur le `MischiefBus` : c'est un **événement**,
  donc un RPC, pas un état.

## Ouvert

- Des « cours d'EPS » réels (course, relais) qu'on peut saboter ? Beaucoup de contenu
  pour peu de systèmes nouveaux — après le proto.
