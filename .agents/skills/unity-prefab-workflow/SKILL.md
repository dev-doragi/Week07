---
name: unity-prefab-workflow
description: Manage Unity prefab assets with unity-cli. Use when the user asks to create a prefab from a scene object, open a prefab in edit mode, save prefab changes, instantiate a prefab into a scene, or update prefab asset properties. Do not use for general scene object editing outside prefab workflows.
allowed-tools: Bash, Read, Grep, Glob
metadata:
  author: akiojin
  version: 0.2.0
  category: prefabs
---

# Prefab Lifecycle

Create, open, edit, and instantiate prefabs.
Read `references/prefab-edit-mode.md` when you need a safe sequence for edit mode, scene handoff, or instantiation checks.

## Use When

- The user wants to create or update a prefab asset.
- The task requires entering prefab edit mode and saving changes back to the asset.
- The user wants to instantiate prefab assets into a scene with specific transforms.

## Do Not Use When

- The request is about editing ordinary scene objects with no prefab asset involved.
- The task is only about creating a scene from scratch.

## Commands

```bash
# Create prefab from scene object
unity-cli raw create_prefab --json '{"gameObjectPath":"/Player","prefabPath":"Assets/Prefabs/Player.prefab"}'

# Prefab editing mode
unity-cli raw open_prefab --json '{"prefabPath":"Assets/Prefabs/Player.prefab"}'
unity-cli raw save_prefab --json '{}'
unity-cli raw exit_prefab_mode --json '{}'

# Instantiate
unity-cli raw instantiate_prefab --json '{"prefabPath":"Assets/Prefabs/Player.prefab","position":{"x":0,"y":0,"z":0}}'

# Modify prefab properties
unity-cli raw modify_prefab --json '{"prefabPath":"Assets/Prefabs/Player.prefab","modifications":{"name":"UpdatedPlayer"}}'
```

## Workflow

1. `open_prefab` to enter edit mode
2. Make changes (add components, modify properties)
3. `save_prefab` to persist
4. `exit_prefab_mode` to return to scene

## Examples

- "Create a prefab from `/Player` and save it under `Assets/Prefabs/Player.prefab`."
- "Open an existing prefab, change it, save it, and exit prefab mode."
- "Instantiate a prefab at the origin for a test scene."

## Common Issues

- Changes do not persist: call `save_prefab` before exiting edit mode.
- The editor remains stuck in prefab mode: explicitly call `exit_prefab_mode` before switching scenes.
- The user actually wants to edit a scene instance, not the prefab asset: switch to `unity-gameobject-edit`.
- Use explicit `position` or `rotation` values during instantiation when placement matters.
