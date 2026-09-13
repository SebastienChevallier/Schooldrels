# Mains IK, objets attrapables et interactions contextuelles — design

Date : 2026-09-13
Statut : validé, prêt à planifier

## But

Des mains pilotées par IK plutôt que par des animations de grab, un objet attrapable
qu'on peut prendre et lancer, et un système d'interactions contextuelles générique :
viser une cible en tenant un objet déclenche une action qui dépend du couple
(objet tenu, cible visée).

L'exemple de référence — un verre vide qu'on peut lancer, ou remplir si on vise une
tireuse à bière — n'est **pas** implémenté. Seule l'abstraction qui le rendra trivial
l'est, exercée par les deux règles dont on a besoin de toute façon.

## Principe directeur : tout est une interaction

Il n'y a pas de touche « attraper » ni de touche « lancer ». Il y a **une action**, qui
traverse un registre de règles ordonné. La première règle qui accepte gagne :

| Ordre | Règle | Condition | Effet |
|---|---|---|---|
| 1 | `GrabRule` | mains vides, on vise un `IGrabbable` à portée | prendre |
| … | *règles de jeu* | *(verre vide + tireuse → verre plein)* | *à venir* |
| n | `ThrowRule` | on tient quelque chose | lancer |

C'est exactement la forme de `IConnectionApprovalPolicy` déjà en place : des règles qui
se composent, enregistrées une par une dans `GameBootstrapper.BuildContainer()`. Ajouter
une interaction = ajouter une classe et une ligne. Ni le joueur, ni le réseau, ni l'IK ne
bougent.

Conséquence assumée : l'ordre d'enregistrement **est** la priorité. Une règle de jeu
insérée après `ThrowRule` ne se déclenchera jamais, puisque `ThrowRule` accepte tout dès
qu'on tient un objet. C'est le même piège que dans les politiques d'approbation, et la
même discipline.

## Assemblies

Le composant qui fait l'IK et demande l'interaction vit sur le joueur, donc dans
`HoldMyBeer.Player`. Les règles sont du gameplay, donc dans `HoldMyBeer.Gameplay`. Or ces
deux assemblies sont des frères : aucune ne voit l'autre, et remonter une flèche est
interdit. Il manque donc une couche en dessous.

```
        HoldMyBeer.Gameplay     HoldMyBeer.Player
                    \             /
                HoldMyBeer.Interaction     ← contrats seuls
                          |
                    HoldMyBeer.Core
```

`HoldMyBeer.Interaction` ne contient **que** des interfaces et des structs de données :
aucun `MonoBehaviour`, aucune référence à NGO. C'est ce que `CLAUDE.md` §3 appelle
« il manque une interface dans la couche du dessous ».

Alternative écartée : faire référencer `Gameplay` par `Player`. Ça compile et ne crée pas
de cycle, mais ça transforme deux frères en chaîne et rend le prochain découpage plus
confus pour un gain nul.

## Contrats (`HoldMyBeer.Interaction`)

| Type | Rôle |
|---|---|
| `IGrabbable` | ce qu'une main peut tenir : point de préhension, masse |
| `IInteractionTarget` | ce qu'on peut viser : une tireuse, une table, un autre joueur |
| `InteractionRequest` | struct : qui demande, ce qu'il tient, ce qu'il vise, le point d'impact, la pose et la vélocité de la main |
| `IInteractionRule` | une règle |
| `IInteractionContext` | ce qu'une règle a le droit de faire |
| `IInteractionRegistry` | la liste ordonnée, `AddRule` / `TryResolve` |

### La règle est coupée en deux, volontairement

```csharp
public interface IInteractionRule
{
    /// Pur, sans effet de bord. Tourne aussi bien sur le client (pour afficher
    /// « Remplir ») que sur le serveur (pour choisir quoi appliquer).
    bool CanApply(in InteractionRequest request, out string prompt);

    /// Serveur uniquement, appelé seulement après un CanApply positif côté serveur.
    void Apply(in InteractionRequest request, IInteractionContext context);
}
```

Séparer la question de l'action a deux raisons concrètes. Le client doit pouvoir savoir
ce qui *se passerait* pour afficher une invite, sans rien déclencher ni faire d'aller-
retour réseau. Et le serveur doit pouvoir refuser sans que la règle ait déjà muté quoi que
ce soit. Un `TryExecute` unique interdirait les deux.

`IInteractionContext` est l'inversion de dépendance qui compte : une règle ne touche
jamais `NetworkManager` ni `NetworkObject`. Elle demande `Hold(...)`, `Release(...)`,
`Despawn(...)`, `Spawn(...)` et le contexte s'en charge. Une règle reste donc du code de
jeu pur, lisible et déplaçable.

## Réseau

### Une seule source de vérité, et elle est sur l'objet

`GrabbableItem` porte un `NetworkVariable<ulong> holder` (écriture serveur,
`ulong.MaxValue` = libre) et la main utilisée. Le joueur ne réplique **rien** sur ce qu'il
tient : chaque client tient un dictionnaire local `clientId → objet`, alimenté par le
`OnValueChanged` de l'objet.

Deux variables répliquées décrivant le même fait finissent toujours par diverger, et le
bug serait « le verre est dans ma main chez moi et par terre chez toi ». Les types
répliqués sont `ulong` et `byte`, `unmanaged`, conformes à `CLAUDE.md` §4.3.

### L'attache est locale, et ne passe pas par la parenté NGO

Tant que l'objet est tenu, chaque client repositionne son transform en `LateUpdate` sur le
point de préhension de **sa propre** main physique. Aucune parenté réseau, aucune
réplication de position.

C'est la même philosophie que le wobble : le ragdoll n'est jamais répliqué, chaque client
le simule. Un objet attaché à un os répliqué serait incohérent ; un objet attaché à l'os
local est parfaitement collé à la main partout. Le prix est que la position exacte du
verre diffère de quelques centimètres d'une machine à l'autre — sans importance tant
qu'on ne vise pas avec.

Pendant la prise, le `NetworkTransform` de l'objet est **désactivé** partout, sinon il se
bat avec l'attache locale et réplique la position divergente de chaque machine. L'état de
prise étant répliqué, tous les clients le désactivent au même moment logique.

### Le lancer

L'owner envoie sa pose de relâchement et sa vélocité ; le serveur **plafonne la
magnitude**, écrit la pose, libère le `holder`, réactive le `NetworkTransform`.

Le serveur ne recalcule pas la pose parce qu'il ne connaît pas la position de la main : le
ragdoll n'est pas répliqué. C'est le même arbitrage que pour le mouvement, déjà assumé
dans `CLAUDE.md` §4.1 — un tricheur peut lancer depuis un point légèrement faux, et le
plafond de vélocité empêche le seul cas vraiment gênant.

Il y aura une petite correction visible au relâchement, le temps que le serveur impose sa
pose. C'est le bon endroit pour payer : la prise, qui dure, est parfaite ; le lancer, qui
est bref, absorbe le décalage.

### Contention

Deux joueurs qui attrapent le même verre dans le même tick : le serveur traite les RPC en
série, le premier gagne, le second échoue parce que `holder` n'est plus libre. Le serveur
valide aussi une distance maximale par rapport à la **racine répliquée** du demandeur —
généreuse, puisqu'il ne peut pas connaître la vraie position de la main.

## Mains et IK

### L'IK pilote le rig animé, pas le rig physique

`Animator.SetIKPosition` sur l'`AnimatedRig` déplace la main animée ; les slerp drives des
`WobbleBone` tirent le bras physique derrière. Le ballottement vient donc gratuitement de
la chaîne déjà construite — c'est précisément ce qu'on veut : pas d'animation de grab, une
cible qui bouge et un bras mou qui la rattrape.

Y Bot est en Humanoid, donc l'IK est disponible. L'**IK Pass** doit être activée sur la
couche de base d'`AC_Player` ; c'est fait par code, comme le reste des assets.

### Deux corps physiques de plus

Le ragdoll s'arrête aujourd'hui aux avant-bras. `mixamorig:LeftHand` et
`mixamorig:RightHand` sont ajoutés à `PlayerRagdollBuilder` : ça donne un point d'accroche
physique réel pour l'objet, et un segment de plus qui ballotte au bout du bras.

### Le composant IK vit sur l'AnimatedRig, pas sur la racine

`OnAnimatorIK` n'est appelé que sur le `GameObject` qui porte l'`Animator`, lequel est
l'`AnimatedRig`, enfant du joueur. Le découpage suit cette contrainte plutôt que de la
contourner :

| Composant | Où | Rôle |
|---|---|---|
| `PlayerHands` | racine du joueur | décide où vont les mains et ce qu'elles tiennent |
| `HandIkDriver` | `AnimatedRig` | ne fait qu'écrire les buts IK dans `OnAnimatorIK` |
| `PlayerInteractor` | racine du joueur | vise, résout, envoie les RPC |

`HandIkDriver` est volontairement bête : il reçoit une position, une rotation et un poids
par main, et les applique. Toute la décision est dans `PlayerHands`, qui est testable sans
`Animator`.

### Trois états de main

Au repos, le but IK est un point devant la poitrine, orienté par le pivot caméra : les
mains suivent le regard, ce qui donne le flottement permanent recherché. En approche, le
but glisse vers le point de préhension de l'objet visé, poids en rampe. En prise, le but
est une pose de port, et l'objet est placé pour que son ancre coïncide avec la paume.

La main droite est prioritaire ; la gauche prend le relais si la droite est occupée.

## Entrée

`IPlayerInputSource` gagne un seul membre : `InteractPressedThisFrame`. Une action, une
touche, tout passe par le registre. Le reste de l'interface ne bouge pas.

## Prefab d'objet attrapable

Généré par code comme le reste, dans `Prefabs/Grabbable.prefab` : `NetworkObject`,
`Rigidbody`, collider, une ancre de préhension, `GrabbableItem`.

Contrairement au ragdoll, il **est** spawné par NGO : il va donc dans
`HoldMyBeerNetworkPrefabs.asset`, sous peine de déconnexion sèche à l'approbation
(`CLAUDE.md` §4.5).

## Limites assumées

- Une seule main tient à la fois ; pas d'objet à deux mains.
- Pas d'invite d'interaction à l'écran : `CanApply` rend déjà le libellé, l'UI viendra.
- Pas de préhension de joueur à joueur.
- Le lancer n'est pas chargeable : une pression, une vélocité.
- Pas de tests, cohérent avec la décision prise pour le wobble. `PlayerHands` et
  `InteractionRegistry` restent sans dépendance Unity forte pour que ce soit possible
  plus tard.
