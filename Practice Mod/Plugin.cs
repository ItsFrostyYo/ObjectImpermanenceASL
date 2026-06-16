using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ObjImpPracticeMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Object Impermanence.exe")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance { get; private set; } = null!;
    internal static ConfigEntry<string> NoClipBindingEntry { get; private set; } = null!;
    internal static ConfigEntry<string> ResetCheckpointBindingEntry { get; private set; } = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        Instance = this;
        NoClipBindingEntry = Config.Bind("Keybinds", "ToggleNoClip", "Keyboard/F2", "Hotkey for toggling noclip. Format examples: Keyboard/F2, Keyboard/Q, Mouse/Left");
        ResetCheckpointBindingEntry = Config.Bind("Keybinds", "ResetCheckpoint", "Keyboard/F3", "Hotkey for resetting to the current checkpoint. Format examples: Keyboard/F3, Mouse/Right");
        PracticeRuntime.ClearCheckpointResetSignalOnLoad();
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }

    internal static void SaveHotkeyBinding(ConfigEntry<string> entry, string serializedBinding)
    {
        entry.Value = serializedBinding;
        Instance.Config.Save();
    }
}
