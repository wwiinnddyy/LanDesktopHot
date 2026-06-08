using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LanDesktopHot.Models;
using LanDesktopHot.Services;
using LanMountainDesktop.AirAppSdk;

namespace LanDesktopHot.Widgets;

public partial class ZhihuHotListWidget : AirAppWidgetBase
{
    private ZhihuHotListService? _dataService;

    private readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private DispatcherTimer? _refreshTimer;

    private ComponentState _currentState = ComponentState.Loading;
    private List<ZhihuHotItem> _currentItems = [];
    private bool _isDarkMode;

    private bool _isDesignMode;

    private static class ThemeColors
    {
        public static readonly Color LightCardBackground = Color.Parse("#FCFBFA");
        public static readonly Color LightCardBorder = Color.Parse("#E8E8E8");
        public static readonly Color LightText = Color.Parse("#2B2F35");
        public static readonly Color LightTextSecondary = Color.Parse("#7A8088");
        public static readonly Color LightIconBadgeBackground = Color.Parse("#140A66FF");
        public static readonly Color LightIconBadgeBorder = Color.Parse("#200A66FF");
        public static readonly Color LightRefreshButtonBackground = Color.Parse("#14A0A6AF");

        public static readonly Color DarkCardBackground = Color.Parse("#1B2129");
        public static readonly Color DarkCardBorder = Color.Parse("#2D3440");
        public static readonly Color DarkText = Color.Parse("#E8EAED");
        public static readonly Color DarkTextSecondary = Color.Parse("#A8B1C2");
        public static readonly Color DarkIconBadgeBackground = Color.Parse("#2D3440");
        public static readonly Color DarkIconBadgeBorder = Color.Parse("#3D4450");
        public static readonly Color DarkRefreshButtonBackground = Color.Parse("#2D3440");

        public static readonly Color Primary = Color.Parse("#0A66FF");
        public static readonly Color Accent = Color.Parse("#FF6B35");
        public static readonly Color Top3 = Color.Parse("#FF6B35");
        public static readonly Color RankNormal = Color.Parse("#7A8088");
    }

    public ZhihuHotListWidget()
    {
        InitializeComponent();

        _isDesignMode = Design.IsDesignMode;
        if (_isDesignMode)
        {
            SetupDesignTimePreview();
        }
    }

    public ZhihuHotListWidget(ZhihuHotListService dataService) : this()
    {
        _dataService = dataService;

        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        _isDarkMode = ResolveIsDarkMode();

        SetState(ComponentState.Loading);

        SizeChanged += OnSizeChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;

        RefreshButton.Click += async (_, _) => await RefreshAsync();
    }

    protected override void OnAttachedCore()
    {
        if (_isDesignMode) return;

        _isDarkMode = ResolveIsDarkMode();
        ApplyTheme();
        _ = RefreshAsync();
        _refreshTimer?.Start();
    }

    protected override void OnDetachedCore()
    {
        if (_isDesignMode) return;

        _refreshTimer?.Stop();
        _cancellationTokenSource?.Cancel();
    }

    protected override void OnAppearanceChangedCore(AirAppAppearanceSnapshot snapshot)
    {
        var newIsDarkMode = snapshot.IsDarkMode;
        if (_isDarkMode != newIsDarkMode)
        {
            _isDarkMode = newIsDarkMode;
            Dispatcher.UIThread.Post(() =>
            {
                ApplyTheme();
                ApplyScale();
                UpdateHotListPanel();
            });
        }
    }

    private void SetupDesignTimePreview()
    {
        Width = 300;
        Height = 400;

        _isDarkMode = false;
        ApplyTheme();

        _currentItems =
        [
            new ZhihuHotItem
            {
                Rank = 1,
                Title = "如何看待2026年人工智能的发展趋势？",
                Description = "AI技术正在快速发展",
                Url = "https://www.zhihu.com/question/1",
                QuestionId = 1
            },
            new ZhihuHotItem
            {
                Rank = 2,
                Title = "如何评价新发布的操作系统？",
                HotMetrics = "1000 万热度",
                Url = "https://www.zhihu.com/question/2",
                QuestionId = 2
            },
            new ZhihuHotItem
            {
                Rank = 3,
                Title = "五一假期有哪些值得去的地方？",
                HotMetrics = "800 万热度",
                Url = "https://www.zhihu.com/question/3",
                QuestionId = 3
            }
        ];

        SetState(ComponentState.Normal);
        UpdateHotListPanel();
    }

    private bool ResolveIsDarkMode()
    {
        if (_isDesignMode || _context is null)
        {
            return false;
        }

        var themeVariant = _context.Appearance.Snapshot.ThemeVariant;
        if (string.Equals(themeVariant, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(themeVariant, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            return true;
        }

        if (ActualThemeVariant == ThemeVariant.Light)
        {
            return false;
        }

        return Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_isDesignMode) return;

        var newIsDarkMode = ResolveIsDarkMode();
        if (_isDarkMode != newIsDarkMode)
        {
            _isDarkMode = newIsDarkMode;
            ApplyTheme();
            UpdateHotListPanel();
        }
    }

    private void ApplyTheme()
    {
        if (_isDesignMode) return;

        var cornerRadius = 12.0;
        RootBorder.CornerRadius = new CornerRadius(cornerRadius);
        CardBackground.CornerRadius = new CornerRadius(cornerRadius);
        CardBorder.CornerRadius = new CornerRadius(cornerRadius);

        if (_isDarkMode)
        {
            ApplyDarkTheme(cornerRadius);
        }
        else
        {
            ApplyLightTheme(cornerRadius);
        }
    }

    private void ApplyLightTheme(double cornerRadius)
    {
        CardBorder.Background = new SolidColorBrush(ThemeColors.LightCardBackground);
        CardBorder.BorderBrush = new SolidColorBrush(ThemeColors.LightCardBorder);

        HeaderIconBadge.Background = new SolidColorBrush(ThemeColors.LightIconBadgeBackground);
        HeaderIconBadge.BorderBrush = new SolidColorBrush(ThemeColors.LightIconBadgeBorder);
        HeaderIcon.Foreground = new SolidColorBrush(ThemeColors.Primary);

        HeaderText.Foreground = new SolidColorBrush(ThemeColors.LightText);
        UpdateTimeText.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);

        RefreshButton.Background = new SolidColorBrush(ThemeColors.LightRefreshButtonBackground);
        RefreshIcon.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);

        LoadingHost.Background = new SolidColorBrush(Color.Parse("#140A66FF"));
        LoadingHost.CornerRadius = new CornerRadius(cornerRadius);
        LoadingText.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);

        ErrorHost.Background = new SolidColorBrush(Color.Parse("#14FF6B35"));
        ErrorHost.BorderBrush = new SolidColorBrush(Color.Parse("#30FF6B35"));
        ErrorHost.CornerRadius = new CornerRadius(cornerRadius);
        ErrorIcon.Foreground = new SolidColorBrush(Color.Parse("#FF6B35"));
        ErrorText.Foreground = new SolidColorBrush(ThemeColors.LightText);

        EmptyHost.Background = new SolidColorBrush(Color.Parse("#0C000000"));
        EmptyHost.CornerRadius = new CornerRadius(cornerRadius);
        EmptyText.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);
    }

    private void ApplyDarkTheme(double cornerRadius)
    {
        CardBorder.Background = new SolidColorBrush(ThemeColors.DarkCardBackground);
        CardBorder.BorderBrush = new SolidColorBrush(ThemeColors.DarkCardBorder);

        HeaderIconBadge.Background = new SolidColorBrush(ThemeColors.DarkIconBadgeBackground);
        HeaderIconBadge.BorderBrush = new SolidColorBrush(ThemeColors.DarkIconBadgeBorder);
        HeaderIcon.Foreground = new SolidColorBrush(ThemeColors.Primary);

        HeaderText.Foreground = new SolidColorBrush(ThemeColors.DarkText);
        UpdateTimeText.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);

        RefreshButton.Background = new SolidColorBrush(ThemeColors.DarkRefreshButtonBackground);
        RefreshIcon.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);

        LoadingHost.Background = new SolidColorBrush(Color.Parse("#200A66FF"));
        LoadingHost.CornerRadius = new CornerRadius(cornerRadius);
        LoadingText.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);

        ErrorHost.Background = new SolidColorBrush(Color.Parse("#20FF6B35"));
        ErrorHost.BorderBrush = new SolidColorBrush(Color.Parse("#30FF6B35"));
        ErrorHost.CornerRadius = new CornerRadius(cornerRadius);
        ErrorIcon.Foreground = new SolidColorBrush(Color.Parse("#FF6B35"));
        ErrorText.Foreground = new SolidColorBrush(ThemeColors.DarkText);

        EmptyHost.Background = new SolidColorBrush(Color.Parse("#1A1A1A1A"));
        EmptyHost.CornerRadius = new CornerRadius(cornerRadius);
        EmptyText.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDesignMode) return;

        SubscribeToPluginBus();
        _isDarkMode = ResolveIsDarkMode();
        ApplyTheme();
        _ = RefreshAsync();
        _refreshTimer?.Start();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDesignMode) return;

        _refreshTimer?.Stop();
        _cancellationTokenSource?.Cancel();

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();

        if (_context is not null)
        {
            _context.Appearance.Changed -= OnAppearanceChanged;
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyScale();
    }

    private void OnAppearanceChanged(object? sender, AppearanceChangedEvent e)
    {
        if (_context is null) return;

        var shouldRefresh = e.ChangedProperties.Count == 0 ||
            e.CornerRadiusChanged ||
            e.ThemeVariantChanged;

        if (shouldRefresh)
        {
            var newIsDarkMode = ResolveIsDarkMode();
            if (_isDarkMode != newIsDarkMode)
            {
                _isDarkMode = newIsDarkMode;
            }

            Dispatcher.UIThread.Post(() =>
            {
                ApplyTheme();
                ApplyScale();
                UpdateHotListPanel();
            });
        }
    }

    private void SubscribeToPluginBus()
    {
        if (_messageBus is null || _subscriptions.Count > 0)
        {
            return;
        }

        _subscriptions.Add(_messageBus.Subscribe<ZhihuDataRefreshMessage>(_ =>
            Dispatcher.UIThread.Post(async () => await RefreshAsync())));
    }

    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (_dataService is null) return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            SetState(ComponentState.Loading);

            var items = await _dataService.FetchAsync(_cancellationTokenSource.Token);

            if (items.Count == 0)
            {
                SetState(ComponentState.Empty);
                return;
            }

            _currentItems = items;
            UpdateTimeText.Text = string.Format(
                T("status.updated_at", "更新于 {0:HH:mm}"), DateTime.Now);
            SetState(ComponentState.Normal);
            UpdateHotListPanel();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            SetState(ComponentState.Error, T("error.network", "网络连接失败"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZhihuHotListWidget] Load failed: {ex.Message}");
            SetState(ComponentState.Error, T("error.unknown", "获取失败"));
        }
    }

    private void SetState(ComponentState state, string? errorMessage = null)
    {
        _currentState = state;

        Dispatcher.UIThread.Post(() =>
        {
            HotListScrollViewer.IsVisible = false;
            LoadingHost.IsVisible = false;
            ErrorHost.IsVisible = false;
            EmptyHost.IsVisible = false;

            switch (state)
            {
                case ComponentState.Loading:
                    LoadingHost.IsVisible = true;
                    LoadingText.Text = T("status.loading", "正在加载...");
                    HeaderText.Text = T("header.title", "知乎热榜");
                    UpdateTimeText.Text = T("status.loading_short", "加载中...");
                    break;

                case ComponentState.Normal:
                    HotListScrollViewer.IsVisible = true;
                    HeaderText.Text = T("header.title", "知乎热榜");
                    break;

                case ComponentState.Error:
                    ErrorHost.IsVisible = true;
                    ErrorText.Text = errorMessage ?? T("error.network", "网络错误");
                    HeaderText.Text = T("header.title", "知乎热榜");
                    UpdateTimeText.Text = "";
                    break;

                case ComponentState.Empty:
                    EmptyHost.IsVisible = true;
                    EmptyText.Text = T("status.empty", "暂无热榜数据");
                    HeaderText.Text = T("header.title", "知乎热榜");
                    UpdateTimeText.Text = "";
                    break;
            }

            ApplyScale();
        });
    }

    private void UpdateHotListPanel()
    {
        Dispatcher.UIThread.Post(() =>
        {
            HotListStackPanel.Children.Clear();

            var basis = GetLayoutBasis();
            var titleSize = Math.Clamp(basis * 0.042, 12, 16);
            var detailSize = Math.Clamp(basis * 0.032, 9, 13);
            var rankSize = Math.Clamp(basis * 0.045, 14, 18);

            foreach (var item in _currentItems)
            {
                var itemCard = CreateHotItemCard(item, titleSize, detailSize, rankSize, basis);
                HotListStackPanel.Children.Add(itemCard);
            }
        });
    }

    private Border CreateHotItemCard(ZhihuHotItem item, double titleSize, double detailSize, double rankSize, double basis)
    {
        var cornerRadius = _isDesignMode ? 10d : 14d;
        var cardCornerRadius = cornerRadius;

        var surfaceColor = _isDarkMode ? Color.Parse("#252B33") : Color.Parse("#F8F8F8");
        var textColor = _isDarkMode ? ThemeColors.DarkText : ThemeColors.LightText;
        var textSecondaryColor = _isDarkMode ? ThemeColors.DarkTextSecondary : ThemeColors.LightTextSecondary;
        var borderColor = _isDarkMode ? Color.Parse("#3D4450") : Color.Parse("#E8E8E8");

        var isTop3 = item.Rank <= 3;
        var rankBadgeColor = isTop3 ? ThemeColors.Top3 : ThemeColors.RankNormal;
        var rankBadgeBg = isTop3
            ? Color.Parse("#20FF6B35")
            : Color.Parse(_isDarkMode ? "#3D4450" : "#E8E8E8");
        var rankBadgeBorder = isTop3
            ? Color.Parse("#40FF6B35")
            : Color.Parse(_isDarkMode ? "#3D4450" : "#E8E8E8");

        var rankBadge = new Border
        {
            Width = Math.Clamp(basis * 0.065, 28, 36),
            Height = Math.Clamp(basis * 0.065, 28, 36),
            CornerRadius = new CornerRadius(cardCornerRadius * 0.5),
            Background = new SolidColorBrush(rankBadgeBg),
            BorderBrush = new SolidColorBrush(rankBadgeBorder),
            BorderThickness = new Thickness(isTop3 ? 1 : 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = item.Rank.ToString(),
                FontSize = rankSize,
                FontWeight = isTop3 ? FontWeight.Bold : FontWeight.Medium,
                Foreground = new SolidColorBrush(rankBadgeColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var infoPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        infoPanel.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = titleSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(textColor),
            TextWrapping = TextWrapping.NoWrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(item.HotMetrics))
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = item.HotMetrics,
                FontSize = detailSize,
                Foreground = new SolidColorBrush(textSecondaryColor),
                TextWrapping = TextWrapping.NoWrap,
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            });
        }

        var hotIcon = new FontIcon
        {
            Glyph = isTop3 ? "" : "",
            FontSize = detailSize * 1.1,
            Foreground = new SolidColorBrush(isTop3 ? ThemeColors.Top3 : textSecondaryColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };

        contentGrid.Children.Add(rankBadge);
        contentGrid.Children.Add(infoPanel);
        contentGrid.Children.Add(hotIcon);
        Grid.SetColumn(infoPanel, 1);
        Grid.SetColumn(hotIcon, 2);

        var card = new Border
        {
            Background = new SolidColorBrush(surfaceColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cardCornerRadius),
            Padding = new Thickness(10, 8),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = contentGrid,
            Tag = item.Url
        };

        card.PointerPressed += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ZhihuHotListWidget] Failed to open URL: {ex.Message}");
                }
            }
        };

        return card;
    }

    private void ApplyScale()
    {
        var basis = GetLayoutBasis();
        var cornerRadius = _isDesignMode ? 24d : 24d;
        var smRadius = _isDesignMode ? 10d : 14d;

        RootBorder.CornerRadius = new CornerRadius(cornerRadius);
        CardBackground.CornerRadius = new CornerRadius(cornerRadius);
        CardBorder.CornerRadius = new CornerRadius(cornerRadius);

        var padding = Math.Clamp(basis * 0.035, 10, 18);
        CardBorder.Padding = new Thickness(padding, padding * 0.875, padding, padding * 0.875);

        HeaderGrid.ColumnSpacing = Math.Clamp(basis * 0.025, 8, 14);
        HeaderGrid.Margin = new Thickness(0, 0, 0, Math.Clamp(basis * 0.03, 8, 14));

        var iconBadgeSize = Math.Clamp(basis * 0.08, 32, 42);
        HeaderIconBadge.Width = iconBadgeSize;
        HeaderIconBadge.Height = iconBadgeSize;
        HeaderIconBadge.CornerRadius = new CornerRadius(iconBadgeSize * 0.28);
        HeaderIcon.FontSize = Math.Clamp(basis * 0.04, 14, 20);

        HeaderStack.Spacing = Math.Clamp(basis * 0.005, 1, 4);
        HeaderText.FontSize = Math.Clamp(basis * 0.045, 14, 20);
        UpdateTimeText.FontSize = Math.Clamp(basis * 0.032, 11, 15);

        var refreshButtonSize = Math.Clamp(basis * 0.08, 30, 40);
        RefreshButton.Width = refreshButtonSize;
        RefreshButton.Height = refreshButtonSize;
        RefreshButton.CornerRadius = new CornerRadius(refreshButtonSize / 2);
        RefreshIcon.FontSize = Math.Clamp(basis * 0.035, 13, 18);

        HotListStackPanel.Spacing = Math.Clamp(basis * 0.015, 4, 8);

        LoadingHost.CornerRadius = new CornerRadius(smRadius);
        ErrorHost.CornerRadius = new CornerRadius(smRadius);
        EmptyHost.CornerRadius = new CornerRadius(smRadius);

        if (_currentState == ComponentState.Normal)
        {
            UpdateHotListPanel();
        }
    }

    private double GetLayoutBasis()
    {
        if (_isDesignMode)
        {
            return Math.Min(Bounds.Width, Bounds.Height);
        }

        var cellSize = 100.0;
        var width = Bounds.Width > 1 ? Bounds.Width : cellSize * 4;
        var height = Bounds.Height > 1 ? Bounds.Height : cellSize * 4;
        return Math.Max(cellSize * 4, Math.Min(width, height));
    }

    private string T(string key, string fallback)
    {
        return fallback;
    }

    private string T(string key, string fallback, params object[] args)
    {
        return string.Format(fallback, args);
    }
}

public enum ComponentState
{
    Loading,
    Normal,
    Error,
    Empty
}