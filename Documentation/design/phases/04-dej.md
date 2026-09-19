# Phase — Pause déjeuner

`DayPhase.Lunch` · durée par défaut **420 s** (5–10 min réglable)

## But

La récréation du jeu et son gros morceau de fun : un self, une file, des plateaux, et
tout ce qu'il faut pour une bataille de nourriture. C'est aussi la phase la plus
libre : toute l'école est plus ou moins accessible pendant que les adultes convergent
vers le réfectoire.

## Contenu

### Le self

- Une **file** matérialisée : on avance, on ne choisit rien.
- En passant devant la chaîne, le **plateau se remplit tout seul** avec le menu du
  jour, tiré au sort comme le reste (cf. [`../items.md`](../items.md) § Nourriture).
- Le plateau est un objet tenu, et **c'est lui qui lance** : viser et cliquer envoie une
  portion. Un joueur ne tient qu'un objet à la fois dans ce jeu (`HeldItemTracker`
  indexe un objet par client), donc jongler entre plateau et purée serait pénible là où
  la blague doit être immédiate.

### La bataille

Ce n'est **pas une mécanique à écrire**. C'est : beaucoup d'objets lançables + une
pièce fermée + des adultes qui regardent. Lancer de la nourriture sur quelqu'un est une
bêtise ; se faire toucher n'a pas de conséquence mécanique (pas de dégâts), juste du
bruit, des traces et de la réput'.

## Règles serveur

- Multiplicateur de réput' **×1** : c'est facile mais massif, ça se compense par le
  volume.
- Le menu du jour est tiré par le serveur et répliqué en **index**.
- Le remplissage du plateau est serveur : le client avance, le serveur ajoute.

## Réseau — la phase la plus dangereuse du jeu

C'est **le pire cas réseau du projet**. Des dizaines de rigidbodies en vol, chez un
host qui est aussi un joueur.

- La nourriture **n'a pas** de réplication de rigidbody continue. On réplique
  l'impulsion (RPC), chacun simule localement, et seule la **position de repos** est
  corrigée par le serveur. Une pomme en vol n'a pas besoin d'être identique au
  centimètre près ; une pomme posée par terre, si.
- **Plafond dur** du nombre d'objets de nourriture vivants. Au-delà, les plus anciens
  sont dépawnés (avec un petit effet, pas une disparition sèche).
- Despawn différé des restes : le sol se nettoie tout seul en fin de phase.
- À mesurer avant d'aller plus loin : 4 joueurs, bataille pleine, bande passante et
  temps de frame du host.

## État du code

Implémenté : `ServingLine` (file, remplissage serveur), `Tray` (portions répliquées,
lancer), `MenuOfTheDay` sous forme de tirage d'index dans un catalogue de menus,
`LightProjectile` (impulsion répliquée, simulation locale, pose de repos corrigée),
`TransientItemBudget` (plafond + despawn du plus ancien), `MessyImpact` (toucher
quelqu'un, bonus sur un adulte). **Jamais mesuré en conditions réelles** : le test à
4 joueurs reste à faire.

## À implémenter

- [ ] Réfectoire dans la greybox (`SchoolBuilder`), sous la racine `School` pour le NavMesh.
- [ ] File + `TrayItem` qui se remplit en avançant.
- [ ] `MenuOfTheDay` (tirage serveur, index répliqué).
- [ ] Projectiles de nourriture « impulsion + simulation locale », plafond, despawn différé.
- [ ] Traces au sol (décalques purement cosmétiques, jamais répliquées).

## Ouvert

- Est-ce que les adultes peuvent être touchés, et est-ce que ça vaut plus ? Oui, c'est
  évidemment le geste que les joueurs vont chercher — à chiffrer en jouant.
- Faut-il pouvoir sortir de la file sans plateau ? Oui, rien ne doit jamais bloquer
  le joueur.
