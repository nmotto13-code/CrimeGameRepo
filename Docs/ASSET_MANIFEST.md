# Asset Manifest

## Import Policy
- Vet first, import second.
- Prefer sources with clean commercial terms and no attribution requirement.
- Import third-party art into `Assets/Art/ThirdParty/`.
- Import third-party audio into `Assets/Audio/ThirdParty/`.
- If a source is visually off-tone or too generic, prefer AI generation or scripted placeholders over low-fit imports.

## Recommended Sources

### UI Chrome
- `Kenney UI Pack`
  Source: https://kenney.nl/assets/ui-pack
  Status: approved for review/import
  Why: broad free UI chrome library, consistent style, useful for dossier cards, tabs, dividers, and file-frame treatments
  License note: Kenney assets are distributed under CC0 on Kenney and do not require attribution

### Generic Iconography / Placeholder Motifs
- `Kenney Game Icons`
  Source: https://kenney.nl/assets/game-icons
  Status: approved for review/import
  Why: useful for dossier toggles, suspect status markers, evidence category badges, and non-character iconography
  License note: Kenney assets are distributed under CC0 on Kenney and do not require attribution

### UI / Foley Sound Seeds
- `Kenney Interface Sounds`
  Source: https://kenney.nl/assets/interface-sounds
  Status: approved for review/import
  Why: clean button, confirm, reject, and subtle feedback cues for dossier/interrogation UI
  License note: Kenney assets are distributed under CC0 on Kenney and do not require attribution

### Background / Surface Texture Seeds
- `Kenney Prototype Textures`
  Source: https://kenney.nl/assets/prototype-textures
  Status: approved for review/import
  Why: good low-risk stopgaps for case file paper, board surfaces, and muted background panels
  License note: Kenney assets are distributed under CC0 on Kenney and do not require attribution

## Defer or Replace With AI / Scripted Placeholders

### Suspect Portrait Placeholders
- Recommendation: do not import a third-party portrait pack yet.
- Reason: no vetted free package from the current target sources cleanly matches the grounded detective tone without visual mismatch.
- Preferred fallback:
  1. Generate neutral silhouette or grayscale portrait placeholders through the existing AI image workflow.
  2. If speed matters more than style, render monogram portrait cards in Unity and replace them later with AI or bespoke art.

### Ambient SFX
- Recommendation: defer third-party ambient package import pending a tighter shortlist.
- Reason: the current Kenney shortlist is strongest for UI/foley, not for office hum, rain beds, or grounded footsteps loops.
- Preferred fallback:
  1. Use AI-generated temp ambience loops or internal mock loops for office hum and rain.
  2. Use minimal one-shot placeholder footsteps only where interaction timing matters.

## Unity Asset Store License Note
- Before importing any Unity Asset Store package, confirm it is covered by the Standard Unity Asset Store EULA and document the package page in the next revision of this manifest.
- Attribution is generally not required under the standard license, but the package page still needs a manual check before import.
- Reference: https://support.unity.com/hc/en-us/articles/205623589-Can-I-use-assets-from-the-Asset-Store-in-my-commercial-game

## Current Recommendation
- Import Kenney UI Pack, Game Icons, Interface Sounds, and Prototype Textures only after visual review in a staging branch.
- Keep suspect portraits and grounded ambience on the AI/scripted path for now.
