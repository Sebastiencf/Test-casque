# Nang Tani - étape 1 : scène greybox jouable

## Lancer
1. Laisser Unity compiler (petit chargement en bas à droite).
2. Menu **Nang Tani > 1 - Construire la scene greybox** -> crée `Assets/Scenes/NangTani_Main.unity`.
3. Appuyer sur **Play**.

## Touches dans l'éditeur (sans casque)
| Touche | Action |
|---|---|
| Clic droit maintenu + souris | regarder autour |
| Espace / clic gauche | gâchette (offrande) |
| 1 à 6 | aller à l'étape : village, bascule, clairière, climax, résolution, épilogue |
| R | recommencer |

Sur l'objet `Experience` > ExperienceDirector : `Editor Speed` accélère le temps dans l'éditeur, `Start Stage` choisit l'étape de départ.

## Parcours (CDD + Story)
Village (calme) -> regard sur l'arbre sacré -> légende de Ming -> Ram arrache la feuille ->
vent, voix hors champ, tourner la tête -> bougies éteintes, silence, silhouette verte -> noir ->
clairière du temple, Nang Tani approche de Ram -> climax (ombres étirées), 1 tap de gâchette ->
excuses, Nang Tani se dissout -> retour au village, Ram s'incline.

## Où modifier quoi
- Ambiance (brouillard, lune, grain...) : `AtmosphereState.cs`
- Répliques / sous-titres : `Dialogue.cs`
- Durées et enchaînement : `ExperienceDirector.cs`
- Couleurs : `Palette.cs`
- Seuils regard / rotation de tête : composants `GazeDwellTarget` et `HeadTurnTrigger` dans la scène

Les sons (vent, grillons, murmure) sont générés par code en attendant les vrais enregistrements :
il suffit d'assigner un AudioClip aux AudioSources de l'objet `Audio` pour les remplacer.

Relancer le menu **recrée la scène à zéro** : dupliquer la scène avant de la retoucher à la main.
