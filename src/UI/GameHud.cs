using System;
using System.Collections.Generic;
using System.Globalization;
using AOH.Game.Presentation;
using Godot;

namespace AOH.Game.UI;

public partial class GameHud : Control
{
    private GameBootstrap? _session;
    private int _selectedProvinceId = -1;
    private int _hoveredProvinceId = -1;
    private int _selectedArmyId = -1;

    // UI Nodes
    private PanelContainer _topBar = null!;
    private Label _dateLabel = null!;
    private Label _worldStatsLabel = null!;
    private Button[] _speedButtons = [];
    private PanelContainer _provincePanel = null!;
    private Label _provinceNameLabel = null!;
    private Label _terrainBadge = null!;
    private Label _coastalBadge = null!;
    private ColorRect _ownerColorBar = null!;
    private Label _ownerCountryLabel = null!;
    private Label _controllerLabel = null!;
    private Label _populationValue = null!;
    private Label _taxRateValue = null!;
    private Label _economyValue = null!;
    private Label _developmentValue = null!;
    private Label _manpowerValue = null!;
    private Container _actionsContainer = null!;
    private Container _armyListContainer = null!;
    private Container _neighborsContainer = null!;
    private Label _armyStatusLabel = null!;
    private PanelContainer _hoverTooltip = null!;
    private Label _tooltipName = null!;
    private Label _tooltipOwner = null!;
    private Label _tooltipDetails = null!;
    private ColorRect _tooltipColorPill = null!;
    private PanelContainer _ledgerModal = null!;
    private VBoxContainer _ledgerListContainer = null!;
    private PanelContainer _placeholderCard = null!;

    public override void _Ready()
    {
        // Configure full screen overlay
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        BuildInterface();
        UpdateProvinceDisplay(-1);
        HideTooltip();
        _ledgerModal.Visible = false;
    }

    public override void _Process(double delta)
    {
        // Update floating tooltip to follow cursor smoothly
        if (_hoverTooltip.Visible)
        {
            var mousePos = GetViewport().GetMousePosition();
            var viewportSize = GetViewportRect().Size;

            float targetX = mousePos.X + 18f;
            float targetY = mousePos.Y + 18f;

            // Clamp so tooltip doesn't go off screen
            if (targetX + _hoverTooltip.Size.X > viewportSize.X - 12f)
            {
                targetX = mousePos.X - _hoverTooltip.Size.X - 18f;
            }

            if (targetY + _hoverTooltip.Size.Y > viewportSize.Y - 12f)
            {
                targetY = mousePos.Y - _hoverTooltip.Size.Y - 18f;
            }

            _hoverTooltip.Position = new Vector2(targetX, targetY);
        }
    }

    // --- Handoff Contract API Methods ---

    public void BindSession(GameBootstrap session)
    {
        _session = session;
    }

    public void InitializeWorld(Godot.Collections.Dictionary summary)
    {
        UpdateWorldSummary(summary);
        PopulateLedger();
    }

    public void OnWorldTicked(Godot.Collections.Dictionary summary)
    {
        UpdateWorldSummary(summary);

        if (_selectedProvinceId > 0)
        {
            UpdateProvinceDisplay(_selectedProvinceId);
        }

        if (_ledgerModal.Visible)
        {
            PopulateLedger();
        }

        UpdateArmyStatus();
    }

    public void OnProvinceSelected(int provinceId)
    {
        _selectedProvinceId = provinceId;
        UpdateProvinceDisplay(provinceId);
    }

    public void OnProvinceHovered(int provinceId)
    {
        _hoveredProvinceId = provinceId;
        UpdateHoverTooltip(provinceId);
    }

    public void OnArmySelected(int armyId)
    {
        if (armyId <= 0 || _session is null)
        {
            return;
        }

        var army = _session.GetArmyDetails(armyId);
        if (army.Count == 0 || !army["isPlayerArmy"].AsBool())
        {
            return;
        }

        _selectedArmyId = armyId;
        _selectedProvinceId = army["currentProvinceId"].AsInt32();
        UpdateProvinceDisplay(_selectedProvinceId);
        UpdateArmyStatus();
    }

    // --- UI State Updates ---

    private void UpdateProvinceDisplay(int provinceId)
    {
        if (provinceId <= 0 || _session is null)
        {
            _provincePanel.Visible = false;
            _placeholderCard.Visible = true;
            return;
        }

        var details = _session.GetProvinceDetails(provinceId);
        if (details.Count == 0)
        {
            _provincePanel.Visible = false;
            _placeholderCard.Visible = true;
            return;
        }

        _placeholderCard.Visible = false;
        _provincePanel.Visible = true;

        string name = details["name"].AsString();
        string ownerName = details["ownerCountryName"].AsString();
        string ownerColor = details["ownerMapColor"].AsString();
        string controllerName = details["controllerCountryName"].AsString();
        int population = details["population"].AsInt32();
        double economy = details["economy"].AsDouble();
        double development = details["development"].AsDouble();
        double taxRate = details["taxRate"].AsDouble();
        int manpower = details["manpower"].AsInt32();
        string terrain = details["terrain"].AsString();
        bool isCoastal = details["isCoastal"].AsBool();

        _provinceNameLabel.Text = name;
        _terrainBadge.Text = GetTerrainIcon(terrain) + " " + TranslateTerrain(terrain);
        _terrainBadge.SelfModulate = GetTerrainColor(terrain);

        _coastalBadge.Text = isCoastal ? "🌊 Ven Biển" : "🏞️ Nội Địa";
        _coastalBadge.SelfModulate = isCoastal ? new Color("#38bdf8") : new Color("#94a3b8");

        _ownerCountryLabel.Text = ownerName;
        _ownerColorBar.Color = Color.FromHtml(ownerColor);

        if (controllerName != ownerName)
        {
            _controllerLabel.Text = $"⚠️ Bị chiếm đóng bởi: {controllerName}";
            _controllerLabel.Visible = true;
        }
        else
        {
            _controllerLabel.Text = "🛡️ Chủ quyền toàn vẹn";
            _controllerLabel.Visible = true;
        }

        _populationValue.Text = population.ToString("N0", CultureInfo.InvariantCulture);
        _taxRateValue.Text = (taxRate * 100).ToString("F1") + "%";
        _economyValue.Text = economy.ToString("F1");
        _developmentValue.Text = development.ToString("F1");
        _manpowerValue.Text = manpower.ToString("N0", CultureInfo.InvariantCulture);

        // Update neighbors
        UpdateNeighborsList(provinceId);
        UpdateArmyList(provinceId);
    }

    private void UpdateNeighborsList(int provinceId)
    {
        // Clear previous buttons
        foreach (var child in _neighborsContainer.GetChildren())
        {
            child.QueueFree();
        }

        var neighbors = _session?.GetProvinceNeighborSummaries(provinceId);
        var neighborNames = new List<string>();
        if (neighbors is not null)
        {
            foreach (var neighbor in neighbors)
            {
                neighborNames.Add(neighbor["name"].AsString());
            }
        }

        var hintLabel = new Label
        {
            Text = neighborNames.Count > 0
                ? string.Join(" · ", neighborNames)
                : "Không có tỉnh giáp ranh",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hintLabel.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        hintLabel.AddThemeFontSizeOverride("font_size", 11);
        _neighborsContainer.AddChild(hintLabel);
    }

    private void UpdateArmyList(int provinceId)
    {
        foreach (var child in _armyListContainer.GetChildren())
        {
            child.QueueFree();
        }

        var armies = _session?.GetArmiesAtProvince(provinceId);
        if (armies is null)
        {
            return;
        }

        foreach (var army in armies)
        {
            if (!army["isPlayerArmy"].AsBool())
            {
                continue;
            }

            var armyId = army["armyId"].AsInt32();
            var soldiers = army["soldiers"].AsInt32();
            var button = CreateStyledButton(
                $"⚔ Quân #{armyId} · {soldiers.ToString("N0", CultureInfo.InvariantCulture)} binh sĩ",
                () =>
                {
                    _selectedArmyId = armyId;
                    UpdateArmyStatus();
                },
                isActive: armyId == _selectedArmyId);
            _armyListContainer.AddChild(button);
        }
    }

    private void UpdateArmyStatus()
    {
        if (_selectedArmyId <= 0 || _session is null)
        {
            _armyStatusLabel.Text = "Chọn quân trên bản đồ hoặc trong danh sách để ra lệnh.";
            return;
        }

        var army = _session.GetArmyDetails(_selectedArmyId);
        if (army.Count == 0)
        {
            _selectedArmyId = -1;
            _armyStatusLabel.Text = "Đội quân đã không còn tồn tại.";
            return;
        }

        var currentProvince = army["currentProvinceName"].AsString();
        var targetProvince = army["targetProvinceName"].AsString();
        var soldiers = army["soldiers"].AsInt32();
        _armyStatusLabel.Text = string.IsNullOrEmpty(targetProvince)
            ? $"Đang chọn Quân #{_selectedArmyId}: {soldiers.ToString("N0", CultureInfo.InvariantCulture)} binh sĩ tại {currentProvince}."
            : $"Quân #{_selectedArmyId} đang hành quân từ {currentProvince} tới {targetProvince}.";
    }

    private void UpdateHoverTooltip(int provinceId)
    {
        if (provinceId <= 0 || _session is null)
        {
            HideTooltip();
            return;
        }

        var details = _session.GetProvinceDetails(provinceId);
        if (details.Count == 0)
        {
            HideTooltip();
            return;
        }

        string name = details["name"].AsString();
        string ownerName = details["ownerCountryName"].AsString();
        string ownerColor = details["ownerMapColor"].AsString();
        string terrain = details["terrain"].AsString();
        int population = details["population"].AsInt32();

        _tooltipName.Text = name;
        _tooltipOwner.Text = ownerName;
        _tooltipColorPill.Color = Color.FromHtml(ownerColor);
        _tooltipDetails.Text = $"{GetTerrainIcon(terrain)} {TranslateTerrain(terrain)}  •  👥 {population / 1000}k dân";

        _hoverTooltip.Visible = true;
    }

    private void HideTooltip()
    {
        _hoverTooltip.Visible = false;
    }

    private void PopulateLedger()
    {
        if (_session is null) return;

        foreach (var child in _ledgerListContainer.GetChildren())
        {
            child.QueueFree();
        }

        var countries = _session.GetCountrySummaries();
        foreach (var country in countries)
        {
            string name = country["name"].AsString();
            string colorHex = country["mapColor"].AsString();
            int countryId = country["countryId"].AsInt32();
            int provCount = country["provinceCount"].AsInt32();
            bool isAi = country["isAiControlled"].AsBool();
            bool isAtWar = country["isAtWarWithPlayer"].AsBool();
            double warScore = country["warScore"].AsDouble();
            double treasury = country["treasury"].AsDouble();
            double income = country["income"].AsDouble();

            var row = new PanelContainer();
            var rowStyle = CreateStyleBox(new Color("#16202e"), new Color("#2d3d52"), 1, 4, 8, 8);
            row.AddThemeStyleboxOverride("panel", rowStyle);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 16);

            var colorBar = new ColorRect
            {
                CustomMinimumSize = new Vector2(8, 28),
                Color = Color.FromHtml(colorHex)
            };
            hBox.AddChild(colorBar);

            var nameLabel = new Label
            {
                Text = name,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            nameLabel.AddThemeColorOverride("font_color", new Color("#f8fafc"));
            nameLabel.AddThemeFontSizeOverride("font_size", 15);
            hBox.AddChild(nameLabel);

            var provLabel = new Label
            {
                Text = $"{provCount} Tỉnh",
                CustomMinimumSize = new Vector2(80, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            provLabel.AddThemeColorOverride("font_color", new Color("#fbbf24"));
            provLabel.AddThemeFontSizeOverride("font_size", 14);
            hBox.AddChild(provLabel);

            var treasuryLabel = new Label
            {
                Text = $"💰 {treasury.ToString("N0", CultureInfo.InvariantCulture)} (+{income.ToString("N0", CultureInfo.InvariantCulture)}/ngày)",
                CustomMinimumSize = new Vector2(170, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            treasuryLabel.AddThemeColorOverride("font_color", new Color("#4ade80"));
            treasuryLabel.AddThemeFontSizeOverride("font_size", 12);
            hBox.AddChild(treasuryLabel);

            var aiBadge = new Label
            {
                Text = isAi ? "🤖 AI" : "👑 Người chơi",
                CustomMinimumSize = new Vector2(110, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            aiBadge.AddThemeColorOverride("font_color", isAi ? new Color("#94a3b8") : new Color("#4ade80"));
            aiBadge.AddThemeFontSizeOverride("font_size", 13);
            hBox.AddChild(aiBadge);

            var relationLabel = new Label
            {
                Text = isAtWar ? $"Chiến tranh ({warScore:+0;-0;0})" : "Hòa bình",
                CustomMinimumSize = new Vector2(115, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            relationLabel.AddThemeColorOverride("font_color", isAtWar ? new Color("#fb7185") : new Color("#94a3b8"));
            relationLabel.AddThemeFontSizeOverride("font_size", 12);
            hBox.AddChild(relationLabel);

            if (isAi)
            {
                var diplomacyButton = CreateStyledButton(isAtWar ? "Hòa ước" : "Tuyên chiến", () =>
                {
                    if (_session is null)
                    {
                        return;
                    }

                    var result = isAtWar ? _session.ConcludePeace(countryId) : _session.DeclareWar(countryId);
                    if (!result["success"].AsBool())
                    {
                        relationLabel.Text = result["message"].AsString();
                    }
                }, new Vector2(100, 34), isActive: isAtWar);
                hBox.AddChild(diplomacyButton);
            }

            row.AddChild(hBox);
            _ledgerListContainer.AddChild(row);
        }
    }

    // --- UI Construction ---

    private void BuildInterface()
    {
        BuildTopBar();
        BuildProvincePanel();
        BuildPlaceholderCard();
        BuildHoverTooltip();
        BuildLedgerModal();
        BuildNavigationHint();
    }

    private void UpdateWorldSummary(Godot.Collections.Dictionary summary)
    {
        int provinceCount = summary.ContainsKey("provinceCount") ? summary["provinceCount"].AsInt32() : 0;
        int countryCount = summary.ContainsKey("countryCount") ? summary["countryCount"].AsInt32() : 0;
        int armyCount = summary.ContainsKey("armyCount") ? summary["armyCount"].AsInt32() : 0;
        int warCount = summary.ContainsKey("warCount") ? summary["warCount"].AsInt32() : 0;
        string date = summary.ContainsKey("date") ? summary["date"].AsString() : string.Empty;
        double treasury = summary.ContainsKey("playerTreasury") ? summary["playerTreasury"].AsDouble() : 0d;
        double income = summary.ContainsKey("playerIncome") ? summary["playerIncome"].AsDouble() : 0d;
        int speed = summary.ContainsKey("speed") ? summary["speed"].AsInt32() : 0;

        _dateLabel.Text = string.IsNullOrEmpty(date) ? "Ngày chưa xác định" : $"Ngày {date}";
        _worldStatsLabel.Text = $"🗺️ {provinceCount} Tỉnh   │   👑 {countryCount} Vương Triều   │   ⚔️ {armyCount} Quân   │   🛡️ {warCount} Chiến tranh   │   💰 {treasury.ToString("N0", CultureInfo.InvariantCulture)} (+{income.ToString("N0", CultureInfo.InvariantCulture)}/ngày)";
        UpdateSpeedButtons(speed);
    }

    private void UpdateSpeedButtons(int speed)
    {
        for (var index = 0; index < _speedButtons.Length; index++)
        {
            var isActive = index == speed;
            var button = _speedButtons[index];
            var normalStyle = CreateStyleBox(
                bgColor: isActive ? new Color("#253549") : new Color("#151f2c"),
                borderColor: isActive ? new Color("#ffd166") : new Color("#334155"),
                borderWidth: 1,
                radius: 5,
                padH: 6,
                padV: 5);
            button.AddThemeStyleboxOverride("normal", normalStyle);
            button.AddThemeColorOverride("font_color", isActive ? new Color("#ffd166") : new Color("#f1f5f9"));
        }
    }

    private void BuildTopBar()
    {
        _topBar = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 54),
            MouseFilter = MouseFilterEnum.Stop
        };
        _topBar.SetAnchorsPreset(LayoutPreset.TopWide);
        _topBar.OffsetLeft = 14;
        _topBar.OffsetTop = 10;
        _topBar.OffsetRight = -14;
        _topBar.OffsetBottom = 64;

        var barStyle = CreateStyleBox(
            bgColor: new Color(0.05f, 0.08f, 0.12f, 0.94f),
            borderColor: new Color("#c89b3c"),
            borderWidth: 1,
            radius: 8,
            padH: 16,
            padV: 8
        );
        _topBar.AddThemeStyleboxOverride("panel", barStyle);

        var hBox = new HBoxContainer();
        hBox.AddThemeConstantOverride("separation", 24);

        // Title & Crest
        var titleBox = new HBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 8);

        var crest = new Label { Text = "⚔️" };
        crest.AddThemeFontSizeOverride("font_size", 18);
        titleBox.AddChild(crest);

        var title = new Label { Text = "LỤC HẢI" };
        title.AddThemeColorOverride("font_color", new Color("#ffd166"));
        title.AddThemeFontSizeOverride("font_size", 19);
        titleBox.AddChild(title);

        _dateLabel = new Label { Text = "Ngày 01/01/1444" };
        _dateLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _dateLabel.AddThemeFontSizeOverride("font_size", 12);
        titleBox.AddChild(_dateLabel);

        hBox.AddChild(titleBox);

        // Global stats (Center)
        _worldStatsLabel = new Label
        {
            Text = "Đang đồng bộ dữ liệu thế giới...",
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _worldStatsLabel.AddThemeColorOverride("font_color", new Color("#e2e8f0"));
        _worldStatsLabel.AddThemeFontSizeOverride("font_size", 14);
        hBox.AddChild(_worldStatsLabel);

        // Speed & Controls (Right)
        var controlsBox = new HBoxContainer();
        controlsBox.AddThemeConstantOverride("separation", 8);

        var ledgerBtn = CreateStyledButton("📜 Vương Triều", () =>
        {
            _ledgerModal.Visible = !_ledgerModal.Visible;
            if (_ledgerModal.Visible) PopulateLedger();
        });
        controlsBox.AddChild(ledgerBtn);

        _speedButtons = new Button[6];
        for (var speed = 0; speed < _speedButtons.Length; speed++)
        {
            var selectedSpeed = speed;
            var buttonText = speed == 0 ? "⏸" : $"{speed}x";
            var speedButton = CreateStyledButton(buttonText, () => _session?.SetGameSpeed(selectedSpeed), new Vector2(36, 32));
            _speedButtons[speed] = speedButton;
            controlsBox.AddChild(speedButton);
        }

        hBox.AddChild(controlsBox);

        _topBar.AddChild(hBox);
        AddChild(_topBar);
    }

    private void BuildProvincePanel()
    {
        _provincePanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(380, 560),
            MouseFilter = MouseFilterEnum.Stop
        };
        _provincePanel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _provincePanel.OffsetLeft = 16;
        _provincePanel.OffsetTop = 76;
        _provincePanel.OffsetRight = 396;
        _provincePanel.OffsetBottom = 760;
        _provincePanel.CustomMinimumSize = new Vector2(380, 680);

        var panelStyle = CreateStyleBox(
            bgColor: new Color(0.06f, 0.09f, 0.14f, 0.96f),
            borderColor: new Color("#c89b3c"),
            borderWidth: 1,
            radius: 10,
            padH: 20,
            padV: 18
        );
        _provincePanel.AddThemeStyleboxOverride("panel", panelStyle);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 14);

        // Header with Province Name & Close button
        var headerBox = new HBoxContainer();
        _provinceNameLabel = new Label
        {
            Text = "Tên Tỉnh",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _provinceNameLabel.AddThemeColorOverride("font_color", new Color("#ffd166"));
        _provinceNameLabel.AddThemeFontSizeOverride("font_size", 22);
        headerBox.AddChild(_provinceNameLabel);

        var closeBtn = CreateStyledButton("✕", () =>
        {
            _selectedProvinceId = -1;
            UpdateProvinceDisplay(-1);
        }, new Vector2(28, 28));
        headerBox.AddChild(closeBtn);
        vBox.AddChild(headerBox);

        // Badges row
        var badgeRow = new HBoxContainer();
        badgeRow.AddThemeConstantOverride("separation", 8);

        _terrainBadge = CreateBadge("Đồng Bằng", new Color("#22c55e"));
        badgeRow.AddChild(_terrainBadge);

        _coastalBadge = CreateBadge("Ven Biển", new Color("#38bdf8"));
        badgeRow.AddChild(_coastalBadge);

        vBox.AddChild(badgeRow);

        // Sovereign Banner
        var bannerBox = new PanelContainer();
        var bannerStyle = CreateStyleBox(new Color("#121b28"), new Color("#1e2d42"), 1, 6, 12, 10);
        bannerBox.AddThemeStyleboxOverride("panel", bannerStyle);

        var bannerHBox = new HBoxContainer();
        bannerHBox.AddThemeConstantOverride("separation", 10);

        _ownerColorBar = new ColorRect
        {
            CustomMinimumSize = new Vector2(6, 36),
            Color = new Color("#9E2A2B")
        };
        bannerHBox.AddChild(_ownerColorBar);

        var ownerInfoBox = new VBoxContainer();
        _ownerCountryLabel = new Label { Text = "Đại Việt" };
        _ownerCountryLabel.AddThemeColorOverride("font_color", new Color("#f8fafc"));
        _ownerCountryLabel.AddThemeFontSizeOverride("font_size", 16);
        ownerInfoBox.AddChild(_ownerCountryLabel);

        _controllerLabel = new Label { Text = "🛡️ Chủ quyền toàn vẹn" };
        _controllerLabel.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _controllerLabel.AddThemeFontSizeOverride("font_size", 12);
        ownerInfoBox.AddChild(_controllerLabel);

        bannerHBox.AddChild(ownerInfoBox);
        bannerBox.AddChild(bannerHBox);
        vBox.AddChild(bannerBox);

        // Separator
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 6);
        vBox.AddChild(sep);

        // Metrics Grid (2x2)
        var grid = new GridContainer
        {
            Columns = 2
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);

        _populationValue = AddMetricCard(grid, "👥 DÂN SỐ", "1,250,000", new Color("#e2e8f0"));
        _taxRateValue = AddMetricCard(grid, "🪙 THUẾ SUẤT", "12.0%", new Color("#fbbf24"));
        _economyValue = AddMetricCard(grid, "💰 KINH TẾ", "35.5", new Color("#38bdf8"));
        _manpowerValue = AddMetricCard(grid, "⚔️ NHÂN LỰC", "95,000", new Color("#ef4444"));

        vBox.AddChild(grid);

        // Development stat row
        var devBox = new HBoxContainer();
        devBox.AddThemeConstantOverride("separation", 8);
        var devTitle = new Label { Text = "📈 Trình độ phát triển:" };
        devTitle.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        devTitle.AddThemeFontSizeOverride("font_size", 13);
        devBox.AddChild(devTitle);

        _developmentValue = new Label { Text = "8.0" };
        _developmentValue.AddThemeColorOverride("font_color", new Color("#facc15"));
        _developmentValue.AddThemeFontSizeOverride("font_size", 13);
        devBox.AddChild(_developmentValue);
        vBox.AddChild(devBox);

        // Strategic Actions
        var actionHeader = new Label { Text = "CHỈ HUY CHIẾN LƯỢC" };
        actionHeader.AddThemeColorOverride("font_color", new Color("#c89b3c"));
        actionHeader.AddThemeFontSizeOverride("font_size", 12);
        vBox.AddChild(actionHeader);

        _actionsContainer = new VBoxContainer();
        _actionsContainer.AddThemeConstantOverride("separation", 6);

        var recruitBtn = CreateStyledButton("⚔️ Chiêu Mộ Binh Sĩ", () =>
        {
            if (_session is null || _selectedProvinceId <= 0)
            {
                return;
            }

            var result = _session.RecruitArmy(_selectedProvinceId, 1_000);
            _armyStatusLabel.Text = result["message"].AsString();
            if (result["success"].AsBool())
            {
                _selectedArmyId = result["armyId"].AsInt32();
                UpdateArmyStatus();
                UpdateArmyList(_selectedProvinceId);
            }
        });
        _actionsContainer.AddChild(recruitBtn);

        var buildBtn = CreateStyledButton("🏰 Xây Dựng Công Trình", () =>
        {
            GD.Print("Xây dựng công trình chưa có trong mốc hiện tại.");
        });
        _actionsContainer.AddChild(buildBtn);

        _armyStatusLabel = new Label
        {
            Text = "Chọn quân trên bản đồ hoặc trong danh sách để ra lệnh.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _armyStatusLabel.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        _armyStatusLabel.AddThemeFontSizeOverride("font_size", 12);
        _actionsContainer.AddChild(_armyStatusLabel);

        var moveArmyBtn = CreateStyledButton("🧭 Điều quân tới tỉnh đang chọn", () =>
        {
            if (_session is null || _selectedArmyId <= 0 || _selectedProvinceId <= 0)
            {
                _armyStatusLabel.Text = "Hãy chọn một đội quân và tỉnh đích trên bản đồ.";
                return;
            }

            var result = _session.MoveArmy(_selectedArmyId, _selectedProvinceId);
            _armyStatusLabel.Text = result["message"].AsString();
            if (result["success"].AsBool())
            {
                UpdateArmyStatus();
            }
        });
        _actionsContainer.AddChild(moveArmyBtn);

        _armyListContainer = new VBoxContainer();
        _armyListContainer.AddThemeConstantOverride("separation", 4);
        _actionsContainer.AddChild(_armyListContainer);

        vBox.AddChild(_actionsContainer);

        // Neighbors section
        var neighborHeader = new Label { Text = "VÙNG LÂN CẬN" };
        neighborHeader.AddThemeColorOverride("font_color", new Color("#c89b3c"));
        neighborHeader.AddThemeFontSizeOverride("font_size", 12);
        vBox.AddChild(neighborHeader);

        _neighborsContainer = new VBoxContainer();
        vBox.AddChild(_neighborsContainer);

        _provincePanel.AddChild(vBox);
        AddChild(_provincePanel);
    }

    private void BuildPlaceholderCard()
    {
        _placeholderCard = new PanelContainer
        {
            CustomMinimumSize = new Vector2(340, 70),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _placeholderCard.SetAnchorsPreset(LayoutPreset.TopLeft);
        _placeholderCard.OffsetLeft = 16;
        _placeholderCard.OffsetTop = 76;
        _placeholderCard.OffsetRight = 356;
        _placeholderCard.OffsetBottom = 146;

        var cardStyle = CreateStyleBox(new Color(0.06f, 0.09f, 0.14f, 0.82f), new Color("#334155"), 1, 8, 14, 12);
        _placeholderCard.AddThemeStyleboxOverride("panel", cardStyle);

        var label = new Label
        {
            Text = "🗺️ Nhấp chuột vào bất kỳ tỉnh thành nào\nđể xem thông tin chiến lược & chỉ huy.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        label.AddThemeFontSizeOverride("font_size", 13);
        _placeholderCard.AddChild(label);

        AddChild(_placeholderCard);
    }

    private void BuildHoverTooltip()
    {
        _hoverTooltip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(230, 85),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };

        var tooltipStyle = CreateStyleBox(
            bgColor: new Color(0.04f, 0.07f, 0.12f, 0.96f),
            borderColor: new Color("#c89b3c"),
            borderWidth: 1,
            radius: 6,
            padH: 12,
            padV: 10
        );
        _hoverTooltip.AddThemeStyleboxOverride("panel", tooltipStyle);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 4);

        // Province name & Color pill
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 8);

        _tooltipColorPill = new ColorRect
        {
            CustomMinimumSize = new Vector2(5, 18),
            Color = new Color("#9E2A2B")
        };
        topRow.AddChild(_tooltipColorPill);

        _tooltipName = new Label { Text = "Tên Tỉnh" };
        _tooltipName.AddThemeColorOverride("font_color", new Color("#ffd166"));
        _tooltipName.AddThemeFontSizeOverride("font_size", 15);
        topRow.AddChild(_tooltipName);

        vBox.AddChild(topRow);

        _tooltipOwner = new Label { Text = "Quốc gia sở hữu" };
        _tooltipOwner.AddThemeColorOverride("font_color", new Color("#cbd5e1"));
        _tooltipOwner.AddThemeFontSizeOverride("font_size", 13);
        vBox.AddChild(_tooltipOwner);

        _tooltipDetails = new Label { Text = "Địa hình • Dân số" };
        _tooltipDetails.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        _tooltipDetails.AddThemeFontSizeOverride("font_size", 11);
        vBox.AddChild(_tooltipDetails);

        _hoverTooltip.AddChild(vBox);
        AddChild(_hoverTooltip);
    }

    private void BuildLedgerModal()
    {
        _ledgerModal = new PanelContainer
        {
            CustomMinimumSize = new Vector2(850, 440),
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false
        };
        _ledgerModal.SetAnchorsPreset(LayoutPreset.Center);
        _ledgerModal.OffsetLeft = -425;
        _ledgerModal.OffsetTop = -220;
        _ledgerModal.OffsetRight = 425;
        _ledgerModal.OffsetBottom = 220;

        var modalStyle = CreateStyleBox(
            bgColor: new Color(0.05f, 0.08f, 0.13f, 0.98f),
            borderColor: new Color("#c89b3c"),
            borderWidth: 1.5f,
            radius: 12,
            padH: 24,
            padV: 20
        );
        _ledgerModal.AddThemeStyleboxOverride("panel", modalStyle);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 14);

        // Header
        var hBox = new HBoxContainer();
        var title = new Label
        {
            Text = "👑 CÁC VƯƠNG TRIỀU & THẾ LỰC TẠI LỤC HẢI",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeColorOverride("font_color", new Color("#ffd166"));
        title.AddThemeFontSizeOverride("font_size", 18);
        hBox.AddChild(title);

        var closeBtn = CreateStyledButton("✕", () => _ledgerModal.Visible = false, new Vector2(30, 30));
        hBox.AddChild(closeBtn);
        vBox.AddChild(hBox);

        // Separator
        vBox.AddChild(new HSeparator());

        // Scroll list
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        _ledgerListContainer = new VBoxContainer();
        _ledgerListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _ledgerListContainer.AddThemeConstantOverride("separation", 8);

        scroll.AddChild(_ledgerListContainer);
        vBox.AddChild(scroll);

        _ledgerModal.AddChild(vBox);
        AddChild(_ledgerModal);
    }

    private void BuildNavigationHint()
    {
        var hintPanel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        hintPanel.SetAnchorsPreset(LayoutPreset.BottomRight);
        hintPanel.OffsetLeft = -520;
        hintPanel.OffsetTop = -42;
        hintPanel.OffsetRight = -16;
        hintPanel.OffsetBottom = -10;

        var hintStyle = CreateStyleBox(new Color(0.05f, 0.08f, 0.12f, 0.80f), new Color("#334155"), 1, 6, 14, 6);
        hintPanel.AddThemeStyleboxOverride("panel", hintStyle);

        var label = new Label
        {
            Text = "WASD / Chuột giữa: Di chuyển   │   Lăn chuột: Phóng to/Thu nhỏ   │   Chuột trái: Chọn tỉnh"
        };
        label.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        label.AddThemeFontSizeOverride("font_size", 12);
        hintPanel.AddChild(label);

        AddChild(hintPanel);
    }

    // --- Helper Styling Methods ---

    private static Label AddMetricCard(GridContainer grid, string label, string defaultValue, Color valColor)
    {
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var cardStyle = CreateStyleBox(new Color("#121a26"), new Color("#1e293b"), 1, 6, 10, 8);
        card.AddThemeStyleboxOverride("panel", cardStyle);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 2);

        var title = new Label { Text = label };
        title.AddThemeColorOverride("font_color", new Color("#94a3b8"));
        title.AddThemeFontSizeOverride("font_size", 11);
        vBox.AddChild(title);

        var val = new Label { Text = defaultValue };
        val.AddThemeColorOverride("font_color", valColor);
        val.AddThemeFontSizeOverride("font_size", 15);
        vBox.AddChild(val);

        card.AddChild(vBox);
        grid.AddChild(card);

        return val;
    }

    private static Label CreateBadge(string text, Color color)
    {
        var badge = new Label
        {
            Text = text
        };
        badge.AddThemeFontSizeOverride("font_size", 12);
        return badge;
    }

    private static Button CreateStyledButton(string text, Action? onClick, Vector2? minSize = null, bool isActive = false)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = minSize ?? new Vector2(0, 34)
        };

        var normalStyle = CreateStyleBox(
            bgColor: isActive ? new Color("#253549") : new Color("#151f2c"),
            borderColor: isActive ? new Color("#ffd166") : new Color("#334155"),
            borderWidth: 1,
            radius: 5,
            padH: 12,
            padV: 6
        );

        var hoverStyle = CreateStyleBox(
            bgColor: new Color("#1f2e42"),
            borderColor: new Color("#ffd166"),
            borderWidth: 1,
            radius: 5,
            padH: 12,
            padV: 6
        );

        btn.AddThemeStyleboxOverride("normal", normalStyle);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.AddThemeColorOverride("font_color", isActive ? new Color("#ffd166") : new Color("#f1f5f9"));
        btn.AddThemeColorOverride("font_hover_color", new Color("#ffd166"));
        btn.AddThemeFontSizeOverride("font_size", 13);

        if (onClick != null)
        {
            btn.Pressed += onClick;
        }

        return btn;
    }

    private static StyleBoxFlat CreateStyleBox(
        Color bgColor,
        Color borderColor,
        float borderWidth = 1f,
        int radius = 6,
        int padH = 8,
        int padV = 6)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            ContentMarginLeft = padH,
            ContentMarginRight = padH,
            ContentMarginTop = padV,
            ContentMarginBottom = padV
        };

        if (borderWidth > 0)
        {
            style.SetBorderWidthAll((int)borderWidth);
        }

        return style;
    }

    private static string GetTerrainIcon(string terrain) => terrain switch
    {
        "Plains" => "🌾",
        "Highlands" => "🏔️",
        "Forest" => "🌲",
        "Marsh" => "🌿",
        "Coast" => "🌊",
        _ => "🗺️"
    };

    private static string TranslateTerrain(string terrain) => terrain switch
    {
        "Plains" => "Đồng Bằng",
        "Highlands" => "Cao Nguyên",
        "Forest" => "Rừng Rậm",
        "Marsh" => "Đầm Lầy",
        "Coast" => "Vùng Ven Biển",
        _ => terrain
    };

    private static Color GetTerrainColor(string terrain) => terrain switch
    {
        "Plains" => new Color("#4ade80"),
        "Highlands" => new Color("#fb923c"),
        "Forest" => new Color("#22c55e"),
        "Marsh" => new Color("#a3e635"),
        "Coast" => new Color("#38bdf8"),
        _ => new Color("#e2e8f0")
    };
}
