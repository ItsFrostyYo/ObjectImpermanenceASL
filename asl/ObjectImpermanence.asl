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

    Assembly.Load(File.ReadAllBytes("Components/uhara10")).CreateInstance("Main");

    vars.StartScene = "0_Landing";
    vars.SplitSceneA = "1A_Intro";
    vars.SplitSceneB = "1B_Exterior";
    vars.SplitSceneC = "2A_Spatial";
    vars.LoadRemovalActive = false;
    vars.PendingAutoStart = false;
    vars.AutoStartAfterReset = false;
    vars.TransitionFromScene = "";
    vars.TransitionStartedFromMap = false;
    vars.LastMapTriggerTime = DateTime.MinValue;

    dynamic[,] _settings =
    {
        { "reset_on_new_start", true, "Reset on Loading `Landing` from Map", null },
        { "group_transition_splits", true, "Transition Splits", null },
        { "split_landing", false, "`Landing -> Intro` (Split)", "group_transition_splits" },
        { "split_intro", false, "`Intro -> Exterior` (Split)", "group_transition_splits" },
        { "split_exterior", false, "`Exterior -> Spatial` (Split)", "group_transition_splits" },
        { "split_spatial", true, "`Spatial -> Landing` (End)", "group_transition_splits" }
    };
    vars.Uhara.Settings.Create(_settings);
}

init
{
    vars.Utils = vars.Uhara.CreateTool("Unity", "Utils");
    vars.Game = vars.Uhara.CreateTool("Unity", "IL2CPP", "Instance");
    vars.Game.SetDefaultNames("Assembly-CSharp");
    vars.Game.Watch<bool>("IsLoadingCheckpoint", "Assembly-CSharp::CheckPointSystem", "<IsLoadingCheckpoint>k__BackingField");
    vars.Game.Watch<bool>("IsMapInteractionInProgress", (short)16, "Assembly-CSharp::MapLocation", "interaction_in_progress");

    vars.LoadRemovalActive = false;
    vars.PendingAutoStart = false;
    vars.AutoStartAfterReset = false;
    vars.TransitionFromScene = "";
    vars.TransitionStartedFromMap = false;
    vars.LastMapTriggerTime = DateTime.MinValue;
}

update
{
    vars.Uhara.Update();

    string oldScene = vars.TryGet(old, "scene") as string ?? "";
    bool oldHasTransitionLoad = (vars.TryGet(old, "hasTransitionLoad") as bool?) ?? false;
    bool oldMapInteractionInProgress = (vars.TryGet(old, "mapInteractionInProgress") as bool?) ?? false;

    current.scene = vars.Utils.GetCurrentSceneName() ?? "";
    if (string.IsNullOrEmpty(current.scene))
        current.scene = vars.Utils.GetCurrentSceneName2() ?? "";

    current.loadingScene = vars.Utils.GetLoadingSceneName() ?? "";
    current.isLoadingCheckpoint = (vars.TryGet(current, "IsLoadingCheckpoint") as bool?) ?? false;
    current.mapInteractionInProgress = false;
    for (int i = 0; i < 16; i++)
    {
        if ((vars.TryGet(current, "IsMapInteractionInProgress" + i.ToString()) as bool?) ?? false)
        {
            current.mapInteractionInProgress = true;
            break;
        }
    }

    current.hasSceneLoad =
        !string.IsNullOrEmpty(current.loadingScene) &&
        current.loadingScene != current.scene;
    current.hasTransitionLoad = current.hasSceneLoad || current.isLoadingCheckpoint;
    current.transitionFinished =
        oldHasTransitionLoad &&
        !current.hasTransitionLoad;

    vars.LoadRemovalActive = current.hasTransitionLoad;

    if (current.mapInteractionInProgress)
        vars.LastMapTriggerTime = DateTime.Now;

    if (!oldHasTransitionLoad && current.hasTransitionLoad)
    {
        vars.TransitionFromScene = !string.IsNullOrEmpty(oldScene) ? oldScene : current.scene;
        vars.TransitionStartedFromMap =
            current.mapInteractionInProgress ||
            oldMapInteractionInProgress ||
            (DateTime.Now - vars.LastMapTriggerTime).TotalSeconds <= 3.0;
    }
}

start
{
    string currentScene = vars.TryGet(current, "scene") as string ?? "";
    bool transitionFinished = (vars.TryGet(current, "transitionFinished") as bool?) ?? false;
    bool hasTransitionLoad = (vars.TryGet(current, "hasTransitionLoad") as bool?) ?? false;

    if (timer.CurrentPhase == TimerPhase.NotRunning
        && currentScene == vars.StartScene
        && (transitionFinished || (vars.PendingAutoStart && !hasTransitionLoad)))
    {
        vars.PendingAutoStart = false;
        return true;
    }
}

split
{
    if (timer.CurrentPhase != TimerPhase.Running)
        return false;

    string currentScene = vars.TryGet(current, "scene") as string ?? "";
    bool transitionFinished = (vars.TryGet(current, "transitionFinished") as bool?) ?? false;

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
        (currentScene == vars.StartScene && settings["split_landing"]) ||
        (currentScene == vars.SplitSceneA && settings["split_intro"]) ||
        (currentScene == vars.SplitSceneB && settings["split_exterior"]) ||
        (currentScene == vars.SplitSceneC && settings["split_spatial"]);

    vars.TransitionFromScene = currentScene;
    return shouldSplit;
}

reset
{
    string currentScene = vars.TryGet(current, "scene") as string ?? "";
    bool transitionFinished = (vars.TryGet(current, "transitionFinished") as bool?) ?? false;

    if (!settings["reset_on_new_start"] || timer.CurrentPhase != TimerPhase.Running)
        return false;

    if (currentScene == vars.StartScene
        && transitionFinished
        && vars.TransitionStartedFromMap)
    {
        vars.AutoStartAfterReset = true;
        vars.TransitionFromScene = currentScene;
        vars.TransitionStartedFromMap = false;
        return true;
    }
}

onReset
{
    vars.LoadRemovalActive = false;
    vars.TransitionFromScene = "";
    vars.TransitionStartedFromMap = false;
    vars.LastMapTriggerTime = DateTime.MinValue;
    if (vars.AutoStartAfterReset)
    {
        vars.PendingAutoStart = true;
        vars.AutoStartAfterReset = false;
    }
}

isLoading
{
    return vars.LoadRemovalActive;
}
