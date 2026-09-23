using System.IO;
using System.Text.Json;
using AOH.Game.Infrastructure.Persistence;
using Godot;

namespace AOH.Game.Presentation;

[GlobalClass]
public partial class GameLauncher : Control
{
    private const string AutosaveSlot = "autosave";
    private PanelContainer _menuPanel = null!;
    private PanelContainer _settingsPanel = null!;
    private Button _continueButton = null!;
    private Label _statusLabel = null!;
    private GameBootstrap? _currentGame;
    private readonly JsonSaveGameRepository _saveRepository = new(Path.Combine(OS.GetUserDataDir(), "saves"));
    private readonly string _settingsPath = Path.Combine(OS.GetUserDataDir(), "display_settings.json");

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        DisplayServer.WindowSetMinSize(new Vector2I(800, 600));
        LoadDisplaySettings();
        var currentWindowSize = DisplayServer.WindowGetSize();
        DisplayServer.WindowSetSize(FitResolutionToScreen(currentWindowSize.X, currentWindowSize.Y));
        BuildMenu();
        UpdateContinueButton();
        Resized += ApplyResponsiveLayout;
        ApplyResponsiveLayout();
        CenterWindowOnCurrentScreen();
    }

    private void BuildMenu()
    {
        var background = new ColorRect
        {
            Color = new Color("#071018"),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var menuCenter = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        menuCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(menuCenter);

        _menuPanel = CreatePanel(new Vector2(460, 520));
        menuCenter.AddChild(_menuPanel);

        var menuContent = new VBoxContainer();
        menuContent.AddThemeConstantOverride("separation", 14);
        _menuPanel.AddChild(menuContent);

        var crest = new Label
        {
            Text = "⚔  LỤC HẢI  ⚔",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        crest.AddThemeColorOverride("font_color", new Color("#ffd166"));
        crest.AddThemeFontSizeOverride("font_size", 34);
        menuContent.AddChild(crest);

        var subtitle = new Label
        {
            Text = "VẬN MỆNH CỦA MỘT ĐẠI LỤC",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        menuContent.AddChild(subtitle);
        menuContent.AddChild(new HSeparator());

        menuContent.AddChild(CreateMenuButton("Bắt đầu chiến dịch", StartNewGame, active: true));
        _continueButton = CreateMenuButton("Tiếp tục chiến dịch", ContinueGame);
        menuContent.AddChild(_continueButton);
        menuContent.AddChild(CreateMenuButton("Cài đặt", ShowSettings));
        menuContent.AddChild(CreateMenuButton("Thoát game", () => GetTree().Quit()));

        _statusLabel = new Label
        {
            Text = "Bốn vương triều đang chờ lệnh của bạn.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        menuContent.AddChild(_statusLabel);

        BuildSettingsPanel();
    }

    private void BuildSettingsPanel()
    {
        var settingsCenter = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        settingsCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(settingsCenter);

        _settingsPanel = CreatePanel(new Vector2(560, 410));
        _settingsPanel.Visible = false;
        settingsCenter.AddChild(_settingsPanel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 16);
        _settingsPanel.AddChild(content);

        var title = new Label
        {
            Text = "CÀI ĐẶT HIỂN THỊ",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", new Color("#ffd166"));
        title.AddThemeFontSizeOverride("font_size", 22);
        content.AddChild(title);
        content.AddChild(new HSeparator());
        content.AddChild(new Label { Text = "Độ phân giải cửa sổ" });
        content.AddChild(CreateMenuButton("1280 × 720", () => ApplyResolution(1280, 720)));
        content.AddChild(CreateMenuButton("1600 × 900", () => ApplyResolution(1600, 900)));
        content.AddChild(CreateMenuButton("1920 × 1080", () => ApplyResolution(1920, 1080)));
        content.AddChild(CreateMenuButton("Quay lại", HideSettings));
    }

    private void StartNewGame()
    {
        StartGameFromSave(null);
    }

    private void ContinueGame()
    {
        StartGameFromSave(AutosaveSlot);
    }

    private void StartGameFromSave(string? slotName)
    {
        var game = new GameBootstrap();
        game.ReturnRequested += HandleReturnToMenu;
        AddChild(game);
        _currentGame = game;

        if (slotName is not null)
        {
            var result = game.LoadGame(slotName);
            if (!result["success"].AsBool())
            {
                RemoveChild(game);
                game.QueueFree();
                _currentGame = null;
                ShowStatus(result["message"].AsString(), isError: true);
                UpdateContinueButton();
                return;
            }
        }

        _menuPanel.Visible = false;
        _settingsPanel.Visible = false;
    }

    private void HandleReturnToMenu()
    {
        if (_currentGame is not null)
        {
            _currentGame.ReturnRequested -= HandleReturnToMenu;
            RemoveChild(_currentGame);
            _currentGame.QueueFree();
            _currentGame = null;
        }

        _menuPanel.Visible = true;
        UpdateContinueButton();
    }

    private void ShowSettings()
    {
        _menuPanel.Visible = false;
        _settingsPanel.Visible = true;
    }

    private void HideSettings()
    {
        _settingsPanel.Visible = false;
        _menuPanel.Visible = true;
    }

    private void UpdateContinueButton()
    {
        _continueButton.Disabled = !_saveRepository.SaveExists(AutosaveSlot);
    }

    private void ApplyResolution(int width, int height)
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var appliedSize = FitResolutionToScreen(width, height);
        DisplayServer.WindowSetSize(appliedSize);
        CenterWindowOnCurrentScreen();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var settings = new DisplaySettings { Width = appliedSize.X, Height = appliedSize.Y };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings));
            ShowStatus($"Đã đặt cửa sổ {appliedSize.X} × {appliedSize.Y}.", isError: false);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Không thể lưu cài đặt hiển thị: {exception.Message}");
        }
    }

    private void LoadDisplaySettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<DisplaySettings>(File.ReadAllText(_settingsPath));
            if (settings is null || settings.Width is < 800 or > 3840 || settings.Height is < 600 or > 2160)
            {
                return;
            }

            DisplayServer.WindowSetSize(FitResolutionToScreen(settings.Width, settings.Height));
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Không thể đọc cài đặt hiển thị: {exception.Message}");
        }
    }

    private static void CenterWindowOnCurrentScreen()
    {
        var screen = DisplayServer.WindowGetCurrentScreen();
        var usableArea = DisplayServer.ScreenGetUsableRect(screen);
        var windowSize = DisplayServer.WindowGetSize();
        var centeredPosition = usableArea.Position + ((usableArea.Size - windowSize) / 2);
        DisplayServer.WindowSetPosition(centeredPosition);
    }

    private static Vector2I FitResolutionToScreen(int width, int height)
    {
        var usableSize = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen()).Size;
        var maximumSize = new Vector2I(Math.Max(800, usableSize.X - 16), Math.Max(600, usableSize.Y - 48));
        var scale = Math.Min(1f, Math.Min(maximumSize.X / (float)width, maximumSize.Y / (float)height));
        return new Vector2I(Math.Max(800, Mathf.RoundToInt(width * scale)), Math.Max(600, Mathf.RoundToInt(height * scale)));
    }

    private void ApplyResponsiveLayout()
    {
        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f || _menuPanel is null || _settingsPanel is null)
        {
            return;
        }

        var availableSize = new Vector2(Math.Max(440f, viewportSize.X - 40f), Math.Max(360f, viewportSize.Y - 40f));
        _menuPanel.CustomMinimumSize = new Vector2(Math.Min(460f, availableSize.X), Math.Min(520f, availableSize.Y));
        _settingsPanel.CustomMinimumSize = new Vector2(Math.Min(560f, availableSize.X), Math.Min(410f, availableSize.Y));
    }

    private void ShowStatus(string message, bool isError)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", isError ? new Color("#fb7185") : new Color("#94a3b8"));
    }

    private static PanelContainer CreatePanel(Vector2 minimumSize)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.07f, 0.11f, 0.96f),
            BorderColor = new Color("#c89b3c"),
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            ContentMarginLeft = 34,
            ContentMarginRight = 34,
            ContentMarginTop = 30,
            ContentMarginBottom = 30
        };
        style.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Button CreateMenuButton(string text, Action onPressed, bool active = false)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 46)
        };
        var normal = new StyleBoxFlat
        {
            BgColor = active ? new Color("#253549") : new Color("#151f2c"),
            BorderColor = active ? new Color("#ffd166") : new Color("#334155"),
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        normal.SetBorderWidthAll(1);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeColorOverride("font_color", active ? new Color("#ffd166") : new Color("#f1f5f9"));
        button.AddThemeColorOverride("font_hover_color", new Color("#ffd166"));
        button.AddThemeFontSizeOverride("font_size", 15);
        button.Pressed += onPressed;
        return button;
    }

    private sealed class DisplaySettings
    {
        public DisplaySettings()
        {
        }

        public int Width { get; init; } = 1600;

        public int Height { get; init; } = 900;
    }
}
