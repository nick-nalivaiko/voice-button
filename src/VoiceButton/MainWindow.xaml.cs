using System.Linq;
using System.Net.Http;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAutomationProperties = System.Windows.Automation.AutomationProperties;
using VoiceButton.Models;
using VoiceButton.Services;

namespace VoiceButton;

public partial class MainWindow : Window
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupAppName = "Voice Button";
    private const string SpeakLatestHotkeyId = "SpeakLatest";
    private const string ClipboardHotkeyId = "Clipboard";
    private const string CodexMicHotkeyId = "CodexMic";

    private readonly VoiceButtonSettings _settings = new();
    private readonly AppSettingsStore _appSettingsStore = new();
    private readonly AppSettings _appSettings;
    private readonly ClipboardService _clipboardService = new();
    private readonly CodexCopyService _codexCopyService;
    private readonly CodexMicrophoneService _codexMicrophoneService;
    private readonly OpenAiSpeechClient _speechClient;
    private readonly OpenAiCredentialStore _credentialStore = new();
    private readonly AudioPlaybackService _audioPlaybackService;
    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly DiagnosticsLogService _diagnosticsLog = new();

    private CancellationTokenSource? _currentRun;
    private TrayIconService? _trayIconService;
    private FloatingButtonWindow? _floatingButtonWindow;
    private bool _exitRequested;
    private bool _isInitializing = true;
    private bool _isRefreshingVoiceOptions;
    private string _currentPage = "General";
    private string? _capturingHotkeyId;

    public MainWindow()
    {
        InitializeComponent();

        _appSettings = _appSettingsStore.Load();
        if (!AppStorage.IsPortable)
        {
            EnvFile.LoadNearest();
        }

        InitializeApiKeyStorage();

        var codexWindowFinder = new CodexWindowFinder(_appSettings);
        _codexCopyService = new CodexCopyService(codexWindowFinder, _clipboardService, _appSettings);
        _codexMicrophoneService = new CodexMicrophoneService(codexWindowFinder, _appSettings);
        _speechClient = new OpenAiSpeechClient(new HttpClient());
        _audioPlaybackService = new AudioPlaybackService(Dispatcher);
        _audioPlaybackService.PlaybackChanged += AudioPlaybackService_PlaybackChanged;

        _appSettings.InterfaceLanguage = NormalizeInterfaceLanguage(_appSettings.InterfaceLanguage);
        _appSettings.SpeechModel = NormalizeSpeechModel(_appSettings.SpeechModel);
        _settings.Model = _appSettings.SpeechModel;
        _settings.Voice = string.IsNullOrWhiteSpace(_appSettings.Voice) ? _settings.Voice : _appSettings.Voice;
        _settings.Speed = NormalizeSpeechSpeed(_appSettings.SpeechSpeed);

        ModelComboBox.ItemsSource = SpeechModelOption.All;
        ModelComboBox.DisplayMemberPath = nameof(SpeechModelOption.Label);
        ModelComboBox.SelectedValuePath = nameof(SpeechModelOption.Id);
        ModelComboBox.SelectedValue = _settings.Model;

        VoiceComboBox.DisplayMemberPath = nameof(VoiceOption.Label);
        VoiceComboBox.SelectedValuePath = nameof(VoiceOption.Id);
        RefreshVoiceOptionsForModel();

        InterfaceLanguageComboBox.ItemsSource = InterfaceLanguageOption.All;
        InterfaceLanguageComboBox.DisplayMemberPath = nameof(InterfaceLanguageOption.Label);
        InterfaceLanguageComboBox.SelectedValuePath = nameof(InterfaceLanguageOption.Id);
        InterfaceLanguageComboBox.SelectedValue = _appSettings.InterfaceLanguage;
        SpeedSlider.Value = _settings.Speed;
        SpeedValueText.Text = FormatSpeechSpeed(_settings.Speed);

        FloatingButtonToggle.IsChecked = _appSettings.ShowFloatingButton;
        MinimizeToTrayToggle.IsChecked = _appSettings.MinimizeToTray;
        RememberFloatingButtonPositionToggle.IsChecked = _appSettings.RememberFloatingButtonPosition;
        HidePathsToggle.IsChecked = _appSettings.HideFilePathsInSpeech;
        HideCodeBlocksToggle.IsChecked = _appSettings.HideCodeBlocksInSpeech;
        HideInlineCodeToggle.IsChecked = _appSettings.HideInlineCodeInSpeech;
        ShortenLinksToggle.IsChecked = _appSettings.ShortenLinksInSpeech;
        HideSecretsToggle.IsChecked = _appSettings.HideSecretsInSpeech;
        ShortenHashesToggle.IsChecked = _appSettings.ShortenHashesInSpeech;
        CollapseStackTracesToggle.IsChecked = _appSettings.CollapseStackTracesInSpeech;
        RemoveMarkdownNoiseToggle.IsChecked = _appSettings.RemoveMarkdownNoiseInSpeech;
        CollapseTablesToggle.IsChecked = _appSettings.CollapseTablesInSpeech;
        CollapseStructuredDataToggle.IsChecked = _appSettings.CollapseStructuredDataInSpeech;
        ShortenShellCommandsToggle.IsChecked = _appSettings.ShortenShellCommandsInSpeech;
        HideLongNumbersToggle.IsChecked = _appSettings.HideLongNumbersInSpeech;
        CodexWindowKeywordsBox.Text = NormalizeCodexWindowKeywords(_appSettings.CodexWindowKeywords);
        HoverCopyButtonToggle.IsChecked = _appSettings.HoverToRevealCopyButton;
        RestoreClipboardToggle.IsChecked = _appSettings.RestoreClipboardAfterCopy;
        ClipboardFallbackToggle.IsChecked = _appSettings.FallbackToClipboardWhenCopyMissing;
        RetryMicrophoneToggle.IsChecked = _appSettings.RetryMicrophoneIfInactive;
        _appSettings.StartWithWindows = IsStartWithWindowsEnabled();
        StartWithWindowsToggle.IsChecked = _appSettings.StartWithWindows;
        _isInitializing = false;

        ApplyInterfaceLanguage();
        ShowSettingsPage("General");
        RefreshApiKeyStatus();
        SetReady();
    }

    private void InitializeApiKeyStorage()
    {
        var storedKey = _credentialStore.Read();
        if (!string.IsNullOrWhiteSpace(storedKey)
            && !storedKey.Contains("PASTE_YOUR_OPENAI_API_KEY_HERE", StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", storedKey.Trim());
            EnvFile.DeletePortableFiles();
            return;
        }

        var environmentKey = OpenAiSpeechClient.GetApiKey();
        if (environmentKey is null)
        {
            return;
        }

        _credentialStore.Save(environmentKey);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", environmentKey);
        EnvFile.DeletePortableFiles();
    }
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _trayIconService = new TrayIconService(
            ShowFromTray,
            () => _ = SpeakLatestAnswerAsync(),
            StopCurrentRun,
            ExitApplication);

        ShowFloatingButton();

        _hotkeyService.Pressed += (_, actionId) => RunHotkeyAction(actionId);
        RegisterConfiguredHotkeys();
    }

    private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var nextOffset = scrollViewer.VerticalOffset - (e.Delta * 0.24);
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(nextOffset, 0, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exitRequested)
        {
            _floatingButtonWindow?.Close();
            _hotkeyService.Dispose();
            _trayIconService?.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SpeakButton_Click(object sender, RoutedEventArgs e)
    {
        _ = SpeakLatestAnswerAsync();
    }

    private void ClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        _ = SpeakClipboardAsync();
    }

    private void PreviewVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        _ = PreviewVoiceAsync();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopCurrentRun();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_appSettings.MinimizeToTray)
        {
            Hide();
            return;
        }

        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_appSettings.MinimizeToTray)
        {
            Hide();
            return;
        }

        ExitApplication();
    }

    private async void CheckApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshApiKeyStatus();
        if (!OpenAiSpeechClient.HasUsableApiKey())
        {
            SetStatus(Tr("KeyMissing"), Tr("KeyMissingDetail"), "#F9C74F", busy: false);
            return;
        }

        try
        {
            SetStatus(Tr("CheckingKey"), Tr("CheckingKeyDetail"), "#37D0F4", busy: true);
            var result = await _speechClient.ValidateApiKeyAsync(CancellationToken.None);
            _diagnosticsLog.Info("OpenAI key check", result.Detail);
            if (result.IsValid)
            {
                SetStatus(Tr("KeyConnected"), result.Detail, "#41D6A1", busy: false);
                return;
            }

            SetStatus(Tr("KeyInvalid"), result.Detail, "#F25F5C", busy: false);
        }
        catch (Exception ex)
        {
            _diagnosticsLog.Error("OpenAI key check", ex);
            SetStatus(Tr("Error"), ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = OpenAiApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetStatus(Tr("KeyMissing"), Tr("PasteKeyFirst"), "#F9C74F", busy: false);
            return;
        }

        try
        {
            _credentialStore.Save(apiKey);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", apiKey);
            EnvFile.DeletePortableFiles();
            OpenAiApiKeyBox.Password = string.Empty;
            RefreshApiKeyStatus();
            SetStatus(Tr("KeySaved"), Tr("KeySavedDetail"), "#41D6A1", busy: false);
        }
        catch (Exception ex)
        {
            SetStatus(Tr("Error"), ex.Message, "#F25F5C", busy: false);
        }
    }
    private void SidebarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page })
        {
            ShowSettingsPage(page);
        }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (_capturingHotkeyId is null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _capturingHotkeyId = null;
            UpdateHotkeyButtons();
            SetStatus("Запись отменена", "Сочетание не изменено.", "#F9C74F", busy: false);
            return;
        }

        if (key is Key.Delete or Key.Back)
        {
            ClearHotkey(_capturingHotkeyId);
            _capturingHotkeyId = null;
            UpdateHotkeyButtons();
            RegisterConfiguredHotkeys();
            return;
        }

        var gesture = HotkeyGesture.FromKeyEvent(e);
        if (gesture is null)
        {
            SetStatus("Нужен хоткей", "Зажмите Ctrl, Alt, Shift или Win вместе с обычной клавишей.", "#F9C74F", busy: false);
            return;
        }

        if (IsDuplicateHotkey(_capturingHotkeyId, gesture.StorageValue))
        {
            SetStatus("Хоткей занят", "Это сочетание уже назначено другому действию Voice Button.", "#F9C74F", busy: false);
            return;
        }

        SetHotkeyValue(_capturingHotkeyId, gesture.StorageValue);
        _appSettingsStore.Save(_appSettings);
        _capturingHotkeyId = null;
        UpdateHotkeyButtons();
        RegisterConfiguredHotkeys();
        SetStatus("Хоткей сохранен", gesture.DisplayText, "#41D6A1", busy: false);
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string hotkeyId })
        {
            return;
        }

        _capturingHotkeyId = hotkeyId;
        UpdateHotkeyButtons();
        GetHotkeyButton(hotkeyId).Content = Tr("PressHotkeyButton");
        GetHotkeyButton(hotkeyId).Focus();
        SetStatus("Запись хоткея", "Нажмите новое сочетание. Esc отменяет, Delete очищает.", "#37D0F4", busy: false);
    }

    private void ClearHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string hotkeyId })
        {
            return;
        }

        ClearHotkey(hotkeyId);
        _appSettingsStore.Save(_appSettings);
        UpdateHotkeyButtons();
        RegisterConfiguredHotkeys();
        SetStatus("Хоткей обновлен", GetHotkeyDisplay(GetHotkeyValue(hotkeyId)), "#41D6A1", busy: false);
    }

    private void ClearHotkey(string hotkeyId)
    {
        SetHotkeyValue(hotkeyId, hotkeyId == SpeakLatestHotkeyId ? "Ctrl+Alt+V" : string.Empty);
    }

    private void RunHotkeyAction(string actionId)
    {
        switch (actionId)
        {
            case SpeakLatestHotkeyId:
                _ = SpeakLatestAnswerAsync();
                break;
            case ClipboardHotkeyId:
                _ = SpeakClipboardAsync();
                break;
            case CodexMicHotkeyId:
                _ = StartActiveVoiceInputAsync();
                break;
        }
    }

    private void RegisterConfiguredHotkeys()
    {
        if (!IsLoaded)
        {
            return;
        }

        var registrations = BuildHotkeyRegistrations();
        var duplicate = registrations
            .GroupBy(registration => registration.Gesture.StorageValue, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            SetStatus("Хоткей занят", "Одно сочетание назначено двум действиям. Измените одно из них.", "#F9C74F", busy: false);
            return;
        }

        if (!_hotkeyService.Register(this, registrations, out var failedHotkeyLabel))
        {
            SetStatus("Хоткей занят", $"{failedHotkeyLabel} уже используется другим приложением.", "#F9C74F", busy: false);
        }
    }

    private List<GlobalHotkeyRegistration> BuildHotkeyRegistrations()
    {
        var registrations = new List<GlobalHotkeyRegistration>();
        AddHotkeyRegistration(registrations, SpeakLatestHotkeyId, SpeakHotkeyLabelText.Text, _appSettings.SpeakLatestHotkey);
        AddHotkeyRegistration(registrations, ClipboardHotkeyId, ClipboardHotkeyLabelText.Text, _appSettings.SpeakClipboardHotkey);
        AddHotkeyRegistration(registrations, CodexMicHotkeyId, CodexMicHotkeyLabelText.Text, _appSettings.CodexMicHotkey);
        return registrations;
    }

    private static void AddHotkeyRegistration(List<GlobalHotkeyRegistration> registrations, string id, string label, string value)
    {
        if (HotkeyGesture.TryParse(value, out var gesture))
        {
            registrations.Add(new GlobalHotkeyRegistration(id, label, gesture));
        }
    }

    private void UpdateHotkeyButtons()
    {
        SpeakLatestHotkeyButton.Content = _capturingHotkeyId == SpeakLatestHotkeyId ? Tr("PressHotkeyButton") : GetHotkeyDisplay(_appSettings.SpeakLatestHotkey);
        ClipboardHotkeyButton.Content = _capturingHotkeyId == ClipboardHotkeyId ? Tr("PressHotkeyButton") : GetHotkeyDisplay(_appSettings.SpeakClipboardHotkey);
        CodexMicHotkeyButton.Content = _capturingHotkeyId == CodexMicHotkeyId ? Tr("PressHotkeyButton") : GetHotkeyDisplay(_appSettings.CodexMicHotkey);
    }

    private bool IsDuplicateHotkey(string currentHotkeyId, string value)
    {
        return HotkeyGesture.TryParse(value, out var gesture)
            && new[]
            {
                (Id: SpeakLatestHotkeyId, Value: _appSettings.SpeakLatestHotkey),
                (Id: ClipboardHotkeyId, Value: _appSettings.SpeakClipboardHotkey),
                (Id: CodexMicHotkeyId, Value: _appSettings.CodexMicHotkey)
            }.Any(item => item.Id != currentHotkeyId
                && HotkeyGesture.TryParse(item.Value, out var existing)
                && string.Equals(existing.StorageValue, gesture.StorageValue, StringComparison.OrdinalIgnoreCase));
    }

    private string GetHotkeyDisplay(string value)
    {
        return HotkeyGesture.TryParse(value, out var gesture) ? gesture.DisplayText : Tr("UnassignedHotkey");
    }

    private System.Windows.Controls.Button GetHotkeyButton(string hotkeyId)
    {
        return hotkeyId switch
        {
            ClipboardHotkeyId => ClipboardHotkeyButton,
            CodexMicHotkeyId => CodexMicHotkeyButton,
            _ => SpeakLatestHotkeyButton
        };
    }

    private string GetHotkeyValue(string hotkeyId)
    {
        return hotkeyId switch
        {
            ClipboardHotkeyId => _appSettings.SpeakClipboardHotkey,
            CodexMicHotkeyId => _appSettings.CodexMicHotkey,
            _ => _appSettings.SpeakLatestHotkey
        };
    }

    private void SetHotkeyValue(string hotkeyId, string value)
    {
        switch (hotkeyId)
        {
            case ClipboardHotkeyId:
                _appSettings.SpeakClipboardHotkey = value;
                break;
            case CodexMicHotkeyId:
                _appSettings.CodexMicHotkey = value;
                break;
            default:
                _appSettings.SpeakLatestHotkey = value;
                break;
        }
    }
    private void FloatingButtonToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _appSettings.ShowFloatingButton = FloatingButtonToggle.IsChecked == true;
        _appSettingsStore.Save(_appSettings);

        if (_appSettings.ShowFloatingButton)
        {
            ShowFloatingButton();
            SetStatus("Плавающая кнопка", "Плавающая кнопка включена.", "#41D6A1", busy: false);
            return;
        }

        _floatingButtonWindow?.Close();
        _floatingButtonWindow = null;
        SetStatus("Плавающая кнопка", "Плавающая кнопка скрыта.", "#F9C74F", busy: false);
    }

    private void MinimizeToTrayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _appSettings.MinimizeToTray = MinimizeToTrayToggle.IsChecked == true;
        _appSettingsStore.Save(_appSettings);
        SetStatus("Общие настройки", _appSettings.MinimizeToTray ? "Окно будет сворачиваться в трей." : "Крестик будет закрывать приложение.", "#41D6A1", busy: false);
    }

    private void RememberFloatingButtonPositionToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _appSettings.RememberFloatingButtonPosition = RememberFloatingButtonPositionToggle.IsChecked == true;
        if (!_appSettings.RememberFloatingButtonPosition)
        {
            _appSettings.FloatingButtonLeft = null;
            _appSettings.FloatingButtonTop = null;
            RestartFloatingButtonIfVisible();
        }

        _appSettingsStore.Save(_appSettings);
        SetStatus("Позиция кнопки", _appSettings.RememberFloatingButtonPosition ? "Позиция плавающей кнопки будет сохраняться." : "Кнопка будет возвращаться в правый нижний угол.", "#41D6A1", busy: false);
    }

    private void StartWithWindowsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        var enabled = StartWithWindowsToggle.IsChecked == true;
        try
        {
            SetStartWithWindows(enabled);
            _appSettings.StartWithWindows = enabled;
            _appSettingsStore.Save(_appSettings);
            SetStatus("Автозапуск", enabled ? "Voice Button добавлен в автозапуск Windows." : "Voice Button убран из автозапуска Windows.", "#41D6A1", busy: false);
        }
        catch (Exception ex)
        {
            _isInitializing = true;
            StartWithWindowsToggle.IsChecked = _appSettings.StartWithWindows;
            _isInitializing = false;
            SetStatus("Ошибка", ex.Message, "#F25F5C", busy: false);
        }
    }

    private void ResetFloatingButtonPosition_Click(object sender, RoutedEventArgs e)
    {
        _appSettings.FloatingButtonLeft = null;
        _appSettings.FloatingButtonTop = null;
        _appSettingsStore.Save(_appSettings);

        RestartFloatingButtonIfVisible();

        SetStatus("Позиция сброшена", "Плавающая кнопка вернется в правый нижний угол.", "#41D6A1", busy: false);
    }

    private void ShowSettingsPage(string page)
    {
        _currentPage = page;
        GeneralPage.Visibility = page == "General" ? Visibility.Visible : Visibility.Collapsed;
        SpeechPage.Visibility = page == "Speech" ? Visibility.Visible : Visibility.Collapsed;
        HotkeysPage.Visibility = page == "Hotkeys" ? Visibility.Visible : Visibility.Collapsed;
        IntegrationPage.Visibility = page == "Integration" ? Visibility.Visible : Visibility.Collapsed;

        SetNavigationState(GeneralNavButton, GeneralNavIcon, page == "General");
        SetNavigationState(SpeechNavButton, SpeechNavIcon, page == "Speech");
        SetNavigationState(HotkeysNavButton, HotkeysNavIcon, page == "Hotkeys");
        SetNavigationState(IntegrationNavButton, IntegrationNavIcon, page == "Integration");

        UpdateHeaderTitle();
    }

    private void SetNavigationState(System.Windows.Controls.Button button, TextBlock icon, bool selected)
    {
        button.Style = (Style)FindResource(selected ? "SelectedSidebarButton" : "SidebarButton");
        icon.Foreground = BrushFromHex(selected ? "#24A3FF" : "#9EB4D0");
    }

    private void RefreshVoiceOptionsForModel()
    {
        var options = IsLegacySpeechModel(_settings.Model) ? VoiceOption.Legacy : VoiceOption.All;
        if (!options.Any(option => string.Equals(option.Id, _settings.Voice, StringComparison.Ordinal)))
        {
            _settings.Voice = options[0].Id;
            _appSettings.Voice = _settings.Voice;
        }

        _isRefreshingVoiceOptions = true;
        VoiceComboBox.ItemsSource = options;
        VoiceComboBox.SelectedValue = _settings.Voice;
        _isRefreshingVoiceOptions = false;
    }

    private static bool IsLegacySpeechModel(string model)
    {
        return string.Equals(model, "tts-1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model, "tts-1-hd", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSpeechModel(string? model)
    {
        return SpeechModelOption.All.Any(option => string.Equals(option.Id, model, StringComparison.Ordinal))
            ? model!
            : "gpt-4o-mini-tts";
    }

    private static string NormalizeInterfaceLanguage(string? language)
    {
        return InterfaceLanguageOption.All.Any(option => string.Equals(option.Id, language, StringComparison.Ordinal))
            ? language!
            : "ru";
    }

    private static double NormalizeSpeechSpeed(double speed)
    {
        return Math.Clamp(Math.Round(speed, 2), 0.25, 4.0);
    }

    private static string FormatSpeechSpeed(double speed)
    {
        return $"{NormalizeSpeechSpeed(speed):0.00}x";
    }
    private void InterfaceLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InterfaceLanguageComboBox.SelectedValue is not string language)
        {
            return;
        }

        _appSettings.InterfaceLanguage = NormalizeInterfaceLanguage(language);
        ApplyInterfaceLanguage();

        if (_isInitializing)
        {
            return;
        }

        _appSettingsStore.Save(_appSettings);
        SetStatus(Tr("InterfaceLanguage"), Tr("InterfaceLanguageDetail"), "#41D6A1", busy: false);
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_appSettings is null || SpeedValueText is null)
        {
            return;
        }

        var speed = NormalizeSpeechSpeed(e.NewValue);
        _settings.Speed = speed;
        SpeedValueText.Text = FormatSpeechSpeed(speed);

        if (_isInitializing)
        {
            return;
        }

        _appSettings.SpeechSpeed = speed;
        _appSettingsStore.Save(_appSettings);
    }
    private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelComboBox.SelectedValue is not string model)
        {
            return;
        }

        _settings.Model = NormalizeSpeechModel(model);
        _appSettings.SpeechModel = _settings.Model;
        RefreshVoiceOptionsForModel();

        if (_isInitializing)
        {
            return;
        }

        _appSettingsStore.Save(_appSettings);
        SetStatus(Tr("VoiceModel"), _settings.Model, "#41D6A1", busy: false);
    }

    private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingVoiceOptions || VoiceComboBox.SelectedValue is not string voice)
        {
            return;
        }

        _settings.Voice = voice;
        _appSettings.Voice = voice;

        if (!_isInitializing)
        {
            _appSettingsStore.Save(_appSettings);
        }
    }

    private void TextPreparationToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _appSettings.HideFilePathsInSpeech = HidePathsToggle.IsChecked == true;
        _appSettings.HideCodeBlocksInSpeech = HideCodeBlocksToggle.IsChecked == true;
        _appSettings.HideInlineCodeInSpeech = HideInlineCodeToggle.IsChecked == true;
        _appSettings.ShortenLinksInSpeech = ShortenLinksToggle.IsChecked == true;
        _appSettings.HideSecretsInSpeech = HideSecretsToggle.IsChecked == true;
        _appSettings.ShortenHashesInSpeech = ShortenHashesToggle.IsChecked == true;
        _appSettings.CollapseStackTracesInSpeech = CollapseStackTracesToggle.IsChecked == true;
        _appSettings.RemoveMarkdownNoiseInSpeech = RemoveMarkdownNoiseToggle.IsChecked == true;
        _appSettings.CollapseTablesInSpeech = CollapseTablesToggle.IsChecked == true;
        _appSettings.CollapseStructuredDataInSpeech = CollapseStructuredDataToggle.IsChecked == true;
        _appSettings.ShortenShellCommandsInSpeech = ShortenShellCommandsToggle.IsChecked == true;
        _appSettings.HideLongNumbersInSpeech = HideLongNumbersToggle.IsChecked == true;
        _appSettingsStore.Save(_appSettings);
        SetStatus(Tr("TextPreparationSectionTitle"), Tr("TextPreparationSavedDetail"), "#41D6A1", busy: false);
    }

    private void IntegrationToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _appSettings.HoverToRevealCopyButton = HoverCopyButtonToggle.IsChecked == true;
        _appSettings.RestoreClipboardAfterCopy = RestoreClipboardToggle.IsChecked == true;
        _appSettings.FallbackToClipboardWhenCopyMissing = ClipboardFallbackToggle.IsChecked == true;
        _appSettings.RetryMicrophoneIfInactive = RetryMicrophoneToggle.IsChecked == true;
        _appSettingsStore.Save(_appSettings);
        SetStatus(Tr("IntegrationPageTitle"), Tr("IntegrationSavedDetail"), "#41D6A1", busy: false);
    }

    private void CodexWindowKeywordsBox_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveCodexWindowKeywords(report: false);
    }

    private void CodexWindowKeywordsBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveCodexWindowKeywords(report: true);
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CodexWindowKeywordsBox.Text = NormalizeCodexWindowKeywords(_appSettings.CodexWindowKeywords);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void TestCodexWindowButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCodexWindowKeywords(report: false);
        var window = new CodexWindowFinder(_appSettings).FindBestWindow();
        if (window is null)
        {
            _diagnosticsLog.Info("Assistant window diagnostic", "window not found");
            SetStatus(Tr("CodexWindowNotFound"), Tr("CodexWindowNotFoundDetail"), "#F9C74F", busy: false);
            return;
        }

        var windowLabel = string.IsNullOrWhiteSpace(window.Title)
            ? window.ProcessName
            : $"{window.Title} ({window.ProcessName})";
        var detail = $"{window.AppName}: {windowLabel}";
        _diagnosticsLog.Info("Assistant window diagnostic", detail);
        SetStatus(Tr("CodexWindowFound"), detail, "#41D6A1", busy: false);
    }

    private async void TestCopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetStatus(Tr("DiagnosticRunning"), Tr("DiagnosticCopyDetail"), "#37D0F4", busy: true);
            var diagnostics = await _codexCopyService.DiagnoseAsync(CancellationToken.None);
            _diagnosticsLog.Info("Assistant Copy diagnostic", diagnostics.ToLogLine());
            if (!diagnostics.WindowFound)
            {
                SetStatus(Tr("CodexWindowNotFound"), Tr("CodexWindowNotFoundDetail"), "#F9C74F", busy: false);
                return;
            }

            if (!diagnostics.CopyButtonFound)
            {
                SetStatus(Tr("CopyNotFound"), string.Format(Tr("DiagnosticWindowDetail"), diagnostics.WindowLabel), "#F9C74F", busy: false);
                return;
            }

            SetStatus(Tr("CopyFound"), string.Format(Tr("CopyFoundDetail"), diagnostics.CopyButtonCount), "#41D6A1", busy: false);
        }
        catch (Exception ex)
        {
            _diagnosticsLog.Error("Assistant Copy diagnostic", ex);
            SetStatus(Tr("Error"), ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetStatus(Tr("DiagnosticRunning"), Tr("DiagnosticMicrophoneDetail"), "#37D0F4", busy: true);
            var diagnostics = await _codexMicrophoneService.DiagnoseAsync(CancellationToken.None);
            _diagnosticsLog.Info("Assistant microphone diagnostic", diagnostics.ToLogLine());
            if (!diagnostics.WindowFound)
            {
                SetStatus(Tr("CodexWindowNotFound"), Tr("CodexWindowNotFoundDetail"), "#F9C74F", busy: false);
                return;
            }

            if (!diagnostics.MicrophoneButtonFound)
            {
                SetStatus(Tr("MicrophoneNotFound"), string.Format(Tr("DiagnosticWindowDetail"), diagnostics.WindowLabel), "#F9C74F", busy: false);
                return;
            }

            SetStatus(Tr("MicrophoneFound"), string.Format(Tr("MicrophoneFoundDetail"), diagnostics.MicrophoneButtonCount), "#41D6A1", busy: false);
        }
        catch (Exception ex)
        {
            _diagnosticsLog.Error("Assistant microphone diagnostic", ex);
            SetStatus(Tr("Error"), ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveCodexWindowKeywords(bool report)
    {
        var normalized = NormalizeCodexWindowKeywords(CodexWindowKeywordsBox.Text);
        if (!string.Equals(CodexWindowKeywordsBox.Text, normalized, StringComparison.Ordinal))
        {
            CodexWindowKeywordsBox.Text = normalized;
        }

        if (string.Equals(_appSettings.CodexWindowKeywords, normalized, StringComparison.Ordinal) && !report)
        {
            return;
        }

        _appSettings.CodexWindowKeywords = normalized;
        if (!_isInitializing)
        {
            _appSettingsStore.Save(_appSettings);
        }

        if (report)
        {
            SetStatus(Tr("CodexWindowKeywordsSaved"), normalized, "#41D6A1", busy: false);
        }
    }

    private static string NormalizeCodexWindowKeywords(string? value)
    {
        var keywords = (value ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return keywords.Length == 0 ? "Codex" : string.Join(", ", keywords);
    }
    private async Task StartActiveVoiceInputAsync()
    {
        if (!TryStartRun(out var cancellationToken))
        {
            return;
        }

        try
        {
            var appName = await _codexMicrophoneService.StartVoiceInputAsync(SetCopyStatus, cancellationToken);
            SetStatus($"Микрофон {appName}", "Голосовой ввод запущен.", "#41D6A1", busy: false);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Остановлено", "Действие прервано.", "#F9C74F", busy: false);
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка", ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            FinishRun();
        }
    }

    private async Task SpeakLatestAnswerAsync()
    {
        if (!TryStartRun(out var cancellationToken))
        {
            return;
        }

        try
        {
            EnsureApiKeyReady();
            var text = await _codexCopyService.CopyLastAnswerAsync(SetCopyStatus, cancellationToken);
            await SpeakTextAsync(text, cancellationToken);
            SetReady();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Остановлено", "Озвучка прервана.", "#F9C74F", busy: false);
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка", ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            FinishRun();
        }
    }

    private async Task SpeakClipboardAsync()
    {
        if (!TryStartRun(out var cancellationToken))
        {
            return;
        }

        try
        {
            EnsureApiKeyReady();
            var text = _clipboardService.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("В clipboard нет текста для озвучки.");
            }

            await SpeakTextAsync(text.Trim(), cancellationToken);
            SetReady();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Остановлено", "Озвучка прервана.", "#F9C74F", busy: false);
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка", ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            FinishRun();
        }
    }

    private async Task PreviewVoiceAsync()
    {
        if (!TryStartRun(out var cancellationToken))
        {
            return;
        }

        try
        {
            EnsureApiKeyReady();
            var voiceLabel = GetSelectedVoiceLabel();
            SetStatus("Пробую голос", voiceLabel, "#37D0F4", busy: true);
            var audio = await _speechClient.CreateSpeechAsync(
                "Проверка голоса. Так будет звучать озвучка ответов Codex и ChatGPT.",
                _settings,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            SetStatus("Озвучиваю", voiceLabel, "#41D6A1", busy: true);
            await _audioPlaybackService.PlayAsync(audio, _settings.ResponseFormat, cancellationToken);
            SetReady();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Остановлено", "Озвучка прервана.", "#F9C74F", busy: false);
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка", ex.Message, "#F25F5C", busy: false);
        }
        finally
        {
            FinishRun();
        }
    }

    private async Task SpeakTextAsync(string text, CancellationToken cancellationToken)
    {
        var speakableText = TtsTextSanitizer.Sanitize(text, TtsTextSanitizerOptions.FromSettings(_appSettings));
        var chunks = TextChunker.Split(speakableText, _settings.MaxChunkLength);
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("Нет текста для озвучки.");
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus("Генерирую аудио", $"{index + 1}/{chunks.Count}", "#37D0F4", busy: true);
            var audio = await _speechClient.CreateSpeechAsync(chunks[index], _settings, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            SetStatus("Озвучиваю", $"{index + 1}/{chunks.Count}", "#41D6A1", busy: true);
            await _audioPlaybackService.PlayAsync(audio, _settings.ResponseFormat, cancellationToken);
        }
    }

    private bool TryStartRun(out CancellationToken cancellationToken)
    {
        cancellationToken = CancellationToken.None;
        if (_currentRun is not null)
        {
            SetStatus("Озвучиваю", "Дождись завершения или нажми Стоп.", "#F9C74F", busy: true);
            return false;
        }

        RefreshApiKeyStatus();
        _currentRun = new CancellationTokenSource();
        cancellationToken = _currentRun.Token;
        SetBusy(true);
        return true;
    }

    private void FinishRun()
    {
        _currentRun?.Dispose();
        _currentRun = null;
        SetBusy(false);
    }

    private void StopCurrentRun()
    {
        _currentRun?.Cancel();
        _audioPlaybackService.Stop();
    }

    private void EnsureApiKeyReady()
    {
        if (!OpenAiSpeechClient.HasUsableApiKey())
        {
            throw new InvalidOperationException(Tr("KeyMissingDetail"));
        }
    }

    private void SetCopyStatus(string status, string? detail)
    {
        SetStatus(status, detail ?? string.Empty, "#37D0F4", busy: true);
    }

    private void SetReady()
    {
        SetStatus(Tr("Ready"), string.Format(Tr("ReadyDetail"), GetHotkeyDisplay(_appSettings.SpeakLatestHotkey)), "#41D6A1", busy: false);
    }

    private void SetStatus(string status, string detail, string color, bool busy)
    {
        StatusText.Text = status;
        DetailText.Text = detail;
        StatusLight.Fill = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        SetBusy(busy);
    }

    private void SetBusy(bool busy)
    {
        SpeakButton.IsEnabled = !busy;
        ClipboardButton.IsEnabled = !busy;
        PreviewVoiceButton.IsEnabled = !busy;
        TestCodexWindowButton.IsEnabled = !busy;
        TestCopyButton.IsEnabled = !busy;
        TestMicrophoneButton.IsEnabled = !busy;
        CheckApiKeyButton.IsEnabled = !busy;
        SaveApiKeyButton.IsEnabled = !busy;
        ProgressBar.IsIndeterminate = busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Hidden;

    }

    private void AudioPlaybackService_PlaybackChanged(object? sender, PlaybackSnapshot snapshot)
    {
        _floatingButtonWindow?.SetPlaybackSnapshot(snapshot);
    }
    private void ApplyAccessibilityNames()
    {
        SetAutomationName(GeneralNavButton, GeneralNavText.Text);
        SetAutomationName(SpeechNavButton, SpeechNavText.Text);
        SetAutomationName(HotkeysNavButton, HotkeysNavText.Text);
        SetAutomationName(IntegrationNavButton, IntegrationNavText.Text);

        SetAutomationName(InterfaceLanguageComboBox, InterfaceLanguageLabelText.Text, InterfaceLanguageHintText.Text);
        SetAutomationName(FloatingButtonToggle, FloatingButtonLabelText.Text, FloatingButtonHintText.Text);
        SetAutomationName(MinimizeToTrayToggle, MinimizeToTrayLabelText.Text, MinimizeToTrayHintText.Text);
        SetAutomationName(StartWithWindowsToggle, StartWithWindowsLabelText.Text, StartWithWindowsHintText.Text);
        SetAutomationName(RememberFloatingButtonPositionToggle, RememberFloatingPositionLabelText.Text, RememberFloatingPositionHintText.Text);
        SetAutomationName(ResetFloatingPositionButton, ResetFloatingPositionButton.Content?.ToString() ?? string.Empty);

        SetAutomationName(OpenAiApiKeyBox, ApiKeyLabelText.Text);
        SetAutomationName(SaveApiKeyButton, SaveApiKeyButton.Content?.ToString() ?? string.Empty);
        SetAutomationName(CheckApiKeyButton, CheckApiKeyButton.Content?.ToString() ?? string.Empty);
        SetAutomationName(ModelComboBox, ModelLabelText.Text);
        SetAutomationName(VoiceComboBox, VoiceLabelText.Text);
        SetAutomationName(SpeedSlider, SpeedLabelText.Text);
        SetAutomationName(PreviewVoiceButton, PreviewVoiceButton.Content?.ToString() ?? string.Empty);
        SetAutomationName(HidePathsToggle, HidePathsLabelText.Text, HidePathsHintText.Text);
        SetAutomationName(HideCodeBlocksToggle, HideCodeBlocksLabelText.Text, HideCodeBlocksHintText.Text);
        SetAutomationName(HideInlineCodeToggle, HideInlineCodeLabelText.Text, HideInlineCodeHintText.Text);
        SetAutomationName(ShortenLinksToggle, ShortenLinksLabelText.Text, ShortenLinksHintText.Text);
        SetAutomationName(HideSecretsToggle, HideSecretsLabelText.Text, HideSecretsHintText.Text);
        SetAutomationName(ShortenHashesToggle, ShortenHashesLabelText.Text, ShortenHashesHintText.Text);
        SetAutomationName(CollapseStackTracesToggle, CollapseStackTracesLabelText.Text, CollapseStackTracesHintText.Text);
        SetAutomationName(RemoveMarkdownNoiseToggle, RemoveMarkdownNoiseLabelText.Text, RemoveMarkdownNoiseHintText.Text);
        SetAutomationName(CollapseTablesToggle, CollapseTablesLabelText.Text, CollapseTablesHintText.Text);
        SetAutomationName(CollapseStructuredDataToggle, CollapseStructuredDataLabelText.Text, CollapseStructuredDataHintText.Text);
        SetAutomationName(ShortenShellCommandsToggle, ShortenShellCommandsLabelText.Text, ShortenShellCommandsHintText.Text);
        SetAutomationName(HideLongNumbersToggle, HideLongNumbersLabelText.Text, HideLongNumbersHintText.Text);

        SetAutomationName(SpeakLatestHotkeyButton, SpeakHotkeyLabelText.Text, SpeakHotkeyHintText.Text);
        SetAutomationName(ClipboardHotkeyButton, ClipboardHotkeyLabelText.Text, ClipboardHotkeyHintText.Text);
        SetAutomationName(CodexMicHotkeyButton, CodexMicHotkeyLabelText.Text, CodexMicHotkeyHintText.Text);

        SetAutomationName(CodexWindowKeywordsBox, CodexWindowKeywordsLabelText.Text, CodexWindowKeywordsHintText.Text);
        SetAutomationName(TestCodexWindowButton, TestCodexWindowButton.Content?.ToString() ?? string.Empty);
        SetAutomationName(HoverCopyButtonToggle, HoverCopyButtonLabelText.Text, HoverCopyButtonHintText.Text);
        SetAutomationName(RestoreClipboardToggle, RestoreClipboardLabelText.Text, RestoreClipboardHintText.Text);
        SetAutomationName(ClipboardFallbackToggle, ClipboardFallbackLabelText.Text, ClipboardFallbackHintText.Text);
        SetAutomationName(RetryMicrophoneToggle, RetryMicrophoneLabelText.Text, RetryMicrophoneHintText.Text);
        SetAutomationName(TestCopyButton, TestCopyButton.Content?.ToString() ?? string.Empty, DiagnosticsHintText.Text);
        SetAutomationName(TestMicrophoneButton, TestMicrophoneButton.Content?.ToString() ?? string.Empty, DiagnosticsHintText.Text);
    }

    private static void SetAutomationName(FrameworkElement element, string name, string? helpText = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            WpfAutomationProperties.SetName(element, name);
        }

        if (!string.IsNullOrWhiteSpace(helpText))
        {
            WpfAutomationProperties.SetHelpText(element, helpText);
        }
    }

    private void RefreshApiKeyStatus()
    {
        if (OpenAiSpeechClient.HasUsableApiKey())
        {
            ApiKeyStateText.Text = Tr("KeyConnected");
            ApiKeyStateText.Foreground = BrushFromHex("#8AF2D2");
            ApiKeyBadge.Background = BrushFromHex("#0E2A28");
            ApiKeyBadge.BorderBrush = BrushFromHex("#1D7A66");
            ApiKeyDot.Fill = BrushFromHex("#41D6A1");
            return;
        }

        ApiKeyStateText.Text = Tr("KeyMissing");
        ApiKeyStateText.Foreground = BrushFromHex("#FFD48A");
        ApiKeyBadge.Background = BrushFromHex("#2A1F0E");
        ApiKeyBadge.BorderBrush = BrushFromHex("#8A6A1D");
        ApiKeyDot.Fill = BrushFromHex("#F9C74F");
    }

    private string GetSelectedVoiceLabel()
    {
        return VoiceComboBox.SelectedItem is VoiceOption option ? option.Label : _settings.Voice;
    }

    private SolidColorBrush BrushFromHex(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ShowFloatingButton()
    {
        if (!_appSettings.ShowFloatingButton || _floatingButtonWindow is not null)
        {
            return;
        }

        _floatingButtonWindow = new FloatingButtonWindow(
            () => _ = SpeakLatestAnswerAsync(),
            () => _ = StartActiveVoiceInputAsync(),
            _audioPlaybackService.TogglePause,
            _audioPlaybackService.Seek,
            StopCurrentRun,
            _appSettings,
            SaveFloatingButtonPosition);
        _floatingButtonWindow.SetPlaybackSnapshot(_audioPlaybackService.CurrentSnapshot);
        _floatingButtonWindow.Show();
    }

    private void SaveFloatingButtonPosition(double left, double top)
    {
        if (!_appSettings.RememberFloatingButtonPosition)
        {
            return;
        }

        _appSettings.FloatingButtonLeft = left;
        _appSettings.FloatingButtonTop = top;
        _appSettingsStore.Save(_appSettings);
    }

    private void RestartFloatingButtonIfVisible()
    {
        if (_floatingButtonWindow is null)
        {
            return;
        }

        _floatingButtonWindow.Close();
        _floatingButtonWindow = null;
        ShowFloatingButton();
    }

    private bool IsStartWithWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
        return key?.GetValue(StartupAppName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    private void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("Не удалось открыть настройки автозапуска Windows.");
        }

        if (!enabled)
        {
            key.DeleteValue(StartupAppName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Не удалось определить путь к VoiceButton.exe.");
        }

        key.SetValue(StartupAppName, "\"" + executablePath + "\"");
    }

    private void DisposeApplicationShell()
    {
        _floatingButtonWindow?.Close();
        _floatingButtonWindow = null;
        _hotkeyService.Dispose();
        _trayIconService?.Dispose();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        StopCurrentRun();
        _floatingButtonWindow?.Close();
        _floatingButtonWindow = null;
        Close();
    }
}
