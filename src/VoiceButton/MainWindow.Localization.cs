using System.Collections.Generic;

namespace VoiceButton;

public partial class MainWindow
{
    private static readonly IReadOnlyDictionary<string, string[]> UiTexts = new Dictionary<string, string[]>
    {
        ["NavGeneral"] = new[] { "Общие", "Загальні", "General" },
        ["NavSpeech"] = new[] { "Озвучка", "Озвучення", "Speech" },
        ["NavHotkeys"] = new[] { "Горячие клавиши", "Гарячі клавіші", "Hotkeys" },
        ["NavIntegration"] = new[] { "Интеграция", "Інтеграція", "Integration" },
        ["HeaderGeneral"] = new[] { "Настройки Voice Button", "Налаштування Voice Button", "Voice Button Settings" },
        ["HeaderSpeech"] = new[] { "Настройки озвучки", "Налаштування озвучення", "Speech Settings" },
        ["HeaderHotkeys"] = new[] { "Горячие клавиши", "Гарячі клавіші", "Hotkeys" },
        ["HeaderIntegration"] = new[] { "Codex и ChatGPT", "Codex і ChatGPT", "Codex and ChatGPT" },
        ["GeneralPageTitle"] = new[] { "Общие", "Загальні", "General" },
        ["GeneralPageHint"] = new[] { "Поведение окна, плавающая кнопка и запуск приложения.", "Поведінка вікна, плаваюча кнопка і запуск програми.", "Window behavior, floating button, and application startup." },
        ["GeneralSectionTitle"] = new[] { "Поведение", "Поведінка", "Behavior" },
        ["InterfaceLanguageLabel"] = new[] { "Язык интерфейса", "Мова інтерфейсу", "Interface language" },
        ["InterfaceLanguageHint"] = new[] { "Переводит основные элементы окна настроек.", "Перекладає основні елементи вікна налаштувань.", "Translates the main settings window controls." },
        ["FloatingButtonLabel"] = new[] { "Показывать плавающую кнопку", "Показувати плаваючу кнопку", "Show floating button" },
        ["FloatingButtonHint"] = new[] { "Маленькая кнопка над часами: микрофон слева, озвучка справа.", "Маленька кнопка над годинником: мікрофон зліва, озвучення справа.", "Small button above the clock: microphone on the left, speech on the right." },
        ["MinimizeToTrayLabel"] = new[] { "Сворачивать в трей", "Згортати в трей", "Minimize to tray" },
        ["MinimizeToTrayHint"] = new[] { "Крестик и кнопка свернуть прячут окно, приложение остается под рукой.", "Хрестик і кнопка згортання ховають вікно, програма лишається під рукою.", "Close and minimize hide the window while the app keeps running." },
        ["StartWithWindowsLabel"] = new[] { "Запускать вместе с Windows", "Запускати разом із Windows", "Start with Windows" },
        ["StartWithWindowsHint"] = new[] { "Добавляет Voice Button в автозапуск текущего пользователя Windows.", "Додає Voice Button в автозапуск поточного користувача Windows.", "Adds Voice Button to the current Windows user's startup apps." },
        ["RememberFloatingPositionLabel"] = new[] { "Запоминать позицию кнопки", "Запам'ятовувати позицію кнопки", "Remember button position" },
        ["RememberFloatingPositionHint"] = new[] { "Если выключить, кнопка каждый запуск возвращается в правый нижний угол.", "Якщо вимкнути, кнопка під час кожного запуску повертається у правий нижній кут.", "When off, the button returns to the lower-right corner on each launch." },
        ["ResetFloatingPositionButton"] = new[] { "Сбросить позицию кнопки", "Скинути позицію кнопки", "Reset button position" },
        ["SpeechPageTitle"] = new[] { "Озвучка", "Озвучення", "Speech" },
        ["SpeechPageHint"] = new[] { "OpenAI, модель, голос и подготовка текста перед озвучкой.", "OpenAI, модель, голос і підготовка тексту перед озвученням.", "OpenAI, model, voice, and text preparation before playback." },
        ["OpenAiSectionTitle"] = new[] { "OpenAI", "OpenAI", "OpenAI" },
        ["ProviderLabel"] = new[] { "Поставщик", "Постачальник", "Provider" },
        ["ApiKeyLabel"] = new[] { "OpenAI API key", "OpenAI API key", "OpenAI API key" },
        ["ApiKeyStatusLabel"] = new[] { "Статус ключа", "Статус ключа", "Key status" },
        ["SaveButton"] = new[] { "Сохранить", "Зберегти", "Save" },
        ["CheckButton"] = new[] { "Проверить", "Перевірити", "Check" },
        ["VoiceSectionTitle"] = new[] { "Голос", "Голос", "Voice" },
        ["ModelLabel"] = new[] { "Модель голоса", "Модель голосу", "Voice model" },
        ["VoiceLabel"] = new[] { "Голос", "Голос", "Voice" },
        ["SpeedLabel"] = new[] { "Скорость", "Швидкість", "Speed" },
        ["PreviewVoiceLabel"] = new[] { "Тест озвучки", "Тест озвучення", "Speech test" },
        ["PreviewVoiceButton"] = new[] { "Тест", "Тест", "Test" },
        ["TextPreparationSectionTitle"] = new[] { "Подготовка текста", "Підготовка тексту", "Text preparation" },
        ["HidePathsLabel"] = new[] { "Скрывать пути к файлам", "Приховувати шляхи до файлів", "Hide file paths" },
        ["HidePathsHint"] = new[] { "В аудио остается имя файла, без полного пути.", "В аудіо лишається назва файлу, без повного шляху.", "Audio keeps only the file name, without the full path." },
        ["HideCodeBlocksLabel"] = new[] { "Скрывать блоки кода", "Приховувати блоки коду", "Hide code blocks" },
        ["HideCodeBlocksHint"] = new[] { "Многострочные и отступленные блоки кода заменяются короткой пометкой.", "Багаторядкові блоки коду та блоки з відступами замінюються короткою позначкою.", "Multiline and indented code blocks are replaced with a short marker." },
        ["HideInlineCodeLabel"] = new[] { "Скрывать inline-code", "Приховувати inline-code", "Hide inline code" },
        ["HideInlineCodeHint"] = new[] { "Короткий текст в обратных кавычках заменяется пометкой.", "Короткий текст у зворотних лапках замінюється позначкою.", "Short text in backticks is replaced with a marker." },
        ["ShortenLinksLabel"] = new[] { "Сокращать ссылки", "Скорочувати посилання", "Shorten links" },
        ["ShortenLinksHint"] = new[] { "Остается домен и последний понятный фрагмент URL.", "Залишається домен і останній зрозумілий фрагмент URL.", "Keeps the domain and the last useful URL segment." },
        ["HideSecretsLabel"] = new[] { "Скрывать секреты и токены", "Приховувати секрети й токени", "Hide secrets and tokens" },
        ["HideSecretsHint"] = new[] { "API keys, bearer-токены, JWT и пароли не уходят в озвучку.", "API keys, bearer-токени, JWT і паролі не йдуть в озвучення.", "API keys, bearer tokens, JWTs, and passwords are not spoken." },
        ["ShortenHashesLabel"] = new[] { "Сокращать hash, UUID и commit id", "Скорочувати hash, UUID і commit id", "Shorten hashes, UUIDs, commit IDs" },
        ["ShortenHashesHint"] = new[] { "Длинные идентификаторы читаются только по первым символам.", "Довгі ідентифікатори читаються тільки за першими символами.", "Long identifiers are spoken only by their first characters." },
        ["CollapseStackTracesLabel"] = new[] { "Сжимать логи и stack trace", "Стискати логи й stack trace", "Collapse logs and stack traces" },
        ["CollapseStackTracesHint"] = new[] { "Повторяющиеся строки логов заменяются одной пометкой.", "Повторювані рядки логів замінюються однією позначкою.", "Repeated log lines are replaced with one marker." },
        ["RemoveMarkdownNoiseLabel"] = new[] { "Убирать markdown-шум", "Прибирати markdown-шум", "Remove Markdown noise" },
        ["RemoveMarkdownNoiseHint"] = new[] { "Символы разметки не будут зачитываться как отдельный текст.", "Символи розмітки не будуть читатися як окремий текст.", "Formatting symbols are not spoken as separate text." },
        ["CollapseTablesLabel"] = new[] { "Сжимать таблицы", "Стискати таблиці", "Collapse tables" },
        ["CollapseTablesHint"] = new[] { "Markdown-таблица заменяется короткой пометкой.", "Markdown-таблиця замінюється короткою позначкою.", "Markdown tables are replaced with a short marker." },
        ["CollapseStructuredDataLabel"] = new[] { "Сжимать JSON и YAML", "Стискати JSON і YAML", "Collapse JSON and YAML" },
        ["CollapseStructuredDataHint"] = new[] { "Конфиги и структурированные блоки не читаются построчно.", "Конфіги й структуровані блоки не читаються порядково.", "Configs and structured blocks are not spoken line by line." },
        ["ShortenShellCommandsLabel"] = new[] { "Скрывать команды", "Приховувати команди", "Hide commands" },
        ["ShortenShellCommandsHint"] = new[] { "Длинные shell-команды заменяются короткой пометкой.", "Довгі shell-команди замінюються короткою позначкою.", "Long shell commands are replaced with a short marker." },
        ["HideLongNumbersLabel"] = new[] { "Скрывать длинные числа и дампы", "Приховувати довгі числа й дампи", "Hide long numbers and dumps" },
        ["HideLongNumbersHint"] = new[] { "Base64, hex-дампы и длинные номера сокращаются.", "Base64, hex-дампи й довгі номери скорочуються.", "Base64, hex dumps, and long numbers are shortened." },
        ["TextPreparationSavedDetail"] = new[] { "Настройки подготовки текста сохранены.", "Налаштування підготовки тексту збережено.", "Text preparation settings saved." },
        ["HotkeysPageTitle"] = new[] { "Горячие клавиши", "Гарячі клавіші", "Hotkeys" },
        ["HotkeysPageHint"] = new[] { "Нажмите на сочетание, затем зажмите нужные клавиши. Esc отменяет запись, Delete очищает.", "Натисніть на комбінацію, потім затисніть потрібні клавіші. Esc скасовує запис, Delete очищає.", "Click a shortcut, then press the desired keys. Esc cancels recording, Delete clears it." },
        ["HotkeysSectionTitle"] = new[] { "Активные сочетания", "Активні комбінації", "Active shortcuts" },
        ["SpeakHotkeyLabel"] = new[] { "Озвучить последний ответ", "Озвучити останню відповідь", "Speak latest answer" },
        ["SpeakHotkeyHint"] = new[] { "Копирует последний ответ активного Codex или ChatGPT и запускает озвучку.", "Копіює останню відповідь активного Codex або ChatGPT і запускає озвучення.", "Copies the latest answer from the active Codex or ChatGPT window and starts playback." },
        ["ClipboardHotkeyLabel"] = new[] { "Озвучить clipboard", "Озвучити clipboard", "Speak clipboard" },
        ["ClipboardHotkeyHint"] = new[] { "Озвучивает текущий текст из буфера обмена.", "Озвучує поточний текст із буфера обміну.", "Speaks the current clipboard text." },
        ["CodexMicLabel"] = new[] { "Микрофон приложения", "Мікрофон програми", "App microphone" },
        ["CodexMicHotkeyHint"] = new[] { "Нажимает встроенную кнопку микрофона в активном Codex или ChatGPT.", "Натискає вбудовану кнопку мікрофона в активному Codex або ChatGPT.", "Presses the built-in microphone button in the active Codex or ChatGPT window." },
        ["UnassignedHotkey"] = new[] { "Не назначено", "Не призначено", "Not assigned" },
        ["ResetHotkeyButton"] = new[] { "Сброс", "Скинути", "Reset" },
        ["ClearHotkeyButton"] = new[] { "Очистить", "Очистити", "Clear" },
        ["PressHotkeyButton"] = new[] { "Нажмите сочетание", "Натисніть комбінацію", "Press shortcut" },
        ["HotkeyEditorTitle"] = new[] { "Как записать сочетание", "Як записати комбінацію", "How to record a shortcut" },
        ["HotkeyEditorHint"] = new[] { "Нажмите поле сочетания, затем зажмите, например, Ctrl + Alt + V. Для глобального хоткея нужна хотя бы одна служебная клавиша.", "Натисніть поле комбінації, потім затисніть, наприклад, Ctrl + Alt + V. Для глобального хоткея потрібна хоча б одна службова клавіша.", "Click a shortcut field, then press something like Ctrl + Alt + V. A global shortcut needs at least one modifier key." },
        ["IntegrationPageTitle"] = new[] { "Интеграция", "Інтеграція", "Integration" },
        ["IntegrationPageHint"] = new[] { "Автовыбор активного Codex или ChatGPT, Copy/clipboard и запуск микрофона.", "Автовибір активного Codex або ChatGPT, Copy/clipboard і запуск мікрофона.", "Automatic active Codex or ChatGPT selection, Copy/clipboard, and microphone launch." },
        ["CodexWindowSectionTitle"] = new[] { "Приложения", "Програми", "Applications" },
        ["CodexWindowKeywordsLabel"] = new[] { "Ключевые слова Codex", "Ключові слова Codex", "Codex window keywords" },
        ["CodexWindowKeywordsHint"] = new[] { "ChatGPT определяется автоматически; здесь можно уточнить заголовок или процесс Codex.", "ChatGPT визначається автоматично; тут можна уточнити заголовок або процес Codex.", "ChatGPT is detected automatically; refine the Codex title or process here." },
        ["CopyIntegrationSectionTitle"] = new[] { "Copy и clipboard", "Copy і clipboard", "Copy and clipboard" },
        ["HoverCopyButtonLabel"] = new[] { "Показывать Copy наведением", "Показувати Copy наведенням", "Reveal Copy on hover" },
        ["HoverCopyButtonHint"] = new[] { "Если кнопка скрыта до hover, Voice Button наведет курсор в область последнего ответа.", "Якщо кнопка схована до hover, Voice Button наведе курсор в область останньої відповіді.", "If Copy is hidden until hover, Voice Button moves the cursor near the latest answer." },
        ["RestoreClipboardLabel"] = new[] { "Восстанавливать clipboard", "Відновлювати clipboard", "Restore clipboard" },
        ["RestoreClipboardHint"] = new[] { "После Copy возвращает прежний буфер обмена, если это возможно.", "Після Copy повертає попередній буфер обміну, якщо це можливо.", "After Copy, restores the previous clipboard when possible." },
        ["ClipboardFallbackLabel"] = new[] { "Озвучивать clipboard без Copy", "Озвучувати clipboard без Copy", "Use clipboard if Copy fails" },
        ["ClipboardFallbackHint"] = new[] { "Если Copy не найден, можно озвучить уже скопированный вручную текст.", "Якщо Copy не знайдено, можна озвучити вже скопійований вручну текст.", "If Copy is missing, speak already-copied manual clipboard text." },
        ["MicrophoneIntegrationSectionTitle"] = new[] { "Микрофон приложений", "Мікрофон програм", "Application microphone" },
        ["RetryMicrophoneLabel"] = new[] { "Повторять запуск микрофона", "Повторювати запуск мікрофона", "Retry microphone launch" },
        ["RetryMicrophoneHint"] = new[] { "Повторный клик применяется только к Codex; диктовка ChatGPT запускается один раз.", "Повторний клік застосовується лише до Codex; диктування ChatGPT запускається один раз.", "The retry applies only to Codex; ChatGPT dictation is started once." },
        ["DiagnosticsSectionTitle"] = new[] { "Диагностика", "Діагностика", "Diagnostics" },
        ["DiagnosticsLabel"] = new[] { "Проверить активное приложение", "Перевірити активну програму", "Check active application" },
        ["DiagnosticsHint"] = new[] { "Проверяет окно, Copy и микрофон. Результат пишется в diagnostics.log.", "Перевіряє вікно, Copy і мікрофон. Результат пишеться в diagnostics.log.", "Checks the window, Copy, and microphone. Results are written to diagnostics.log." },
        ["FindCopyButton"] = new[] { "Найти Copy", "Знайти Copy", "Find Copy" },
        ["FindMicrophoneButton"] = new[] { "Найти микрофон", "Знайти мікрофон", "Find microphone" },
        ["CheckingKey"] = new[] { "Проверяю ключ", "Перевіряю ключ", "Checking key" },
        ["CheckingKeyDetail"] = new[] { "Отправляю безопасный запрос к OpenAI.", "Надсилаю безпечний запит до OpenAI.", "Sending a safe request to OpenAI." },
        ["KeyInvalid"] = new[] { "Ключ не прошел проверку", "Ключ не пройшов перевірку", "Key check failed" },
        ["DiagnosticRunning"] = new[] { "Диагностика", "Діагностика", "Diagnostics" },
        ["DiagnosticCopyDetail"] = new[] { "Ищу кнопку Copy в активном Codex или ChatGPT.", "Шукаю кнопку Copy в активному Codex або ChatGPT.", "Looking for the Copy button in the active Codex or ChatGPT window." },
        ["DiagnosticMicrophoneDetail"] = new[] { "Ищу кнопку микрофона в активном Codex или ChatGPT.", "Шукаю кнопку мікрофона в активному Codex або ChatGPT.", "Looking for the microphone button in the active Codex or ChatGPT window." },
        ["DiagnosticWindowDetail"] = new[] { "Окно: {0}", "Вікно: {0}", "Window: {0}" },
        ["CopyFound"] = new[] { "Copy найден", "Copy знайдено", "Copy found" },
        ["CopyNotFound"] = new[] { "Copy не найден", "Copy не знайдено", "Copy not found" },
        ["CopyFoundDetail"] = new[] { "Найдено кнопок Copy у ответов: {0}.", "Знайдено кнопок Copy у відповідей: {0}.", "Answer Copy buttons found: {0}." },
        ["MicrophoneFound"] = new[] { "Микрофон найден", "Мікрофон знайдено", "Microphone found" },
        ["MicrophoneNotFound"] = new[] { "Микрофон не найден", "Мікрофон не знайдено", "Microphone not found" },
        ["MicrophoneFoundDetail"] = new[] { "Найдено кнопок микрофона: {0}.", "Знайдено кнопок мікрофона: {0}.", "Microphone buttons found: {0}." },
        ["IntegrationSavedDetail"] = new[] { "Настройки интеграции сохранены.", "Налаштування інтеграції збережено.", "Integration settings saved." },
        ["CodexWindowFound"] = new[] { "Приложение найдено", "Програму знайдено", "Application found" },
        ["CodexWindowNotFound"] = new[] { "Приложение не найдено", "Програму не знайдено", "Application not found" },
        ["CodexWindowNotFoundDetail"] = new[] { "Открой Codex или ChatGPT и попробуй снова.", "Відкрий Codex або ChatGPT і спробуй знову.", "Open Codex or ChatGPT and try again." },
        ["CodexWindowKeywordsSaved"] = new[] { "Ключевые слова сохранены", "Ключові слова збережено", "Window keywords saved" },
        ["StopButton"] = new[] { "Стоп", "Стоп", "Stop" },
        ["ClipboardButton"] = new[] { "Озвучить clipboard", "Озвучити clipboard", "Speak clipboard" },
        ["CancelButton"] = new[] { "Свернуть", "Згорнути", "Minimize" },
        ["SpeakButton"] = new[] { "Озвучить последний ответ", "Озвучити останню відповідь", "Speak latest answer" },
        ["AccentHint"] = new[] { "Автосохранение включено.", "Автозбереження увімкнено.", "Auto-save is on." },
        ["Ready"] = new[] { "Готово", "Готово", "Ready" },
        ["ReadyDetail"] = new[] { "{0} озвучит последний ответ.", "{0} озвучить останню відповідь.", "{0} speaks the latest answer." },
        ["KeyConnected"] = new[] { "Ключ подключен", "Ключ підключено", "Key connected" },
        ["KeyConnectedDetail"] = new[] { "OpenAI API key найден и готов к озвучке.", "OpenAI API key знайдено і готовий до озвучення.", "OpenAI API key was found and is ready for speech." },
        ["KeyMissing"] = new[] { "Ключ не задан", "Ключ не задано", "Key missing" },
        ["KeyMissingDetail"] = new[] { "Вставь ключ в разделе Озвучка и нажми Сохранить.", "Встав ключ у розділі Озвучення і натисни Зберегти.", "Paste the key in Speech and press Save." },
        ["PasteKeyFirst"] = new[] { "Сначала вставь OpenAI API key в поле ввода.", "Спочатку встав OpenAI API key у поле вводу.", "Paste an OpenAI API key first." },
        ["KeySaved"] = new[] { "Ключ сохранен", "Ключ збережено", "Key saved" },
        ["KeySavedDetail"] = new[] { "Ключ защищенно сохранен в Windows Credential Manager.", "Ключ захищено збережено у Windows Credential Manager.", "The key was securely saved in Windows Credential Manager." },
        ["Error"] = new[] { "Ошибка", "Помилка", "Error" },
        ["InterfaceLanguage"] = new[] { "Язык интерфейса", "Мова інтерфейсу", "Interface language" },
        ["InterfaceLanguageDetail"] = new[] { "Интерфейс переключен.", "Інтерфейс перемкнено.", "Interface language switched." },
        ["VoiceModel"] = new[] { "Модель голоса", "Модель голосу", "Voice model" }
    };

    private void ApplyInterfaceLanguage()
    {
        GeneralNavText.Text = Tr("NavGeneral");
        SpeechNavText.Text = Tr("NavSpeech");
        HotkeysNavText.Text = Tr("NavHotkeys");
        IntegrationNavText.Text = Tr("NavIntegration");
        GeneralPageTitleText.Text = Tr("GeneralPageTitle");
        GeneralPageHintText.Text = Tr("GeneralPageHint");
        GeneralSectionTitleText.Text = Tr("GeneralSectionTitle");
        InterfaceLanguageLabelText.Text = Tr("InterfaceLanguageLabel");
        InterfaceLanguageHintText.Text = Tr("InterfaceLanguageHint");
        FloatingButtonLabelText.Text = Tr("FloatingButtonLabel");
        FloatingButtonHintText.Text = Tr("FloatingButtonHint");
        MinimizeToTrayLabelText.Text = Tr("MinimizeToTrayLabel");
        MinimizeToTrayHintText.Text = Tr("MinimizeToTrayHint");
        StartWithWindowsLabelText.Text = Tr("StartWithWindowsLabel");
        StartWithWindowsHintText.Text = Tr("StartWithWindowsHint");
        RememberFloatingPositionLabelText.Text = Tr("RememberFloatingPositionLabel");
        RememberFloatingPositionHintText.Text = Tr("RememberFloatingPositionHint");
        ResetFloatingPositionButton.Content = Tr("ResetFloatingPositionButton");
        SpeechPageTitleText.Text = Tr("SpeechPageTitle");
        SpeechPageHintText.Text = Tr("SpeechPageHint");
        OpenAiSectionTitleText.Text = Tr("OpenAiSectionTitle");
        ProviderLabelText.Text = Tr("ProviderLabel");
        ApiKeyLabelText.Text = Tr("ApiKeyLabel");
        ApiKeyStatusLabelText.Text = Tr("ApiKeyStatusLabel");
        SaveApiKeyButton.Content = Tr("SaveButton");
        CheckApiKeyButton.Content = Tr("CheckButton");
        VoiceSectionTitleText.Text = Tr("VoiceSectionTitle");
        ModelLabelText.Text = Tr("ModelLabel");
        VoiceLabelText.Text = Tr("VoiceLabel");
        SpeedLabelText.Text = Tr("SpeedLabel");
        PreviewVoiceLabelText.Text = Tr("PreviewVoiceLabel");
        PreviewVoiceButton.Content = Tr("PreviewVoiceButton");
        TextPreparationSectionTitleText.Text = Tr("TextPreparationSectionTitle");
        HidePathsLabelText.Text = Tr("HidePathsLabel");
        HidePathsHintText.Text = Tr("HidePathsHint");
        HideCodeBlocksLabelText.Text = Tr("HideCodeBlocksLabel");
        HideCodeBlocksHintText.Text = Tr("HideCodeBlocksHint");
        HideInlineCodeLabelText.Text = Tr("HideInlineCodeLabel");
        HideInlineCodeHintText.Text = Tr("HideInlineCodeHint");
        ShortenLinksLabelText.Text = Tr("ShortenLinksLabel");
        ShortenLinksHintText.Text = Tr("ShortenLinksHint");
        HideSecretsLabelText.Text = Tr("HideSecretsLabel");
        HideSecretsHintText.Text = Tr("HideSecretsHint");
        ShortenHashesLabelText.Text = Tr("ShortenHashesLabel");
        ShortenHashesHintText.Text = Tr("ShortenHashesHint");
        CollapseStackTracesLabelText.Text = Tr("CollapseStackTracesLabel");
        CollapseStackTracesHintText.Text = Tr("CollapseStackTracesHint");
        RemoveMarkdownNoiseLabelText.Text = Tr("RemoveMarkdownNoiseLabel");
        RemoveMarkdownNoiseHintText.Text = Tr("RemoveMarkdownNoiseHint");
        CollapseTablesLabelText.Text = Tr("CollapseTablesLabel");
        CollapseTablesHintText.Text = Tr("CollapseTablesHint");
        CollapseStructuredDataLabelText.Text = Tr("CollapseStructuredDataLabel");
        CollapseStructuredDataHintText.Text = Tr("CollapseStructuredDataHint");
        ShortenShellCommandsLabelText.Text = Tr("ShortenShellCommandsLabel");
        ShortenShellCommandsHintText.Text = Tr("ShortenShellCommandsHint");
        HideLongNumbersLabelText.Text = Tr("HideLongNumbersLabel");
        HideLongNumbersHintText.Text = Tr("HideLongNumbersHint");
        HotkeysPageTitleText.Text = Tr("HotkeysPageTitle");
        HotkeysPageHintText.Text = Tr("HotkeysPageHint");
        HotkeysSectionTitleText.Text = Tr("HotkeysSectionTitle");
        SpeakHotkeyLabelText.Text = Tr("SpeakHotkeyLabel");
        SpeakHotkeyHintText.Text = Tr("SpeakHotkeyHint");
        ClipboardHotkeyLabelText.Text = Tr("ClipboardHotkeyLabel");
        ClipboardHotkeyHintText.Text = Tr("ClipboardHotkeyHint");
        CodexMicHotkeyLabelText.Text = Tr("CodexMicLabel");
        CodexMicHotkeyHintText.Text = Tr("CodexMicHotkeyHint");
        ClearSpeakLatestHotkeyButton.Content = Tr("ResetHotkeyButton");
        ClearClipboardHotkeyButton.Content = Tr("ClearHotkeyButton");
        ClearCodexMicHotkeyButton.Content = Tr("ClearHotkeyButton");
        UpdateHotkeyButtons();
        HotkeyEditorTitleText.Text = Tr("HotkeyEditorTitle");
        HotkeyEditorHintText.Text = Tr("HotkeyEditorHint");
        IntegrationPageTitleText.Text = Tr("IntegrationPageTitle");
        IntegrationPageHintText.Text = Tr("IntegrationPageHint");
        CodexWindowSectionTitleText.Text = Tr("CodexWindowSectionTitle");
        CodexWindowKeywordsLabelText.Text = Tr("CodexWindowKeywordsLabel");
        CodexWindowKeywordsHintText.Text = Tr("CodexWindowKeywordsHint");
        TestCodexWindowButton.Content = Tr("CheckButton");
        CopyIntegrationSectionTitleText.Text = Tr("CopyIntegrationSectionTitle");
        HoverCopyButtonLabelText.Text = Tr("HoverCopyButtonLabel");
        HoverCopyButtonHintText.Text = Tr("HoverCopyButtonHint");
        RestoreClipboardLabelText.Text = Tr("RestoreClipboardLabel");
        RestoreClipboardHintText.Text = Tr("RestoreClipboardHint");
        ClipboardFallbackLabelText.Text = Tr("ClipboardFallbackLabel");
        ClipboardFallbackHintText.Text = Tr("ClipboardFallbackHint");
        MicrophoneIntegrationSectionTitleText.Text = Tr("MicrophoneIntegrationSectionTitle");
        RetryMicrophoneLabelText.Text = Tr("RetryMicrophoneLabel");
        RetryMicrophoneHintText.Text = Tr("RetryMicrophoneHint");
        DiagnosticsSectionTitleText.Text = Tr("DiagnosticsSectionTitle");
        DiagnosticsLabelText.Text = Tr("DiagnosticsLabel");
        DiagnosticsHintText.Text = Tr("DiagnosticsHint");
        TestCopyButton.Content = Tr("FindCopyButton");
        TestMicrophoneButton.Content = Tr("FindMicrophoneButton");
        StopButton.Content = Tr("StopButton");
        ClipboardButton.Content = Tr("ClipboardButton");
        CancelButton.Content = Tr("CancelButton");
        SpeakButton.Content = Tr("SpeakButton");
        AccentHintText.Text = Tr("AccentHint");
        UpdateHeaderTitle();
        ApplyAccessibilityNames();
        RefreshApiKeyStatus();
    }

    private void UpdateHeaderTitle()
    {
        HeaderTitle.Text = _currentPage switch
        {
            "Speech" => Tr("HeaderSpeech"),
            "Hotkeys" => Tr("HeaderHotkeys"),
            "Integration" => Tr("HeaderIntegration"),
            _ => Tr("HeaderGeneral")
        };
    }

    private string Tr(string key)
    {
        var languageIndex = _appSettings.InterfaceLanguage switch
        {
            "uk" => 1,
            "en" => 2,
            _ => 0
        };

        return UiTexts.TryGetValue(key, out var values) && values.Length > languageIndex ? values[languageIndex] : key;
    }
}
