﻿using Godot;
using Newtonsoft.Json;

/// <summary>
///   Camera for the strategy stages of the game
/// </summary>
public class StrategicCamera : Camera
{
    private bool edgePanEnabled;

    private bool cursorDirty = true;
    private Vector3 cursorWorldPos;

    /// <summary>
    ///   The position the camera is over
    /// </summary>
    [Export]
    [JsonProperty]
    public Vector3 WorldLocation { get; set; }

    /// <summary>
    ///   How fast the camera pans with user input
    /// </summary>
    [Export]
    [JsonProperty]
    public float CameraPanSpeed { get; set; } = 40;

    [Export]
    [JsonProperty]
    public float ZoomLevel { get; set; } = 1;

    [Export]
    [JsonProperty]
    public float MinZoomLevel { get; set; } = 0.1f;

    [Export]
    [JsonProperty]
    public float MaxZoomLevel { get; set; } = 2.5f;

    /// <summary>
    ///   How fast the zoom level changes
    /// </summary>
    [Export]
    [JsonProperty]
    public float ZoomSpeed { get; set; } = 0.06f;

    /// <summary>
    ///   Multiplier for how zooming out affects the camera movement speed
    /// </summary>
    [Export]
    [JsonProperty]
    public float ZoomLevelMovementSpeedModifier { get; set; } = 1.4f;

    /// <summary>
    ///   When true this camera allows the player to control the position and zoom of the camera.
    ///   If false the camera can only be controlled through code (for example to play an animation)
    /// </summary>
    [Export]
    public bool AllowPlayerInput { get; set; } = true;

    /// <summary>
    ///   Returns the position the player is pointing to with their cursor
    /// </summary>
    [JsonIgnore]
    public Vector3 CursorWorldPos
    {
        get
        {
            if (cursorDirty)
                UpdateCursorWorldPos();
            return cursorWorldPos;
        }
        private set => cursorWorldPos = value;
    }

    public override void _Ready()
    {
        ProcessPriority = 1000;

        // TODO: add controlling for the listener parented to this camera
    }

    public override void _EnterTree()
    {
        base._EnterTree();

        edgePanEnabled = Settings.Instance.PanStrategyViewWithMouse.Value;

        Settings.Instance.PanStrategyViewWithMouse.OnChanged += ReadEdgePanMode;

        InputManager.RegisterReceiver(this);
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        Settings.Instance.PanStrategyViewWithMouse.OnChanged -= ReadEdgePanMode;

        InputManager.UnregisterReceiver(this);
    }

    public override void _Process(float delta)
    {
        cursorDirty = true;

        if (edgePanEnabled && AllowPlayerInput)
            HandleEdgePanning(delta);

        // TODO: interpolating if there's a small movement for more smoothness?
        GlobalTransform = StrategicCameraHelpers.CalculateCameraPosition(WorldLocation, ZoomLevel);
    }

    [RunOnAxis(new[] { "g_zoom_in", "g_zoom_out" }, new[] { -1.0f, 1.0f }, UseDiscreteKeyInputs = true, Priority = -1)]
    public bool Zoom(float delta, float value)
    {
        if (!Current || !AllowPlayerInput)
            return false;

        ZoomLevel = Mathf.Clamp(ZoomLevel + ZoomSpeed * value, MinZoomLevel, MaxZoomLevel);
        return true;
    }

    [RunOnAxis(new[] { "g_move_forward", "g_move_backwards" }, new[] { -1.0f, 1.0f })]
    [RunOnAxis(new[] { "g_move_left", "g_move_right" }, new[] { -1.0f, 1.0f })]
    [RunOnAxisGroup]
    public bool PanCamera(float delta, float forwardMovement, float leftRightMovement)
    {
        if (!Current || !AllowPlayerInput)
            return false;

        var movement = new Vector3(leftRightMovement, 0, forwardMovement);

        // TODO: check if delta makes the movement feel good or not
        ApplyPan(movement * CameraPanSpeed * ZoomLevel * ZoomLevelMovementSpeedModifier * delta);

        return true;
    }

    private void ReadEdgePanMode(bool newValue)
    {
        edgePanEnabled = newValue;
    }

    private void HandleEdgePanning(float delta)
    {
        var viewport = GetViewport();
        var cursor = viewport.GetMousePosition();

        // Rather than using the window size here from the Viewport, we need the GUI scaled size
        var screenSize = GUICommon.Instance.ViewportSize;

        float leftRight = 0;
        float upDown = 0;

        if (cursor.x <= Constants.EDGE_PAN_PIXEL_THRESHOLD)
        {
            leftRight = -1;
        }
        else if (cursor.x + Constants.EDGE_PAN_PIXEL_THRESHOLD >= screenSize.x)
        {
            leftRight = 1;
        }

        if (cursor.y <= Constants.EDGE_PAN_PIXEL_THRESHOLD)
        {
            upDown = -1;
        }
        else if (cursor.y + Constants.EDGE_PAN_PIXEL_THRESHOLD >= screenSize.y)
        {
            upDown = 1;
        }

        if (leftRight != 0 || upDown != 0)
        {
            var scale = Settings.Instance.PanStrategyViewMouseSpeed.Value * delta * ZoomLevel *
                ZoomLevelMovementSpeedModifier;

            ApplyPan(new Vector3(leftRight * scale, 0, upDown * scale));
        }
    }

    private void ApplyPan(Vector3 scaledMovement)
    {
        var rotation = new Quat(Vector3.Up, GlobalTransform.basis.GetEuler().y);

        var movement = rotation.Xform(scaledMovement);

        WorldLocation += movement;
    }

    private void UpdateCursorWorldPos()
    {
        // TODO: this will need access to the terrain data or cast a physics ray or something here
        var worldPlane = new Plane(new Vector3(0, 1, 0), 0.0f);

        var viewPort = GetViewport();

        if (viewPort == null)
        {
            GD.PrintErr("Strategic camera is not related to a viewport, can't update mouse world position");
            return;
        }

        var mousePos = viewPort.GetMousePosition();

        var intersection = worldPlane.IntersectRay(ProjectRayOrigin(mousePos),
            ProjectRayNormal(mousePos));

        if (intersection.HasValue)
        {
            CursorWorldPos = intersection.Value;
        }

        cursorDirty = false;
    }
}
