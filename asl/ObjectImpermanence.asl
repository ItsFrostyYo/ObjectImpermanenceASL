// Object Impermanence Demo autosplitter
// Engine: Unity IL2CPP via Uhara Unity scene utilities

state("Object Impermanence"){}

startup
{
    vars.TryGet = (Func<dynamic, string, object>)((bag, key) =>
    {
        try
        {
            var dict = bag as System.Collections.Generic.IDictionary<string, object>;
            if (dict != null && dict.ContainsKey(key))
                return dict[key];
        }
        catch { }
        return null;
    });

    vars.ToULong = (Func<object, ulong>)(value =>
    {
        try { return Convert.ToUInt64(value); }
        catch { return 0UL; }
    });

    vars.NormalizeText = (Func<string, string>)(value =>
    {
        return string.IsNullOrEmpty(value) ? "" : value.Trim().ToLowerInvariant();
    });

    vars.MatchesCheckpoint = (Func<string, string[], bool>)((value, aliases) =>
    {
        string normalizedValue = vars.NormalizeText(value);
        if (string.IsNullOrEmpty(normalizedValue))
            return false;

        foreach (string alias in aliases)
        {
            if (normalizedValue == vars.NormalizeText(alias))
                return true;
        }

        return false;
    });

    Assembly.Load(File.ReadAllBytes("Components/uhara10")).CreateInstance("Main");

    vars.StartScene = "0_Landing";
    vars.SplitSceneA = "1A_Intro";
    vars.SplitSceneB = "1B_Exterior";
    vars.SplitSceneC = "2A_Spatial";

    vars.LoadRemovalActive = false;
    vars.PendingAutoStart = false;
    vars.PendingStartLocation = "";
    vars.AutoStartAfterReset = false;
    vars.AutoStartLocation = "";
    vars.TransitionFromScene = "";
    vars.TransitionStartedFromMap = false;
    vars.LastMapTriggerTime = DateTime.MinValue;
    vars.CheckpointHashes = new System.Collections.Generic.Dictionary<string, string>
    {
        { "alley", "fe07462b5f0c7db449d28570cc0f0590" },
        { "fan", "2cda0bb6ac14a5a478f4dcfece851520" },
        { "houses", "fb2931df569575d4eb755362a9c755ea" },
        { "statue", "dd751c1d7c4072c428ea78350916c879" },
        { "chasm", "b2121aaa456b083438884aa359e962de" },
    };
    vars.SeenRunCheckpoints = new System.Collections.Generic.HashSet<string>();
    vars.GetCheckpointHash = (Func<string, string>)(key =>
    {
        var checkpointHashes = vars.CheckpointHashes as System.Collections.Generic.Dictionary<string, string>;
        if (checkpointHashes == null || string.IsNullOrEmpty(key) || !checkpointHashes.ContainsKey(key))
            return "";
        return checkpointHashes[key];
    });
    vars.GetRunLocation = (Func<string, string, string>)((scene, checkpointSaveKey) =>
    {
        if (scene == vars.StartScene)
            return "landing";

        if (scene == vars.SplitSceneA)
        {
            if (checkpointSaveKey == vars.GetCheckpointHash("fan"))
                return "fan";
            if (checkpointSaveKey == vars.GetCheckpointHash("alley"))
                return "alley";
            return "entrance";
        }

        if (scene == vars.SplitSceneB)
        {
            if (checkpointSaveKey == vars.GetCheckpointHash("statue"))
                return "statue";
            if (checkpointSaveKey == vars.GetCheckpointHash("houses"))
                return "houses";
            return "cloudbed";
        }

        if (scene == vars.SplitSceneC)
        {
            if (checkpointSaveKey == vars.GetCheckpointHash("chasm"))
                return "chasm";
            return "rounded_room";
        }

        return "";
    });
    vars.GetILSettingKey = (Func<string, string>)(runLocation =>
    {
        if (runLocation == "entrance")
            return "il_entrance";
        if (runLocation == "alley")
            return "il_alley";
        if (runLocation == "fan")
            return "il_fan";
        if (runLocation == "cloudbed")
            return "il_cloudbed";
        if (runLocation == "houses")
            return "il_houses";
        if (runLocation == "statue")
            return "il_statue";
        if (runLocation == "rounded_room")
            return "il_rounded_room";
        if (runLocation == "chasm")
            return "il_chasm";
        return "";
    });

    dynamic[,] _settings =
    {
        { "GroupResets", true, "Reset Types", null },
        { "reset_on_new_start", true, "Loading `Landing` from Map (Reset)", "GroupResets" },
        { "reset_on_landing_reload", true, "Death Transitions in `Landing` (Reset)", "GroupResets" },

        { "GroupTransitions", true, "Scene Transition Splits", null },
        { "LandingSplit", true, "Landing `Landing -> Entrance` (Split)", "GroupTransitions" },
        { "IntroSplit", true, "Intro `Fan -> Cloudbed` (Split)", "GroupTransitions" },
        { "ExteriorSplit", true, "Exterior `Statue -> Rounded Room` (Split)", "GroupTransitions" },
        { "SpatialSplit", true, "Spacial `Chasm -> Landing` (End Split)", "GroupTransitions" },

        { "GroupCheckpoints", true, "Checkpoint Splits", null },
        { "AlleyCheckpointSplit", false, "Checkpoint `Alley` (Split)", "GroupCheckpoints" },
        { "FanCheckpointSplit", false, "Checkpoint `Fan` (Split)", "GroupCheckpoints" },
        { "HousesCheckpointSplit", false, "Checkpoint `Houses` (Split)", "GroupCheckpoints" },
        { "StatueCheckpointSplit", false, "Checkpoint `Statue` (Split)", "GroupCheckpoints" },
        { "ChasmCheckpointSplit", false, "Checkpoint `Chasm` (Split)", "GroupCheckpoints" },

        { "GroupIL", true, "Individual Level Settings", null },
        { "il_entrance", false, "`Entrance` (Start + Reset)", "GroupIL" },
        { "il_alley", false, "`Alley` (Start + Reset)", "GroupIL" },
        { "il_fan", false, "`Fan` (Start + Reset)", "GroupIL" },
        { "il_cloudbed", false, "`Cloudbed` (Start + Reset)", "GroupIL" },
        { "il_houses", false, "`Houses` (Start + Reset)", "GroupIL" },
        { "il_statue", false, "`Statue` (Start + Reset)", "GroupIL" },
        { "il_rounded_room", false, "`Rounded Room` (Start + Reset)", "GroupIL" },
        { "il_chasm", false, "`Chasm` (Start + Reset)", "GroupIL" },

    };
    vars.Uhara.Settings.Create(_settings);
}

init
{
    vars.Utils = vars.Uhara.CreateTool("Unity", "Utils");
    vars.Game = vars.Uhara.CreateTool("Unity", "IL2CPP", "Instance");
    vars.Game.SetDefaultNames("Assembly-CSharp");

    vars.Game.Watch<bool>("IsLoadingCheckpoint", "Assembly-CSharp::CheckPointSystem", "<IsLoadingCheckpoint>k__BackingField");
    vars.Game.Watch<int>("CoreMenuState", "Assembly-CSharp::CoreMenu", "state");
    vars.Game.Watch<int>("CoreMenuSection", "Assembly-CSharp::CoreMenu", "section");
    vars.Game.Watch<ulong>("ActiveCheckpointPtr", "Assembly-CSharp::CheckPointSystem", "<ActiveCheckpoint>k__BackingField");

    var activeCheckpointSaveKey = vars.Game.Get("Assembly-CSharp::CheckPointSystem", "<ActiveCheckpoint>k__BackingField", "saveKey");
    if (activeCheckpointSaveKey != null)
        vars.Resolver.WatchUnityString("ActiveCheckpointSaveKey", activeCheckpointSaveKey.Base, activeCheckpointSaveKey.Offsets);

    vars.LoadRemovalActive = false;
    vars.PendingAutoStart = false;
    vars.PendingStartLocation = "";
    vars.AutoStartAfterReset = false;
    vars.AutoStartLocation = "";
    vars.TransitionFromScene = "";
    vars.TransitionStartedFromMap = false;
    vars.LastMapTriggerTime = DateTime.MinValue;
    vars.SeenRunCheckpoints.Clear();
}

update
{
    vars.Uhara.Update();

    string oldScene = vars.TryGet(old, "scene") as string ?? "";
    bool oldHasTransitionLoad = (vars.TryGet(old, "hasTransitionLoad") as bool?) ?? false;
    current.scene = vars.Utils.GetCurrentSceneName() ?? "";
    if (string.IsNullOrEmpty(current.scene))
        current.scene = vars.Utils.GetCurrentSceneName2() ?? "";

    current.loadingScene = vars.Utils.GetLoadingSceneName() ?? "";
    current.isLoadingCheckpoint = (vars.TryGet(current, "IsLoadingCheckpoint") as bool?) ?? false;
    current.coreMenuState = (vars.TryGet(current, "CoreMenuState") as int?) ?? 0;
    current.coreMenuSection = (vars.TryGet(current, "CoreMenuSection") as int?) ?? 0;
    current.mapMenuOpen =
        current.coreMenuSection == 2 &&
        current.coreMenuState != 0;
    current.activeCheckpointPtr = vars.ToULong(vars.TryGet(current, "ActiveCheckpointPtr"));
    current.activeCheckpointSaveKey = vars.TryGet(current, "ActiveCheckpointSaveKey") as string ?? "";
    current.runLocation = vars.GetRunLocation(current.scene, current.activeCheckpointSaveKey);

    current.hasSceneLoad =
        !string.IsNullOrEmpty(current.loadingScene) &&
        current.loadingScene != current.scene;
    current.hasTransitionLoad = current.hasSceneLoad || current.isLoadingCheckpoint;
    current.transitionFinished =
        oldHasTransitionLoad &&
        !current.hasTransitionLoad;

    vars.LoadRemovalActive = current.hasTransitionLoad;

    if (current.mapMenuOpen)
        vars.LastMapTriggerTime = DateTime.Now;

    if (!oldHasTransitionLoad && current.hasTransitionLoad)
    {
        bool oldMapMenuOpen = (vars.TryGet(old, "mapMenuOpen") as bool?) ?? false;
        vars.TransitionFromScene = !string.IsNullOrEmpty(oldScene) ? oldScene : current.scene;
        vars.TransitionStartedFromMap =
            current.mapMenuOpen ||
            oldMapMenuOpen ||
            (DateTime.Now - vars.LastMapTriggerTime).TotalSeconds <= 3.0;
    }
}

start
{
    string currentScene = vars.TryGet(current, "scene") as string ?? "";
    string currentRunLocation = vars.TryGet(current, "runLocation") as string ?? "";
    bool transitionFinished = (vars.TryGet(current, "transitionFinished") as bool?) ?? false;
    bool hasTransitionLoad = (vars.TryGet(current, "hasTransitionLoad") as bool?) ?? false;
    string ilSettingKey = vars.GetILSettingKey(currentRunLocation);

    bool normalStart =
        currentScene == vars.StartScene &&
        (transitionFinished || (vars.PendingAutoStart && vars.PendingStartLocation == "landing" && !hasTransitionLoad));

    bool ilStart =
        !string.IsNullOrEmpty(ilSettingKey) &&
        settings[ilSettingKey] &&
        (
            (vars.TransitionStartedFromMap && transitionFinished) ||
            (vars.PendingAutoStart && vars.PendingStartLocation == currentRunLocation && !hasTransitionLoad)
        );

    if (timer.CurrentPhase == TimerPhase.NotRunning
        && (normalStart || ilStart))
    {
        vars.PendingAutoStart = false;
        vars.PendingStartLocation = "";
        return true;
    }
}

split
{
    if (timer.CurrentPhase != TimerPhase.Running)
        return false;

    string currentScene = vars.TryGet(current, "scene") as string ?? "";
    bool transitionFinished = (vars.TryGet(current, "transitionFinished") as bool?) ?? false;
    bool hasTransitionLoad = (vars.TryGet(current, "hasTransitionLoad") as bool?) ?? false;

    ulong currentCheckpointPtr = vars.ToULong(vars.TryGet(current, "activeCheckpointPtr"));
    ulong oldCheckpointPtr = vars.ToULong(vars.TryGet(old, "activeCheckpointPtr"));

    if (!vars.TransitionStartedFromMap
        && currentCheckpointPtr != 0
        && currentCheckpointPtr != oldCheckpointPtr)
    {
        string checkpointSaveKey = vars.TryGet(current, "activeCheckpointSaveKey") as string ?? "";
        string checkpointKey = "";
        var checkpointHashes = vars.CheckpointHashes as System.Collections.Generic.Dictionary<string, string>;

        bool isAlley =
            checkpointHashes != null &&
            checkpointHashes.ContainsKey("alley") &&
            checkpointSaveKey == checkpointHashes["alley"];
        bool isFan =
            checkpointHashes != null &&
            checkpointHashes.ContainsKey("fan") &&
            checkpointSaveKey == checkpointHashes["fan"];
        bool isHouses =
            checkpointHashes != null &&
            checkpointHashes.ContainsKey("houses") &&
            checkpointSaveKey == checkpointHashes["houses"];
        bool isStatue =
            checkpointHashes != null &&
            checkpointHashes.ContainsKey("statue") &&
            checkpointSaveKey == checkpointHashes["statue"];
        bool isChasm =
            checkpointHashes != null &&
            checkpointHashes.ContainsKey("chasm") &&
            checkpointSaveKey == checkpointHashes["chasm"];

        if (settings["AlleyCheckpointSplit"] && isAlley)
            checkpointKey = "alley";
        else if (settings["FanCheckpointSplit"] && isFan)
            checkpointKey = "fan";
        else if (settings["HousesCheckpointSplit"] && isHouses)
            checkpointKey = "houses";
        else if (settings["StatueCheckpointSplit"] && isStatue)
            checkpointKey = "statue";
        else if (settings["ChasmCheckpointSplit"] && isChasm)
            checkpointKey = "chasm";

        if (!string.IsNullOrEmpty(checkpointKey) && !vars.SeenRunCheckpoints.Contains(checkpointKey))
        {
            vars.SeenRunCheckpoints.Add(checkpointKey);
            return true;
        }
    }

    if (!transitionFinished)
        return false;

    bool sceneChanged =
        !string.IsNullOrEmpty(vars.TransitionFromScene) &&
        !string.IsNullOrEmpty(currentScene) &&
        currentScene != vars.TransitionFromScene;

    if (!sceneChanged)
        return false;

    if (vars.TransitionStartedFromMap)
    {
        vars.TransitionFromScene = currentScene;
        return false;
    }

    bool shouldSplit =
        (currentScene == vars.SplitSceneA && settings["LandingSplit"]) ||
        (currentScene == vars.SplitSceneB && settings["IntroSplit"]) ||
        (currentScene == vars.SplitSceneC && settings["ExteriorSplit"]) ||
        (currentScene == vars.StartScene && settings["SpatialSplit"]);

    vars.TransitionFromScene = currentScene;
    return shouldSplit;
}

reset
{
    string currentScene = vars.TryGet(current, "scene") as string ?? "";
    string currentRunLocation = vars.TryGet(current, "runLocation") as string ?? "";
    bool transitionFinished = (vars.TryGet(current, "transitionFinished") as bool?) ?? false;

    if (timer.CurrentPhase != TimerPhase.Running)
        return false;

    if (transitionFinished && vars.TransitionStartedFromMap)
    {
        if (currentRunLocation == "landing" && settings["reset_on_new_start"])
        {
            vars.AutoStartAfterReset = true;
            vars.AutoStartLocation = currentRunLocation;
            vars.TransitionFromScene = currentScene;
            vars.TransitionStartedFromMap = false;
            return true;
        }

        string ilSettingKey = vars.GetILSettingKey(currentRunLocation);
        if (!string.IsNullOrEmpty(ilSettingKey) && settings[ilSettingKey])
        {
            vars.AutoStartAfterReset = true;
            vars.AutoStartLocation = currentRunLocation;
            vars.TransitionFromScene = currentScene;
            vars.TransitionStartedFromMap = false;
            return true;
        }
    }

    if (currentScene == vars.StartScene
        && transitionFinished
        && vars.TransitionFromScene == vars.StartScene
        && !vars.TransitionStartedFromMap
        && settings["reset_on_landing_reload"])
    {
        vars.AutoStartAfterReset = true;
        vars.AutoStartLocation = "landing";
        vars.TransitionFromScene = currentScene;
        return true;
    }
}

onReset
{
    vars.LoadRemovalActive = false;
    vars.TransitionFromScene = "";
    vars.TransitionStartedFromMap = false;
    vars.LastMapTriggerTime = DateTime.MinValue;
    vars.SeenRunCheckpoints.Clear();
    if (vars.AutoStartAfterReset)
    {
        vars.PendingAutoStart = true;
        vars.PendingStartLocation = vars.AutoStartLocation as string ?? "";
        vars.AutoStartAfterReset = false;
        vars.AutoStartLocation = "";
    }
    else
    {
        vars.PendingStartLocation = "";
        vars.AutoStartLocation = "";
    }
}

isLoading
{
    return vars.LoadRemovalActive;
}
