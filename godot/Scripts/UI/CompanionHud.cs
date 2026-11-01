using System;
using Godot;
using WobbleHero.Core;
using WobbleHero.Networking;

namespace WobbleHero.UI;

/// <summary>
/// On-screen HUD displaying active game telemetry, balance board tilt gauges,
/// companion connection URL, and zero-dependency QR code.
/// </summary>
public partial class CompanionHud : CanvasLayer
{
    private Label? _gameTitleLabel;
    private Label? _scoreLabel;
    private Label? _stateLabel;
    private Label? _urlLabel;
    private TextureRect? _qrTextureRect;
    private ProgressBar? _rollBar;
    private ProgressBar? _pitchBar;
    private Label? _sourceLabel;

    private GameManager? _manager;

    public void Initialize(GameManager manager)
    {
        _manager = manager;
        BuildUi();
    }

    private void BuildUi()
    {
        // Root Control container
        var root = new Control
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(root);

        // 1. Top Bar: Game Title & Score
        var topMargin = new MarginContainer
        {
            AnchorRight = 1.0f,
            OffsetLeft = 32,
            OffsetTop = 24,
            OffsetRight = -32,
            OffsetBottom = 96,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(topMargin);

        var topHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        topMargin.AddChild(topHBox);

        _gameTitleLabel = new Label { Text = "WobbleHero" };
        _gameTitleLabel.AddThemeFontSizeOverride("font_size", 32);
        topHBox.AddChild(_gameTitleLabel);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        topHBox.AddChild(spacer);

        _stateLabel = new Label
        {
            Text = "START",
            Modulate = new Color(0.4f, 0.7f, 1.0f)
        };
        _stateLabel.AddThemeFontSizeOverride("font_size", 24);
        topHBox.AddChild(_stateLabel);

        var spacer2 = new Control { CustomMinimumSize = new Vector2(32, 0) };
        topHBox.AddChild(spacer2);

        _scoreLabel = new Label
        {
            Text = "SCORE: 0",
            Modulate = new Color(0.98f, 0.78f, 0.15f)
        };
        _scoreLabel.AddThemeFontSizeOverride("font_size", 36);
        topHBox.AddChild(_scoreLabel);

        // 2. Bottom Right: QR Code & Companion URL
        var bottomMargin = new MarginContainer
        {
            AnchorLeft = 1.0f,
            AnchorTop = 1.0f,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetLeft = -280,
            OffsetTop = -240,
            OffsetRight = -24,
            OffsetBottom = -24,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(bottomMargin);

        var panel = new PanelContainer();
        bottomMargin.AddChild(panel);

        var vBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vBox.AddThemeConstantOverride("separation", 6);
        panel.AddChild(vBox);

        var qrPrompt = new Label
        {
            Text = "Scan to Connect Remote",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        qrPrompt.AddThemeFontSizeOverride("font_size", 14);
        vBox.AddChild(qrPrompt);

        _qrTextureRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(130, 130),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        vBox.AddChild(_qrTextureRect);

        _urlLabel = new Label
        {
            Text = "http://localhost:8080/remote",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(0.7f, 0.85f, 1.0f)
        };
        _urlLabel.AddThemeFontSizeOverride("font_size", 12);
        vBox.AddChild(_urlLabel);

        // 3. Bottom Left: Balance Board Input Gauges
        var leftMargin = new MarginContainer
        {
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetLeft = 24,
            OffsetTop = -140,
            OffsetRight = 260,
            OffsetBottom = -24,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(leftMargin);

        var leftPanel = new PanelContainer();
        leftMargin.AddChild(leftPanel);

        var leftVBox = new VBoxContainer();
        leftVBox.AddThemeConstantOverride("separation", 6);
        leftPanel.AddChild(leftVBox);

        _sourceLabel = new Label
        {
            Text = "Board: Idle (Keyboard/Touch)",
            Modulate = new Color(0.6f, 0.8f, 1.0f)
        };
        _sourceLabel.AddThemeFontSizeOverride("font_size", 13);
        leftVBox.AddChild(_sourceLabel);

        var rollLabel = new Label { Text = "Roll (Left / Right):" };
        rollLabel.AddThemeFontSizeOverride("font_size", 11);
        leftVBox.AddChild(rollLabel);
        _rollBar = new ProgressBar
        {
            MinValue = -1.0,
            MaxValue = 1.0,
            Value = 0.0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 12)
        };
        leftVBox.AddChild(_rollBar);

        var pitchLabel = new Label { Text = "Pitch (Front / Back):" };
        pitchLabel.AddThemeFontSizeOverride("font_size", 11);
        leftVBox.AddChild(pitchLabel);
        _pitchBar = new ProgressBar
        {
            MinValue = -1.0,
            MaxValue = 1.0,
            Value = 0.0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 12)
        };
        leftVBox.AddChild(_pitchBar);
    }

    public void UpdateCompanionUrl(string url)
    {
        if (_urlLabel != null)
        {
            _urlLabel.Text = url;
        }

        // Generate QR Code texture
        if (_qrTextureRect != null)
        {
            try
            {
                var qrTex = QrCodeGenerator.GenerateTexture(url, border: 2);
                _qrTextureRect.Texture = qrTex;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionHud] QR generation error: {ex.Message}");
            }
        }
    }

    public void UpdateTelemetry(BoardState board, string gameTitle, string gameState, int score)
    {
        if (_gameTitleLabel != null) _gameTitleLabel.Text = $"WobbleHero • {gameTitle}";
        if (_stateLabel != null) _stateLabel.Text = gameState;
        if (_scoreLabel != null) _scoreLabel.Text = $"SCORE: {score}";

        if (_rollBar != null) _rollBar.Value = board.Roll;
        if (_pitchBar != null) _pitchBar.Value = board.Pitch;

        if (_sourceLabel != null)
        {
            string src = board.Source.ToUpperInvariant();
            string status = board.IsActive ? $"ACTIVE ({board.Hz} Hz)" : "WAITING";
            _sourceLabel.Text = $"Board: {src} ({status})";
            _sourceLabel.Modulate = board.IsActive ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
        }
    }
}
