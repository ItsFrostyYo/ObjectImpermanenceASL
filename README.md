# Object Impermanence Autosplitter

This repo contains the Uhara-based LiveSplit autosplitter for the `Object Impermanence` process.

- Release script: `asl/ObjectImpermanence.asl`
- Test script: `asl/ObjectImpermanenceTest.asl`

## What It Does

- **Start**: starts when you load into `Landing`
- **Optional IL start**: can start from any of the 9 run locations when loaded from the map
- **Load removal**: pauses during scene transitions and checkpoint loading
- **Scene splits**: splits on normal walking transitions only
- **Checkpoint splits**: optional splits for the map-visible checkpoint locations that are not already covered by scene transitions
- **Map transitions**: do not split, except for the optional reset behavior

## Supported Split Types

### Scene transitions

These only split on normal non-map transitions, and only if their setting is enabled:

- `Landing -> Entrance`
- `Fan -> Cloudbed`
- `Statue -> Rounded Room`
- `Chasm -> Landing` (`End Split`)

### Checkpoint splits

These are the extra checkpoint splits that are available in settings:

- `Alley`
- `Fan`
- `Houses`
- `Statue`
- `Chasm`

These are mapped to the in-game map locations, not to menu/map travel and not to the later end-of-area fallback checkpoints.

## Map Behavior

Map travel is handled separately from normal walking transitions:

- Map travel to non-`Landing` locations does **not** split
- Map travel still uses **load removal**
- Map travel can **reset** on specific destinations if that destination is enabled in `Individual Level Settings`

This keeps normal route transitions and manual map routing separated.

## Settings

The LiveSplit settings are grouped exactly like this:

### Reset Types

- `Loading "Landing" from Map (Reset)`
- `Death Transitions in "Landing" (Reset)`

`Loading "Landing" from Map (Reset)` is the original Landing map reset behavior.

`Death Transitions in "Landing" (Reset)` is for `Landing -> Landing` reload behavior, such as dying and being reloaded back into `Landing`.

This is separate from map-based IL resets.

### Scene Transition Splits

- `Landing "Landing -> Entrance" (Split)`
- `Intro "Fan -> Cloudbed" (Split)`
- `Exterior "Statue -> Rounded Room" (Split)`
- `Spacial "Chasm -> Landing" (End Split)`

These only split on normal non-map transitions.

### Checkpoint Splits

- `Checkpoint Alley` (`Split`)
- `Checkpoint Fan` (`Split`)
- `Checkpoint Houses` (`Split`)
- `Checkpoint Statue` (`Split`)
- `Checkpoint Chasm` (`Split`)

These split when that checkpoint is unlocked during the run.

### Individual Level Settings

- `Entrance` `(Start + Reset)`
- `Alley` `(Start + Reset)`
- `Fan` `(Start + Reset)`
- `Cloudbed` `(Start + Reset)`
- `Houses` `(Start + Reset)`
- `Statue` `(Start + Reset)`
- `Rounded Room` `(Start + Reset)`
- `Chasm` `(Start + Reset)`

Each of these enables the full IL flow for that specific destination:

- start when that location is loaded from the map
- reset when that location is loaded from the map during a run
- immediately flow into a fresh start for that IL after the reset

`Landing` stays on the original reset/start behavior in `Reset Types` and the normal main start logic.

Only enabled settings are active.

## In-Game Time

This autosplitter uses LiveSplit `Game Time` for load removal during transitions and checkpoint loading.

## Uhara

This autosplitter depends on `uhara10`.

The required component is included in this repo:

- `Components/uhara10`

If LiveSplit does not already place or load it correctly, put `uhara10` in your LiveSplit `Components` folder.

If the autosplitter stops working, the usual causes are:

- `uhara10` is missing
- the game updated and the watched fields changed

## Credits

[Jura](https://www.speedrun.com/users/Jura3) for testing and route knowledge.

ru-mii for:
- [Uhara Library for autosplitters](https://github.com/ru-mii/uhara)
- [Unreal Engine Logger for pointer work](https://github.com/ru-mii/Unreal-Logger)

## License

MIT License. See [LICENSE](./LICENSE).
