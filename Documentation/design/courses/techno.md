# Cours — Techno

**Salle :** salle de techno · **Multiplicateur :** ×1,5 dans sa salle

## Ce que la matière apporte

Du matériel fragile, cher, et **volable**. C'est le cours des objectifs discrets :
pas le bordel le plus bruyant, mais le plus rentable si on ne se fait pas prendre.

## Items

| Item | Ce qu'il fait |
|---|---|
| **PC** | lourd : ralentit, se voit dans les mains, impossible à cacher. Objet du vol. |
| **Arduino** | se branche sur un poste et le fait dérailler (écran, bruit, lumière). Bêtise à retardement : on le pose, ça part plus tard, on est ailleurs. |
| **Câble / souris** | petit, sans valeur, mais se lance et se vole pour saboter un poste. |

Détail des valeurs : [`../items.md`](../items.md).

## Objectif secondaire : voler un PC

Le premier `ISideObjective` du jeu, et le modèle des suivants.

- Prendre un PC en salle techno et **le sortir de l'école** sans être vu.
- Le porter est visible et ralentit : on ne peut pas courir avec.
- Réussi → grosse réput'. Vu en le portant → la bêtise est annulée, le PC retourne
  en salle.
- Ce n'est **pas une mécanique nouvelle** : item lourd + zone de sortie + le système
  de suspicion existant.

## L'Arduino (à creuser)

L'idée à garder : une bêtise **différée** et **délocalisée**. On la pose pendant le
cours, elle part pendant le dej, et personne ne regarde le coupable. Ça crée un jeu
de planification qui n'existe nulle part ailleurs dans le jeu.
Reste à trancher : délai fixe ou réglable, un seul actif à la fois ?

## Contrainte spatiale

Salle étroite, pleine de postes : peu d'angles morts, beaucoup d'obstacles. On y est
vu vite, mais on s'y cache bien.

## Réseau

- Un PC porté doit répliquer son porteur de façon fiable (c'est un objectif, pas un
  gag) : état sur le PC, pas un RPC.
- L'Arduino est un **état** (posé, armé, déclenché), donc `NetworkVariable` : un joueur
  qui rejoint entre la pose et le déclenchement doit voir la salle dans le bon état.

## Ouvert

- Est-ce qu'un PC volé se revend / compte pour le quota, ou est-ce un score à part ?
  Proposition : il compte pour le quota, très cher, pour que le vol soit une vraie
  stratégie d'équipe.
