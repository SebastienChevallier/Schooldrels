# Cours — Français / Maths / Philo (cours généraux)

**Salle :** la salle générale, **fixe pour tout le cycle** · **Multiplicateur :** ×1,5

## Ce que la matière apporte

Le cours par défaut, et donc le terrain que les joueurs connaîtront le mieux. Les
trois matières partagent la même salle et les mêmes items : ce qui change, c'est
l'habillage (le tableau, le prof, l'ambiance), pas le jeu.

**Pourquoi une salle fixe :** si tout bouge tous les jours, personne n'apprend la
carte. Les cours généraux sont le repère stable du cycle ; c'est l'EPS, la techno et
la chimie qui bougent.

## Items

| Item | Ce qu'il fait |
|---|---|
| **Craie** | se lance, tache, fait peu de bruit. Le projectile de base, disponible partout. |
| **Effaceur** | plus lourd, plus bruyant. Effacer le tableau du prof en plein cours est une bêtise en soi. |
| **Sarbacane artisanale** | tir précis et silencieux à distance, chargé au maintien du clic. Le seul moyen de faire une bêtise **sans être à côté**. |
| **Trousse / cahier** | consommables à lancer, remplissage de salle. |

Détail des valeurs : [`../items.md`](../items.md).

## Artisanat (à trancher)

La sarbacane suggère un petit système de **craft** : un tube (techno) + du papier
(cours généraux) = sarbacane. Séduisant parce que ça relie les salles entre elles et
justifie de circuler.

Position pour le proto : **la sarbacane existe comme item simple**, spawné en salle
générale. Le craft est un palier de progression plus tard
([`../progression.md`](../progression.md)), pas un prérequis. On ne construit pas un
système d'artisanat pour un seul objet.

## Contrainte spatiale

Rangées de tables : beaucoup de couvert bas, le prof voit la salle de face depuis le
tableau. C'est le cours où **se baisser** paie.

## Réseau

- La sarbacane est une règle d'interaction chargée (`IsCharged = true`), comme le
  lancer : rien de neuf côté réseau, un projectile court-vie de plus.
- Le tableau taggué est déjà un `PrankTarget` : aucun code.
