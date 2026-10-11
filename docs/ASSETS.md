# Asset intake

The team is currently selecting music and game assets. This document records sources and import decisions; it does not authorize buying, downloading, redistributing, or implementing any specific asset.

## Placement

- Characters: Assets/_Breathe/Art/Characters.
- Room/environment: Assets/_Breathe/Art/Environment.
- First-party textures/materials: corresponding Art subfolders.
- Music, ambience, sound effects, voice lines: corresponding Audio subfolders.
- Imported vendor packages: Assets/ThirdParty when compatible with the vendor's layout, otherwise keep their required location.
- Original ZIP files, source downloads, reference bundles: local-only AssetInbox.

## Review before importing

- Record creator/source URL, license, required credit, and proof of any purchase.
- Confirm permission for game use and the intended source-sharing/distribution arrangement.
- Check Unity 6 and URP compatibility. Do not import an entire demo project or let an asset package silently change the render pipeline or project settings.
- Prefer Unity-native assets or FBX for the initial 3D workflow. A .blend workflow needs Blender; glTF/GLB may need an importer package. Document any additional dependency before using it.
- Keep original license/readme files with third-party assets.
- Move imported assets through Unity to preserve .meta references.
- Check music looping points and source quality later; no playback scripting is part of setup.

## Asset register

| Asset | Creator / source URL | License / required credit | Local import path | URP / Unity version checked | Status |
|---|---|---|---|---|---|
| Pending selection | | | | | Not imported |

## Existing repository media

The existing dumb/ folder is preserved outside Assets. Its MP3 remains ordinary Git content using an explicit .gitattributes exception; new imported binary media uses LFS. The existing track is not imported into Unity, and its license/source has not been verified. Record those details before importing or distributing it.
