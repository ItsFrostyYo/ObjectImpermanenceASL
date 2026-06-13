# Object Impermanence Autosplitter (Uhara-Memory Based)

This contains the **Uhara-memory based** `.asl` version of the Object Impermanence Demo autosplitter.

- `asl/ObjectImpermanence.asl`

It currently supports this game process:

- `Object Impermanence`

## How It Works

The autosplitter is built around scene transitions and checkpoint/map reload behavior.

- **Start** starts automatically when you load into `Landing`.
- **Splits** happen on normal walking scene transitions only.
- **Map transitions** do not split.
- **Load removal** is active during scene transitions and checkpoint loading.

## Settings

### Reset

- `Reset on Loading "Landing" from Map` Resets when you use the map to go back to "Landing".

This only applies to map-based returns to `Landing`. Normal walking transitions are not treated as resets.

### Transition Splits

These settings only apply to **normal non-map transitions**.

- `Split on "Landing -> Intro" (Split)`
- `Split on "Intro -> Exterior" (Split)`
- `Split on "Exterior -> Spatial" (Split)`
- `Split on "Spatial -> Landing" (End)`

Only the transitions you enable will split.

Example:

If only `Split on "Exterior -> Spatial" (Split)` is enabled, then only that transition will split when you Transition from Exterior to Spatial, The other normal transitions will not split.

### Map Behavior

Map-based transitions are handled separately from normal walking transitions:

- Map transitions to any non-`Landing` scene will **not split**
- Map transitions to `Landing` will **reset** only if `Reset on Loading "Landing" from Map` is enabled
- Map transitions still use **load removal**

## In-Game Time

This autosplitter uses LiveSplit `Game Time` for load removal during transitions and checkpoint loading.

## Uhara

This autosplitter depends on `uhara10`.

The Livesplit Version should automatically place `uhara10` next to the .asl, but if not This repo includes it here:

- `Components/uhara10`

Place `uhara10` in your LiveSplit `Components` folder if needed:

- `Components/uhara10`

If the autosplitter stops working, either a game update broke the pointers or `uhara10` is missing.

## Credits

[Jura](https://www.speedrun.com/users/Jura3) for Helping with Testing and Knowledge on the Speedrun.

ru-mii for uhara10,
[Unreal Engine Logger for finding pointers](https://github.com/ru-mii/Unreal-Logger)
[Uhara Library for autosplitters](https://github.com/ru-mii/uhara)

## License

MIT License. See [LICENSE](./LICENSE).