# Object Impermanence Autosplitter

This repo contains the Uhara-based LiveSplit autosplitter for the `Object Impermanence` process.

- Release script: `asl/ObjectImpermanence.asl`
- Test script: `asl/ObjectImpermanenceTest.asl`

## What It Does

- **Start**: starts when you load into `Landing`
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
- Map travel to `Landing` can **reset** if `Reset on Loading "Landing" from Map` is enabled

This keeps normal route transitions and manual map routing separated.

## Settings

### Reset

- `Reset on Loading "Landing" from Map`

When enabled, returning to `Landing` through the map triggers a reset. Walking into `Landing` at the end of the run is still treated as a normal scene transition.

### Transition Splits

- `Landing -> Entrance` (`Split`)
- `Checkpoint Alley` (`Split`)
- `Checkpoint Fan` (`Split`)
- `Fan -> Cloudbed` (`Split`)
- `Checkpoint Houses` (`Split`)
- `Checkpoint Statue` (`Split`)
- `Statue -> Rounded Room` (`Split`)
- `Checkpoint Chasm` (`Split`)
- `Chasm -> Landing` (`End Split`)

Only enabled settings split.

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
