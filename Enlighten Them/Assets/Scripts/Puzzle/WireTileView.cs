using System;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum WireDirection
{
    None = 0,
    Up = 1 << 0,
    Right = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3
}

public enum WireTileVisualState
{
    Unpowered,
    Powered,
    Dimmed,
    Success
}

public enum WireTileType
{
    Empty,
    Straight,
    Corner,
    Tee,
    Cross,
    Source,
    Target,
    Broken
}

[RequireComponent(typeof(Button))]
public sealed class WireTileView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image wireImage;

    [Header("Colors")]
    [SerializeField]
    private Color unpoweredColor =
        new Color(0.55f, 0.55f, 0.55f, 1f);

    [SerializeField]
    private Color poweredColor =
        new Color(1f, 0.85f, 0.15f, 1f);

    [SerializeField]
    private Color dimmedColor =
        new Color(0.15f, 0.15f, 0.15f, 0.35f);

    [SerializeField]
    private Color successColor =
        new Color(1f, 1f, 0.4f, 1f);

    [SerializeField, Range(0f, 1f)]
    private float endpointColorStrength = 0.8f;

    private Action<WireTileView> clickedCallback;
    private int quarterTurns;
    private Color channelColor = Color.white;
    private Color activePowerColor = Color.white;
    private bool hasActivePowerColor;

    public int Index { get; private set; }
    public int ChannelId { get; private set; } = -1;
    public WireTileType Type { get; private set; }
    public bool IsLocked { get; private set; }

    public WireDirection Connections =>
        GetConnectionsFor(Type, quarterTurns);

    private bool CanRotate =>
        !IsLocked && Type != WireTileType.Empty;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        button.onClick.AddListener(HandleClicked);

        if (wireImage == null)
        {
            Debug.LogError(
                "WireTileView requires a WireImage reference.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void Configure(
        int index,
        WireTileType tileType,
        int startingQuarterTurns,
        Sprite sprite,
        bool locked,
        int channelId,
        Color assignedChannelColor,
        Action<WireTileView> onClicked)
    {
        Index = index;
        Type = tileType;
        IsLocked = locked;
        ChannelId = channelId;
        channelColor = assignedChannelColor;
        activePowerColor = assignedChannelColor;
        hasActivePowerColor = false;
        clickedCallback = onClicked;

        quarterTurns =
            NormalizeQuarterTurns(startingQuarterTurns);

        if (wireImage != null)
        {
            wireImage.sprite = sprite;
            wireImage.enabled =
                sprite != null &&
                tileType != WireTileType.Empty;
        }

        button.interactable = CanRotate;

        ApplyRotation();
        SetVisualState(WireTileVisualState.Unpowered);
    }

    // Keeps older calls valid while the rest of the project is migrated.
    public void Configure(
        int index,
        WireTileType tileType,
        int startingQuarterTurns,
        Sprite sprite,
        bool locked,
        Action<WireTileView> onClicked)
    {
        Configure(
            index,
            tileType,
            startingQuarterTurns,
            sprite,
            locked,
            -1,
            poweredColor,
            onClicked
        );
    }

    public void RotateClockwise()
    {
        if (!CanRotate)
            return;

        quarterTurns =
            NormalizeQuarterTurns(quarterTurns + 1);

        ApplyRotation();
    }

    public void SetPowered(bool powered)
    {
        hasActivePowerColor = false;

        SetVisualState(
            powered
                ? WireTileVisualState.Powered
                : WireTileVisualState.Unpowered
        );
    }

    public void SetPowered(bool powered, Color powerColor)
    {
        activePowerColor = powerColor;
        hasActivePowerColor = powered;

        SetVisualState(
            powered
                ? WireTileVisualState.Powered
                : WireTileVisualState.Unpowered
        );
    }

    public void SetVisualState(
        WireTileVisualState state,
        Color visualColor)
    {
        activePowerColor = visualColor;
        hasActivePowerColor = true;
        SetVisualState(state);
    }

    public void SetVisualState(WireTileVisualState state)
    {
        if (wireImage == null)
            return;

        switch (state)
        {
            case WireTileVisualState.Powered:
                wireImage.color =
                    hasActivePowerColor
                        ? activePowerColor
                        : ChannelId >= 0
                            ? channelColor
                            : poweredColor;
                break;

            case WireTileVisualState.Dimmed:
                wireImage.color = dimmedColor;
                break;

            case WireTileVisualState.Success:
                wireImage.color =
                    hasActivePowerColor
                        ? Color.Lerp(
                            activePowerColor,
                            Color.white,
                            0.45f
                        )
                        : ChannelId >= 0
                            ? Color.Lerp(
                                channelColor,
                                Color.white,
                                0.45f
                            )
                            : successColor;
                break;

            default:
                bool isEndpoint =
                    Type == WireTileType.Source ||
                    Type == WireTileType.Target;

                wireImage.color =
                    isEndpoint && ChannelId >= 0
                        ? Color.Lerp(
                            unpoweredColor,
                            channelColor,
                            endpointColorStrength
                        )
                        : unpoweredColor;
                break;
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        button.interactable = enabled && CanRotate;
    }

    private void HandleClicked()
    {
        if (!CanRotate)
            return;

        clickedCallback?.Invoke(this);
    }

    private void ApplyRotation()
    {
        if (wireImage == null)
            return;

        float zRotation = quarterTurns * -90f;

        wireImage.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, zRotation);
    }

    public static WireDirection GetConnectionsFor(
        WireTileType tileType,
        int turns)
    {
        WireDirection directions =
            GetBaseConnections(tileType);

        int normalizedTurns =
            NormalizeQuarterTurns(turns);

        for (int i = 0; i < normalizedTurns; i++)
            directions = RotateDirectionsClockwise(directions);

        return directions;
    }

    private static int NormalizeQuarterTurns(int value)
    {
        value %= 4;

        if (value < 0)
            value += 4;

        return value;
    }

    private static WireDirection GetBaseConnections(
        WireTileType tileType)
    {
        switch (tileType)
        {
            case WireTileType.Straight:
                return WireDirection.Up |
                       WireDirection.Down;

            case WireTileType.Corner:
                return WireDirection.Up |
                       WireDirection.Right;

            case WireTileType.Tee:
                return WireDirection.Left |
                       WireDirection.Up |
                       WireDirection.Right;

            case WireTileType.Cross:
                return WireDirection.Up |
                       WireDirection.Right |
                       WireDirection.Down |
                       WireDirection.Left;

            case WireTileType.Source:
                return WireDirection.Right;

            case WireTileType.Target:
                return WireDirection.Left;

            default:
                return WireDirection.None;
        }
    }

    private static WireDirection RotateDirectionsClockwise(
        WireDirection directions)
    {
        WireDirection rotated = WireDirection.None;

        if ((directions & WireDirection.Up) != 0)
            rotated |= WireDirection.Right;

        if ((directions & WireDirection.Right) != 0)
            rotated |= WireDirection.Down;

        if ((directions & WireDirection.Down) != 0)
            rotated |= WireDirection.Left;

        if ((directions & WireDirection.Left) != 0)
            rotated |= WireDirection.Up;

        return rotated;
    }
}
