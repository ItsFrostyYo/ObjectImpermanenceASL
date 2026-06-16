using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ObjImpPracticeMod;

internal static class PracticeRuntime
{
    private const float DefaultFlightSpeed = 66.7f;
    private const float MinFlightSpeed = 5f;
    private const float MaxFlightSpeed = 350f;
    private const float ScrollSpeedFactor = 1.1f;
    private const string AlleyCheckpointHash = "fe07462b5f0c7db449d28570cc0f0590";
    private const string FanCheckpointHash = "2cda0bb6ac14a5a478f4dcfece851520";
    private const string HousesCheckpointHash = "fb2931df569575d4eb755362a9c755ea";
    private const string StatueCheckpointHash = "dd751c1d7c4072c428ea78350916c879";
    private const string ChasmCheckpointHash = "b2121aaa456b083438884aa359e962de";
    private static readonly TimeSpan ResetCooldown = TimeSpan.FromSeconds(1.25);

    private static bool _overlayVisible = true;
    private static bool _noclipEnabled;
    private static bool _runtimeFaulted;
    private static bool _hotkeyEditorOpen;
    private static bool _awaitingRebindRelease;
    private static bool _checkpointResetInFlight;
    private static bool _checkpointLoadObserved;
    private static DateTime _signalFileDeleteAtUtc = DateTime.MinValue;
    private static float _flightSpeed = DefaultFlightSpeed;
    private static float _actualSpeed;
    private static Vector3 _lastMeasuredPosition = Vector3.zero;
    private static bool _hasMeasuredPosition;
    private static DateTime _lastResetRequestUtc = DateTime.MinValue;
    private static PlayerMovement? _cachedPlayerMovement;
    private static Rigidbody? _cachedRigidbody;
    private static Camera? _cachedCamera;
    private static readonly FieldInfo? CheckpointSaveKeyField = typeof(CheckPointTag).GetField("saveKey", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CoreMenuMapControllerField = typeof(CoreMenu).GetField("map_controller", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? MapLocationsField = typeof(MapController).GetField("map_locations", BindingFlags.Instance | BindingFlags.NonPublic);
    private static GUIStyle? _textStyle;
    private static GUIStyle? _editorTitleStyle;
    private static GUIStyle? _editorLabelStyle;
    private static GUIStyle? _editorButtonStyle;
    private static HotkeyBinding _noClipBinding = new(Key.F2);
    private static HotkeyBinding _resetCheckpointBinding = new(Key.F3);
    private static PendingRebind _pendingRebind;

    public static bool NoclipEnabled => _noclipEnabled;

    public static void ClearCheckpointResetSignalOnLoad()
    {
        _signalFileDeleteAtUtc = DateTime.MinValue;
        DeleteCheckpointResetSignalFile();
        Plugin.Instance.Log.LogInfo($"Checkpoint reset signal path: {GetCheckpointResetSignalPath()}");
    }

    public static void Tick()
    {
        if (_runtimeFaulted)
            return;

        try
        {
            RefreshReferences();
            UpdateCheckpointResetState();
            UpdateMeasuredSpeed();
            HandleHotkeys();
        }
        catch (Exception ex)
        {
            FailRuntime($"Tick failed: {ex}");
        }
    }

    private static void HandleHotkeys()
    {
        Keyboard? keyboard = Keyboard.current;
        Mouse? mouse = Mouse.current;

        if (keyboard == null)
            return;

        _noClipBinding = HotkeyBinding.Parse(Plugin.NoClipBindingEntry.Value, new HotkeyBinding(Key.F2));
        _resetCheckpointBinding = HotkeyBinding.Parse(Plugin.ResetCheckpointBindingEntry.Value, new HotkeyBinding(Key.F3));

        if (keyboard.f1Key.wasPressedThisFrame)
            _overlayVisible = !_overlayVisible;

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            SetHotkeyEditorOpen(!_hotkeyEditorOpen);
            return;
        }

        if (_hotkeyEditorOpen)
        {
            HandleHotkeyEditorRebind();
            return;
        }

        if (_noClipBinding.WasPressedThisFrame())
            ToggleNoClip();

        if (_resetCheckpointBinding.WasPressedThisFrame())
            ResetCheckpoint();

        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0f)
                _flightSpeed = Mathf.Min(_flightSpeed * ScrollSpeedFactor, MaxFlightSpeed);
            else if (scrollY < 0f)
                _flightSpeed = Mathf.Max(_flightSpeed / ScrollSpeedFactor, MinFlightSpeed);
        }
    }

    private static void HandleHotkeyEditorRebind()
    {
        if (_pendingRebind == PendingRebind.None)
            return;

        if (_awaitingRebindRelease)
        {
            if (!AnyMouseButtonsPressed())
                _awaitingRebindRelease = false;
            return;
        }

        if (!HotkeyBinding.TryCapture(out HotkeyBinding binding))
            return;

        try
        {
            if (_pendingRebind == PendingRebind.NoClip)
                Plugin.SaveHotkeyBinding(Plugin.NoClipBindingEntry, binding.Serialize());
            else if (_pendingRebind == PendingRebind.ResetCheckpoint)
                Plugin.SaveHotkeyBinding(Plugin.ResetCheckpointBindingEntry, binding.Serialize());
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Failed to save hotkey binding: {ex}");
        }

        _pendingRebind = PendingRebind.None;
        _awaitingRebindRelease = true;
    }

    private static void SetHotkeyEditorOpen(bool open)
    {
        _hotkeyEditorOpen = open;
        if (!open)
            _pendingRebind = PendingRebind.None;

        _awaitingRebindRelease = open;
        ApplyEditorState();
    }

    private static void ApplyEditorState()
    {
        if (_cachedPlayerMovement != null && !_cachedPlayerMovement.Equals(null))
            _cachedPlayerMovement.enabled = !_hotkeyEditorOpen;

        if (_hotkeyEditorOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ZeroRigidbodyVelocity();
        }
    }

    private static void ToggleNoClip()
    {
        if (_cachedPlayerMovement == null || _cachedPlayerMovement.Equals(null))
            return;

        _noclipEnabled = !_noclipEnabled;

        if (_noclipEnabled)
            EnableNoClip();
        else
            DisableNoClip();
    }

    private static void EnableNoClip()
    {
        if (_cachedRigidbody == null || _cachedRigidbody.Equals(null))
            return;

        ZeroRigidbodyVelocity();
        _cachedRigidbody.useGravity = false;
        _cachedRigidbody.isKinematic = false;
        SetDetectCollisions(_cachedRigidbody, false);
    }

    private static void DisableNoClip()
    {
        if (_cachedRigidbody != null && !_cachedRigidbody.Equals(null))
        {
            SetDetectCollisions(_cachedRigidbody, true);
            _cachedRigidbody.useGravity = true;
            ZeroRigidbodyVelocity();
        }

        _actualSpeed = 0f;
    }

    private static void ResetCheckpoint()
    {
        try
        {
            if (_checkpointResetInFlight)
            {
                Plugin.Instance.Log.LogInfo("ResetCheckpoint ignored because a checkpoint reset is already in flight.");
                return;
            }

            if (DateTime.UtcNow - _lastResetRequestUtc < ResetCooldown)
            {
                Plugin.Instance.Log.LogInfo("ResetCheckpoint ignored because it is inside the cooldown window.");
                return;
            }

            if (CheckPointSystem.IsLoadingCheckpoint)
            {
                Plugin.Instance.Log.LogInfo("ResetCheckpoint ignored because the game is already loading a checkpoint.");
                return;
            }

            DisableNoClip();

            CheckPointTag? activeMainCheckpoint = CheckPointSystem.ActiveMainCheckpoint;
            if (activeMainCheckpoint != null)
            {
                int targetCode = GetLocationCodeForCheckpoint(activeMainCheckpoint);
                _lastResetRequestUtc = DateTime.UtcNow;
                Plugin.Instance.Log.LogInfo($"ResetCheckpoint requested. checkpoint={DescribeCheckpoint(activeMainCheckpoint)}, targetCode={targetCode}");
                BeginCheckpointResetFlow(activeMainCheckpoint, targetCode);
                LoadCheckpointThroughMap(activeMainCheckpoint);
                return;
            }

            Plugin.Instance.Log.LogWarning("Reset checkpoint requested, but no active main checkpoint was found.");
        }
        catch (Exception ex)
        {
            EndCheckpointResetFlow();
            Plugin.Instance.Log.LogError($"Reset checkpoint failed: {ex}");
        }
    }

    private static void BeginCheckpointResetFlow(CheckPointTag checkpoint, int targetCode)
    {
        _checkpointResetInFlight = true;
        _checkpointLoadObserved = false;
        _signalFileDeleteAtUtc = DateTime.UtcNow + ResetCooldown;
        string targetLocation = GetRunLocationForCode(targetCode);
        WriteCheckpointResetSignalFile(targetLocation);
        Plugin.Instance.Log.LogInfo($"CheckpointResetSignal write requested. targetLocation={targetLocation}, deleteAt={_signalFileDeleteAtUtc:O}");
    }

    private static void EndCheckpointResetFlow()
    {
        if (!_checkpointResetInFlight)
            return;

        _checkpointResetInFlight = false;
        _checkpointLoadObserved = false;
    }

    private static void UpdateCheckpointResetState()
    {
        if (_signalFileDeleteAtUtc != DateTime.MinValue && DateTime.UtcNow >= _signalFileDeleteAtUtc)
        {
            DeleteCheckpointResetSignalFile();
            _signalFileDeleteAtUtc = DateTime.MinValue;
        }

        if (!_checkpointResetInFlight)
            return;

        if (CheckPointSystem.IsLoadingCheckpoint)
        {
            if (!_checkpointLoadObserved)
                Plugin.Instance.Log.LogInfo("Checkpoint reset load started.");
            _checkpointLoadObserved = true;
            return;
        }

        if (_checkpointLoadObserved && !CheckPointSystem.IsLoadingCheckpoint)
        {
            Plugin.Instance.Log.LogInfo("Checkpoint reset load finished.");
            EndCheckpointResetFlow();
        }
    }

    private static void LoadCheckpointThroughMap(CheckPointTag checkpoint)
    {
        CoreMenu? coreMenu = UnityEngine.Object.FindObjectOfType<CoreMenu>();
        if (coreMenu != null && !coreMenu.Equals(null))
        {
            try
            {
                MapController? mapController = CoreMenuMapControllerField?.GetValue(coreMenu) as MapController;
                MapLocation? mapLocation = FindMapLocationForCheckpoint(mapController, checkpoint);
                if (mapController != null && !mapController.Equals(null) && mapLocation != null && !mapLocation.Equals(null))
                {
                    PrimeCoreMenuForMapReset(coreMenu);
                    Plugin.Instance.Log.LogInfo($"Routing reset through map. checkpoint={DescribeCheckpoint(checkpoint)}, mapIndex={GetLocationCodeFromMapLocation(checkpoint)}");
                    mapController.select_checkpoint(mapLocation);
                    coreMenu.load_checkpoint(mapLocation.checkpoint);
                    return;
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Log.LogWarning($"Failed to route checkpoint reset through map controller: {ex.Message}");
            }
        }

        LoadCheckpointThroughGame(checkpoint);
    }

    private static void LoadCheckpointThroughGame(CheckPointTag checkpoint)
    {
        CoreMenu? coreMenu = UnityEngine.Object.FindObjectOfType<CoreMenu>();
        if (coreMenu != null && !coreMenu.Equals(null))
        {
            PrimeCoreMenuForMapReset(coreMenu);
            Plugin.Instance.Log.LogInfo($"Routing reset through CoreMenu.load_checkpoint directly. checkpoint={DescribeCheckpoint(checkpoint)}");
            coreMenu.load_checkpoint(checkpoint);
            return;
        }

        Plugin.Instance.Log.LogInfo($"Routing reset through CheckPointSystem.LoadCheckpoint directly. checkpoint={DescribeCheckpoint(checkpoint)}");
        CheckPointSystem.LoadCheckpoint(checkpoint);
    }

    private static MapLocation? FindMapLocationForCheckpoint(MapController? mapController, CheckPointTag checkpoint)
    {
        if (mapController == null || mapController.Equals(null))
            return null;

        if (MapLocationsField?.GetValue(mapController) is not Array mapLocations)
            return null;

        for (int i = 0; i < mapLocations.Length; i++)
        {
            if (mapLocations.GetValue(i) is not MapLocation mapLocation || mapLocation.Equals(null))
                continue;

            if (mapLocation.checkpoint == checkpoint)
                return mapLocation;
        }

        return null;
    }

    private static int GetLocationCodeForCheckpoint(CheckPointTag? checkpoint)
    {
        if (checkpoint == null || checkpoint.Equals(null))
            return 0;

        int mapLocationCode = GetLocationCodeFromMapLocation(checkpoint);
        if (mapLocationCode != 0)
            return mapLocationCode;

        string scenePath = checkpoint.GetScenePath() ?? string.Empty;
        string checkpointId = GetCheckpointIdentifier(checkpoint);

        if (ScenePathEndsWith(scenePath, "0_Landing"))
            return 1;

        if (ScenePathEndsWith(scenePath, "1A_Intro"))
        {
            if (checkpointId == FanCheckpointHash)
                return 4;
            if (checkpointId == AlleyCheckpointHash)
                return 3;
            return 2;
        }

        if (ScenePathEndsWith(scenePath, "1B_Exterior"))
        {
            if (checkpointId == StatueCheckpointHash)
                return 7;
            if (checkpointId == HousesCheckpointHash)
                return 6;
            return 5;
        }

        if (ScenePathEndsWith(scenePath, "2A_Spatial"))
        {
            if (checkpointId == ChasmCheckpointHash)
                return 9;
            return 8;
        }

        return 0;
    }

    private static string GetRunLocationForCode(int code)
    {
        return code switch
        {
            1 => "landing",
            2 => "entrance",
            3 => "alley",
            4 => "fan",
            5 => "cloudbed",
            6 => "houses",
            7 => "statue",
            8 => "rounded_room",
            9 => "chasm",
            _ => string.Empty
        };
    }

    private static int GetLocationCodeFromMapLocation(CheckPointTag checkpoint)
    {
        try
        {
            CoreMenu? coreMenu = UnityEngine.Object.FindObjectOfType<CoreMenu>();
            if (coreMenu == null || coreMenu.Equals(null))
                return 0;

            MapController? mapController = CoreMenuMapControllerField?.GetValue(coreMenu) as MapController;
            if (mapController == null || mapController.Equals(null))
                return 0;

            if (MapLocationsField?.GetValue(mapController) is not Array mapLocations)
                return 0;

            for (int i = 0; i < mapLocations.Length; i++)
            {
                if (mapLocations.GetValue(i) is not MapLocation mapLocation || mapLocation.Equals(null))
                    continue;

                if (mapLocation.checkpoint == checkpoint)
                    return i + 1;
            }
        }
        catch
        {
        }

        return 0;
    }

    private static bool ScenePathEndsWith(string scenePath, string sceneName)
    {
        if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(sceneName))
            return false;

        return scenePath.Equals(sceneName, StringComparison.OrdinalIgnoreCase) ||
               scenePath.EndsWith("/" + sceneName + ".unity", StringComparison.OrdinalIgnoreCase) ||
               scenePath.EndsWith("\\" + sceneName + ".unity", StringComparison.OrdinalIgnoreCase) ||
               scenePath.IndexOf(sceneName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetCheckpointIdentifier(CheckPointTag checkpoint)
    {
        try
        {
            string checkpointId = checkpoint.GetCheckpointID() ?? string.Empty;
            if (!string.IsNullOrEmpty(checkpointId))
                return checkpointId;
        }
        catch
        {
        }

        return GetCheckpointSaveKey(checkpoint);
    }

    private static string GetCheckpointSaveKey(CheckPointTag checkpoint)
    {
        try
        {
            return CheckpointSaveKeyField?.GetValue(checkpoint) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string DescribeCheckpoint(CheckPointTag checkpoint)
    {
        string scenePath = string.Empty;
        string checkpointId = string.Empty;
        string saveKey = string.Empty;

        try { scenePath = checkpoint.GetScenePath() ?? string.Empty; } catch { }
        try { checkpointId = checkpoint.GetCheckpointID() ?? string.Empty; } catch { }
        try { saveKey = GetCheckpointSaveKey(checkpoint); } catch { }

        return $"scene={scenePath}, checkpointId={checkpointId}, saveKey={saveKey}";
    }

    private static string GetCheckpointResetSignalPath()
    {
        try
        {
            string gameDirectory = Paths.GameRootPath;
            if (string.IsNullOrEmpty(gameDirectory))
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                    return string.Empty;

                gameDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
            }

            if (string.IsNullOrEmpty(gameDirectory))
                return string.Empty;

            return Path.Combine(gameDirectory, "CheckpointResetSignal");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void WriteCheckpointResetSignalFile(string targetLocation)
    {
        try
        {
            string signalPath = GetCheckpointResetSignalPath();
            if (string.IsNullOrEmpty(signalPath))
            {
                Plugin.Instance.Log.LogWarning("CheckpointResetSignal path was empty.");
                return;
            }

            File.WriteAllText(signalPath, targetLocation ?? string.Empty);
            Plugin.Instance.Log.LogInfo($"CheckpointResetSignal written at {signalPath} with '{targetLocation}'.");
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogWarning($"Failed to write CheckpointResetSignal: {ex.Message}");
        }
    }

    private static void DeleteCheckpointResetSignalFile()
    {
        try
        {
            string signalPath = GetCheckpointResetSignalPath();
            if (string.IsNullOrEmpty(signalPath) || !File.Exists(signalPath))
                return;

            File.Delete(signalPath);
            Plugin.Instance.Log.LogInfo($"CheckpointResetSignal deleted at {signalPath}.");
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogWarning($"Failed to delete CheckpointResetSignal: {ex.Message}");
        }
    }

    private static void PrimeCoreMenuForMapReset(CoreMenu coreMenu)
    {
        try
        {
            FieldInfo? sectionField = typeof(CoreMenu).GetField("section", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo? stateField = typeof(CoreMenu).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);

            if (sectionField != null)
                sectionField.SetValue(coreMenu, Enum.ToObject(sectionField.FieldType, 2));

            if (stateField != null)
                stateField.SetValue(coreMenu, Enum.ToObject(stateField.FieldType, 1));
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogWarning($"Failed to prime CoreMenu map state before checkpoint reset: {ex.Message}");
        }
    }

    public static bool StepNoClipMovement()
    {
        if (!_noclipEnabled || _runtimeFaulted || _hotkeyEditorOpen)
            return false;

        Transform? playerTransform = GetPlayerTransform();
        if (playerTransform == null || _cachedRigidbody == null || _cachedRigidbody.Equals(null))
            return false;

        Keyboard? keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        Transform reference = _cachedCamera != null && !_cachedCamera.Equals(null)
            ? _cachedCamera.transform
            : playerTransform;

        Vector3 horizontalMove = Vector3.zero;
        Vector3 referenceForward = reference.forward;
        Vector3 referenceRight = reference.right;
        if (referenceForward.sqrMagnitude > 0.0001f)
            referenceForward.Normalize();
        if (referenceRight.sqrMagnitude > 0.0001f)
            referenceRight.Normalize();

        if (keyboard.wKey.isPressed)
            horizontalMove += referenceForward;
        if (keyboard.sKey.isPressed)
            horizontalMove -= referenceForward;
        if (keyboard.dKey.isPressed)
            horizontalMove += referenceRight;
        if (keyboard.aKey.isPressed)
            horizontalMove -= referenceRight;

        Vector3 verticalMove = Vector3.zero;
        if (keyboard.spaceKey.isPressed)
            verticalMove += Vector3.up;
        if (keyboard.cKey.isPressed || keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
            verticalMove -= Vector3.up;

        Vector3 finalMove = horizontalMove + verticalMove;
        if (finalMove.sqrMagnitude > 1f)
            finalMove.Normalize();

        _cachedRigidbody.velocity = finalMove * _flightSpeed;
        return true;
    }

    private static void RefreshReferences()
    {
        if (_cachedPlayerMovement == null || _cachedPlayerMovement.Equals(null))
            _cachedPlayerMovement = UnityEngine.Object.FindObjectOfType<PlayerMovement>();

        if (_cachedPlayerMovement != null && (_cachedRigidbody == null || _cachedRigidbody.Equals(null)))
            _cachedRigidbody = _cachedPlayerMovement.GetComponent<Rigidbody>();

        if (_cachedCamera == null || _cachedCamera.Equals(null) || !_cachedCamera.isActiveAndEnabled)
            _cachedCamera = Camera.main;

        if (_hotkeyEditorOpen && _cachedPlayerMovement != null && !_cachedPlayerMovement.Equals(null) && _cachedPlayerMovement.enabled)
            _cachedPlayerMovement.enabled = false;
    }

    private static void UpdateMeasuredSpeed()
    {
        Transform? playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return;

        Vector3 currentPosition = playerTransform.position;
        if (!_hasMeasuredPosition)
        {
            _lastMeasuredPosition = currentPosition;
            _hasMeasuredPosition = true;
            _actualSpeed = 0f;
            return;
        }

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        _actualSpeed = Vector3.Distance(_lastMeasuredPosition, currentPosition) / deltaTime;
        _lastMeasuredPosition = currentPosition;
    }

    private static Transform? GetPlayerTransform()
    {
        if (_cachedPlayerMovement == null || _cachedPlayerMovement.Equals(null))
            return null;

        return _cachedPlayerMovement.transform;
    }

    public static void DrawOverlay()
    {
        if (_runtimeFaulted || !_overlayVisible)
            return;

        try
        {
            EnsureStyles();

            Transform? playerTransform = GetPlayerTransform();
            Vector3 position = playerTransform != null ? playerTransform.position : Vector3.zero;
            Vector3 facing = GetFacingEuler(playerTransform);

            string text = string.Join("\n", new[]
            {
                _noclipEnabled ? $"Turn off NoClip: {_noClipBinding.DisplayName()}" : $"Turn on NoClip: {_noClipBinding.DisplayName()}",
                $"Reset Checkpoint: {_resetCheckpointBinding.DisplayName()}",
                $"Flight Speed: {_flightSpeed:0.0}",
                $"Position: {FormatVector(position)}",
                $"Facing: {FormatVector(facing)}",
                $"Speed: {_actualSpeed:0.000}",
                "Open Hotkey Editor: F9"
            });

            GUI.Label(new Rect(25f, 25f, Screen.width * 0.45f, 170f), text, _textStyle);

            if (_hotkeyEditorOpen)
                DrawHotkeyEditorWindow();
        }
        catch (Exception ex)
        {
            FailRuntime($"Overlay draw failed: {ex}");
        }
    }

    private static void DrawHotkeyEditorWindow()
    {
        EnsureStyles();

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), "");
        GUI.color = previousColor;

        float width = 520f;
        float height = 240f;
        Rect panelRect = new(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.33f,
            width,
            height);

        GUI.Box(panelRect, "");
        GUI.Label(new Rect(panelRect.x + 22f, panelRect.y + 18f, panelRect.width - 44f, 32f), "Hotkey Editor", _editorTitleStyle);

        float rowLabelX = panelRect.x + 24f;
        float rowButtonX = panelRect.x + 250f;
        float rowWidth = 220f;
        float rowHeight = 34f;

        DrawBindingRow("NoClip", _noClipBinding.DisplayName(), PendingRebind.NoClip, rowLabelX, rowButtonX, panelRect.y + 70f, rowWidth, rowHeight);
        DrawBindingRow("Reset Checkpoint", _resetCheckpointBinding.DisplayName(), PendingRebind.ResetCheckpoint, rowLabelX, rowButtonX, panelRect.y + 118f, rowWidth, rowHeight);

        string statusText = _pendingRebind switch
        {
            PendingRebind.NoClip => "Press a key or mouse button for NoClip.",
            PendingRebind.ResetCheckpoint => "Press a key or mouse button for Reset Checkpoint.",
            _ => "Click a binding box to change it. Press F9 to close."
        };

        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 176f, panelRect.width - 48f, 44f), statusText, _editorLabelStyle);
    }

    private static void DrawBindingRow(string label, string bindingText, PendingRebind target, float labelX, float buttonX, float y, float buttonWidth, float buttonHeight)
    {
        GUI.Label(new Rect(labelX, y + 5f, 180f, 26f), label, _editorLabelStyle);

        string buttonText = _pendingRebind == target ? "Press input..." : bindingText;
        Rect buttonRect = new(buttonX, y, buttonWidth, buttonHeight);
        GUI.Box(buttonRect, buttonText, _editorButtonStyle);
        if (WasRectClicked(buttonRect))
        {
            _pendingRebind = target;
            _awaitingRebindRelease = true;
        }
    }

    private static Vector3 GetFacingEuler(Transform? playerTransform)
    {
        if (_cachedCamera != null && !_cachedCamera.Equals(null))
        {
            Vector3 euler = _cachedCamera.transform.rotation.eulerAngles;
            float yaw = playerTransform != null ? playerTransform.rotation.eulerAngles.y : euler.y;
            float roll = playerTransform != null ? playerTransform.rotation.eulerAngles.z : euler.z;
            return new Vector3(euler.x, yaw, roll);
        }

        return playerTransform != null ? playerTransform.rotation.eulerAngles : Vector3.zero;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.x:0.000}, {value.y:0.000}, {value.z:0.000}";
    }

    private static void EnsureStyles()
    {
        if (_textStyle == null)
        {
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18
            };
            _textStyle.normal.textColor = Color.white;
        }

        if (_editorTitleStyle == null)
        {
            _editorTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
            };
            _editorTitleStyle.normal.textColor = Color.white;
        }

        if (_editorLabelStyle == null)
        {
            _editorLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18
            };
            _editorLabelStyle.normal.textColor = Color.white;
        }

        if (_editorButtonStyle == null)
        {
            _editorButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17
            };
        }
    }

    private static bool AnyMouseButtonsPressed()
    {
        Mouse? mouse = Mouse.current;
        if (mouse == null)
            return false;

        return mouse.leftButton.isPressed ||
               mouse.rightButton.isPressed ||
               mouse.middleButton.isPressed ||
               mouse.backButton.isPressed ||
               mouse.forwardButton.isPressed;
    }

    private static bool WasRectClicked(Rect rect)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null)
            return false;

        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            return false;

        if (!rect.Contains(currentEvent.mousePosition))
            return false;

        currentEvent.Use();
        return true;
    }

    private static void ZeroRigidbodyVelocity()
    {
        if (_cachedRigidbody == null || _cachedRigidbody.Equals(null))
            return;

        _cachedRigidbody.velocity = Vector3.zero;
        _cachedRigidbody.angularVelocity = Vector3.zero;
    }

    private static void FailRuntime(string message)
    {
        _runtimeFaulted = true;
        _noclipEnabled = false;
        EndCheckpointResetFlow();
        SetHotkeyEditorOpen(false);

        try
        {
            DisableNoClip();
        }
        catch
        {
        }

        Plugin.Instance.Log.LogError(message);
    }

    private static void SetDetectCollisions(Rigidbody rigidbody, bool enabled)
    {
        try
        {
            var property = typeof(Rigidbody).GetProperty("detectCollisions");
            property?.SetValue(rigidbody, enabled);
        }
        catch
        {
        }
    }

    private enum PendingRebind
    {
        None,
        NoClip,
        ResetCheckpoint
    }
}

[HarmonyPatch(typeof(GameManager), "Update")]
internal static class GameManagerUpdatePatch
{
    private static void Postfix()
    {
        PracticeRuntime.Tick();
    }
}

[HarmonyPatch(typeof(MusicSystem), "OnGUI")]
internal static class MusicSystemOnGuiPatch
{
    private static void Postfix()
    {
        PracticeRuntime.DrawOverlay();
    }
}

[HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
internal static class PlayerMovementFixedUpdatePatch
{
    private static bool Prefix()
    {
        return !PracticeRuntime.StepNoClipMovement();
    }
}
