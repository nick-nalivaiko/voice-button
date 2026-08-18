using System.Collections.Generic;

namespace VoiceButton;

public partial class MainWindow
{
    private static readonly IReadOnlyDictionary<string, string[]> UiTexts = new Dictionary<string, string[]>
    {
        ["NavGeneral"] = new[] { "Общие", "Загальні", "General" },
        ["NavSpeech"] = new[] { "Озвучка", "Озвучення", "Speech" },
        ["NavDictation"] = new[] { "Диктовка", "Диктування", "Dictation" },
        ["NavHotkeys"] = new[] { "Горячие клавиши", "Гарячі клавіші", "Hotkeys" },
        ["NavIntegration"] = new[] { "Интеграция", "Інтеграція", "Integration" },
        ["HeaderGeneral"] = new[] { "Настройки Voice Button", "Налаштування Voice Button", "Voice Button Settings" },
        ["HeaderSpeech"] = new[] { "Настройки озвучки", "Налаштування озвучення", "Speech Settings" },
        ["HeaderDictation"] = new[] { "Настройки диктовки", "Налаштування диктування", "Dictation Settings" },
        ["HeaderHotkeys"] = new[] { "Горячие клавиши", "Гарячі клавіші", "Hotkeys" },
        ["HeaderIntegration"] = new[] { "Codex и ChatGPT", "Codex і ChatGPT", "Codex and ChatGPT" },
        ["GeneralPageTitle"] = new[] { "Общие", "Загальні", "General" },
        ["GeneralPageHint"] = new[] { "Поведение окна, плавающая кнопка и запуск приложения.", "Поведінка вікна, плаваюча кнопка і запуск програми.", "Window behavior, floating button, and application startup." },
        ["GeneralSectionTitle"] = new[] { "Поведение", "Поведінка", "Behavior" },
        ["InterfaceLanguageLabel"] = new[] { "Язык интерфейса", "Мова інтерфейсу", "Interface language" },
        ["InterfaceLanguageHint"] = new[] { "Переводит основные элементы окна настроек.", "Перекладає основні елементи вікна налаштувань.", "Translates the main settings window controls." },
        ["FloatingButtonLabel"] = new[] { "Показывать плавающую кнопку", "Показувати плаваючу кнопку", "Show floating button" },
        ["FloatingButtonHint"] = new[] { "Микрофон слева, новый ответ справа; ПКМ по динамику озвучивает clipboard. Волна появляется для сохраненного аудио.", "Мікрофон ліворуч, нова відповідь праворуч; ПКМ по динаміку озвучує clipboard. Хвиля з'являється для збереженого аудіо.", "Microphone on the left, new answer on the right; right-click the speaker to speak the clipboard. The waveform appears for saved audio." },
        ["MinimizeToTrayLabel"] = new[] { "Закрывать в трей", "Закривати в трей", "Close to tray" },
        ["MinimizeToTrayHint"] = new[] { "Если включено, крестик прячет окно в трей. Верхняя кнопка всегда сворачивает его в панель задач.", "Якщо ввімкнено, хрестик ховає вікно в трей. Верхня кнопка завжди згортає його на панель завдань.", "When enabled, Close hides the window in the tray. The top Minimize button always sends it to the taskbar." },
        ["StartWithWindowsLabel"] = new[] { "Запускать вместе с Windows", "Запускати разом із Windows", "Start with Windows" },
        ["StartWithWindowsHint"] = new[] { "Добавляет Voice Button в автозапуск текущего пользователя Windows.", "Додає Voice Button в автозапуск поточного користувача Windows.", "Adds Voice Button to the current Windows user's startup apps." },
        ["RememberFloatingPositionLabel"] = new[] { "Запоминать позицию кнопки", "Запам'ятовувати позицію кнопки", "Remember button position" },
        ["RememberFloatingPositionHint"] = new[] { "Если выключить, кнопка каждый запуск возвращается в правый нижний угол.", "Якщо вимкнути, кнопка під час кожного запуску повертається у правий нижній кут.", "When off, the button returns to the lower-right corner on each launch." },
        ["ResetFloatingPositionButton"] = new[] { "Сбросить позицию кнопки", "Скинути позицію кнопки", "Reset button position" },
        ["SpeechPageTitle"] = new[] { "Озвучка", "Озвучення", "Speech" },
        ["SpeechPageHint"] = new[] { "OpenAI, модель, голос и подготовка текста перед озвучкой.", "OpenAI, модель, голос і підготовка тексту перед озвученням.", "OpenAI, model, voice, and text preparation before playback." },
        ["DictationPageTitle"] = new[] { "Диктовка", "Диктування", "Dictation" },
        ["DictationPageHint"] = new[] { "Запись с микрофона, распознавание и вставка текста в активное приложение.", "Запис із мікрофона, розпізнавання та вставлення тексту в активну програму.", "Microphone recording, transcription, and text insertion into the active application." },
        ["RecognitionSectionTitle"] = new[] { "Распознавание", "Розпізнавання", "Recognition" },
        ["TranscriptionProviderLabel"] = new[] { "Поставщик", "Постачальник", "Provider" },
        ["TranscriptionModelLabel"] = new[] { "Модель распознавания", "Модель розпізнавання", "Transcription model" },
        ["TranscriptionModelHint"] = new[] { "Основная модель точнее, mini дешевле.", "Основна модель точніша, mini дешевша.", "The main model is more accurate; mini costs less." },
        ["TranscriptionLanguageLabel"] = new[] { "Язык речи", "Мова мовлення", "Spoken language" },
        ["TranscriptionLanguageHint"] = new[] { "Авто подходит, если языки меняются.", "Авто підходить, якщо мови змінюються.", "Auto works best when spoken languages change." },
        ["TranscriptionLanguageAuto"] = new[] { "Автоматически", "Автоматично", "Automatic" },
        ["TranscriptionLanguageRussian"] = new[] { "Русский", "Російська", "Russian" },
        ["TranscriptionLanguageUkrainian"] = new[] { "Украинский", "Українська", "Ukrainian" },
        ["TranscriptionLanguageEnglish"] = new[] { "Английский", "Англійська", "English" },
        ["DictationKeyLabel"] = new[] { "OpenAI API key", "OpenAI API key", "OpenAI API key" },
        ["DictationKeyHint"] = new[] { "Используется ключ из раздела «Озвучка».", "Використовується ключ із розділу «Озвучення».", "Uses the key from the Speech section." },
        ["DictationBehaviorSectionTitle"] = new[] { "Поведение", "Поведінка", "Behavior" },
        ["InsertDictationLabel"] = new[] { "Вставлять текст автоматически", "Вставляти текст автоматично", "Insert text automatically" },
        ["InsertDictationHint"] = new[] { "Если курсор стоит в поле ввода, текст вставится туда; иначе останется в clipboard.", "Якщо курсор стоїть у полі вводу, текст вставиться туди; інакше залишиться в clipboard.", "If an input field has focus, text is inserted there; otherwise it remains in the clipboard." },
        ["RestoreDictationClipboardLabel"] = new[] { "Восстанавливать clipboard после вставки", "Відновлювати clipboard після вставлення", "Restore clipboard after insertion" },
        ["RestoreDictationClipboardHint"] = new[] { "После успешной вставки возвращает прежнее содержимое буфера обмена.", "Після успішного вставлення повертає попередній вміст буфера обміну.", "Restores the previous clipboard contents after a successful insertion." },
        ["DictationRoutingTitle"] = new[] { "Как работает кнопка микрофона", "Як працює кнопка мікрофона", "How the microphone button works" },
        ["DictationRoutingHint"] = new[] { "В Codex и ChatGPT запускается встроенный микрофон. В Telegram, Блокноте, браузере и других приложениях Voice Button записывает речь и вставляет готовый текст.", "У Codex і ChatGPT запускається вбудований мікрофон. У Telegram, Блокноті, браузері та інших програмах Voice Button записує мовлення й вставляє готовий текст.", "Codex and ChatGPT use their built-in microphone. In Telegram, Notepad, browsers, and other apps, Voice Button records speech and inserts the finished text." },
        ["DictationSettingsSaved"] = new[] { "Настройки диктовки сохранены.", "Налаштування диктування збережено.", "Dictation settings saved." },
        ["DictationBusy"] = new[] { "Диктовка занята", "Диктування зайняте", "Dictation is busy" },
        ["DictationBusyDetail"] = new[] { "Заверши текущее действие или нажми Стоп.", "Заверши поточну дію або натисни Стоп.", "Finish the current action or press Stop." },
        ["DictationListening"] = new[] { "Слушаю", "Слухаю", "Listening" },
        ["DictationListeningInsertDetail"] = new[] { "Нажми микрофон ещё раз: текст будет вставлен в выбранное поле.", "Натисни мікрофон ще раз: текст буде вставлено у вибране поле.", "Press the microphone again to insert text into the selected field." },
        ["DictationListeningClipboardDetail"] = new[] { "Поле ввода не выбрано: результат останется в clipboard.", "Поле вводу не вибрано: результат залишиться в clipboard.", "No input field is selected; the result will remain in the clipboard." },
        ["DictationTranscribing"] = new[] { "Распознаю речь", "Розпізнаю мовлення", "Transcribing" },
        ["DictationTranscribingDetail"] = new[] { "Отправляю запись в OpenAI и готовлю текст.", "Надсилаю запис до OpenAI та готую текст.", "Sending the recording to OpenAI and preparing text." },
        ["DictationInserted"] = new[] { "Текст вставлен", "Текст вставлено", "Text inserted" },
        ["DictationInsertedDetail"] = new[] { "Распознанный текст добавлен в выбранное поле.", "Розпізнаний текст додано у вибране поле.", "The transcript was added to the selected input field." },
        ["DictationPasteAttempted"] = new[] { "Вставка отправлена", "Вставлення надіслано", "Paste sent" },
        ["DictationPasteAttemptedDetail"] = new[] { "Команда вставки отправлена приложению. Текст также оставлен в clipboard.", "Команду вставлення надіслано програмі. Текст також залишено в clipboard.", "The paste command was sent to the app. The text also remains in the clipboard." },
        ["DictationCopied"] = new[] { "Текст в clipboard", "Текст у clipboard", "Text copied" },
        ["DictationCopiedDetail"] = new[] { "Поле ввода не найдено. Текст можно вставить вручную.", "Поле вводу не знайдено. Текст можна вставити вручну.", "No input field was found. Paste the text manually." },
        ["DictationCanceled"] = new[] { "Диктовка остановлена", "Диктування зупинено", "Dictation stopped" },
        ["DictationCanceledDetail"] = new[] { "Запись или распознавание отменено.", "Запис або розпізнавання скасовано.", "Recording or transcription was canceled." },
        ["FloatingMicTooltip"] = new[] { "Микрофон / новый ответ; ПКМ по динамику: озвучить clipboard", "Мікрофон / нова відповідь; ПКМ по динаміку: озвучити clipboard", "Microphone / new answer; right-click speaker: speak clipboard" },
        ["FloatingResumeTooltip"] = new[] { "Микрофон / продолжить аудио / новый ответ; ПКМ по динамику: clipboard", "Мікрофон / продовжити аудіо / нова відповідь; ПКМ по динаміку: clipboard", "Microphone / resume audio / new answer; right-click speaker: clipboard" },
        ["FloatingRecordingTooltip"] = new[] { "Остановить запись и вставить текст", "Зупинити запис і вставити текст", "Stop recording and insert text" },
        ["FloatingProcessingTooltip"] = new[] { "Распознаю речь", "Розпізнаю мовлення", "Transcribing speech" },
        ["FloatingPauseTooltip"] = new[] { "Продолжить / перемотка / стоп", "Продовжити / перемотування / стоп", "Resume / seek / stop" },
        ["FloatingPlayTooltip"] = new[] { "Пауза / перемотка / стоп", "Пауза / перемотування / стоп", "Pause / seek / stop" },
        ["FloatingLiveOffTooltip"] = new[] { "Включить озвучку хода работы Codex", "Увімкнути озвучення ходу роботи Codex", "Enable Codex work narration" },
        ["FloatingLiveOnTooltip"] = new[] { "Выключить озвучку хода работы Codex", "Вимкнути озвучення ходу роботи Codex", "Disable Codex work narration" },
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
        ["CodexMicLabel"] = new[] { "Микрофон / диктовка", "Мікрофон / диктування", "Microphone / dictation" },
        ["CodexMicHotkeyHint"] = new[] { "В Codex и ChatGPT включает встроенный микрофон, в остальных приложениях запускает диктовку.", "У Codex і ChatGPT вмикає вбудований мікрофон, в інших програмах запускає диктування.", "Uses the built-in microphone in Codex and ChatGPT, and starts dictation in other apps." },
        ["SendVoiceHotkeyLabel"] = new[] { "Отправить голосовой ввод", "Надіслати голосове введення", "Send voice input" },
        ["SendVoiceHotkeyHint"] = new[] { "Нажимает Enter в активном Codex или ChatGPT и отправляет записанный голосовой ввод.", "Натискає Enter в активному Codex або ChatGPT і надсилає записане голосове введення.", "Presses Enter in the active Codex or ChatGPT window to send recorded voice input." },
        ["SendVoiceRequiresActiveAppDetail"] = new[] { "Сначала сделай активным окно Codex или ChatGPT.", "Спочатку зроби активним вікно Codex або ChatGPT.", "Focus a Codex or ChatGPT window first." },
        ["SendVoiceActiveAppChangedDetail"] = new[] { "Отпусти клавиши хоткея, не переключая активное окно.", "Відпусти клавіші хоткея, не перемикаючи активне вікно.", "Release the shortcut keys without switching the active window." },
        ["SendVoiceFailedDetail"] = new[] { "Windows не удалось отправить клавишу Enter.", "Windows не вдалося надіслати клавішу Enter.", "Windows could not send the Enter key." },
        ["VoiceInputSent"] = new[] { "Голосовой ввод отправлен", "Голосове введення надіслано", "Voice input sent" },
        ["VoiceInputSentDetail"] = new[] { "Enter передан в {0}.", "Enter передано в {0}.", "Enter was sent to {0}." },
        ["UnassignedHotkey"] = new[] { "Не назначено", "Не призначено", "Not assigned" },
        ["ResetHotkeyButton"] = new[] { "Сброс", "Скинути", "Reset" },
        ["ClearHotkeyButton"] = new[] { "Очистить", "Очистити", "Clear" },
        ["PressHotkeyButton"] = new[] { "Нажмите сочетание", "Натисніть комбінацію", "Press shortcut" },
        ["HotkeyEditorTitle"] = new[] { "Как записать сочетание", "Як записати комбінацію", "How to record a shortcut" },
        ["HotkeyEditorHint"] = new[] { "Нажмите поле сочетания, затем зажмите, например, Ctrl + Alt + V. Для глобального хоткея нужна хотя бы одна служебная клавиша.", "Натисніть поле комбінації, потім затисніть, наприклад, Ctrl + Alt + V. Для глобального хоткея потрібна хоча б одна службова клавіша.", "Click a shortcut field, then press something like Ctrl + Alt + V. A global shortcut needs at least one modifier key." },
        ["IntegrationPageTitle"] = new[] { "Интеграция", "Інтеграція", "Integration" },
        ["IntegrationPageHint"] = new[] { "Автовыбор Codex или ChatGPT, Copy/clipboard, микрофон и live-озвучка Codex.", "Автовибір Codex або ChatGPT, Copy/clipboard, мікрофон і live-озвучення Codex.", "Automatic Codex or ChatGPT selection, Copy/clipboard, microphone, and live Codex narration." },
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
        ["LiveNarrationSectionTitle"] = new[] { "Озвучка хода работы Codex", "Озвучення ходу роботи Codex", "Codex work narration" },
        ["LiveNarrationLabel"] = new[] { "Включить live-озвучку", "Увімкнути live-озвучення", "Enable live narration" },
        ["LiveNarrationHint"] = new[] { "Показывает маленькую кнопку у динамика и позволяет озвучивать новые абзацы хода работы Codex.", "Показує маленьку кнопку біля динаміка та дозволяє озвучувати нові абзаци ходу роботи Codex.", "Shows a small speaker control that narrates new Codex work paragraphs." },
        ["LiveNarrationOn"] = new[] { "Live-озвучка включена", "Live-озвучення увімкнено", "Live narration enabled" },
        ["LiveNarrationOnDetail"] = new[] { "Читаю текущие и новые абзацы хода работы Codex.", "Читаю поточні та нові абзаци ходу роботи Codex.", "Current and new Codex work paragraphs will be spoken." },
        ["LiveNarrationOff"] = new[] { "Live-озвучка выключена", "Live-озвучення вимкнено", "Live narration disabled" },
        ["LiveNarrationOffDetail"] = new[] { "Автоматическая озвучка новых абзацев остановлена.", "Автоматичне озвучення нових абзаців зупинено.", "Automatic narration of new paragraphs has stopped." },
        ["LiveNarrationSpeaking"] = new[] { "Озвучиваю ход работы", "Озвучую хід роботи", "Narrating Codex work" },
        ["LiveNarrationParagraphDetail"] = new[] { "Текущий абзац Codex.", "Поточний абзац Codex.", "Current Codex paragraph." },
        ["LiveNarrationWaiting"] = new[] { "Live-озвучка", "Live-озвучення", "Live narration" },
        ["LiveNarrationWaitingDetail"] = new[] { "Жду следующий абзац Codex.", "Чекаю наступний абзац Codex.", "Waiting for the next Codex paragraph." },
        ["LiveNarrationStoppedDetail"] = new[] { "Текущая очередь пропущена. Жду следующий новый абзац.", "Поточну чергу пропущено. Чекаю наступний новий абзац.", "The current queue was skipped. Waiting for the next new paragraph." },
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
        ["MinimizeToTrayButton"] = new[] { "Свернуть в трей", "Згорнути в трей", "Minimize to tray" },
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
        DictationNavText.Text = Tr("NavDictation");
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
        DictationPageTitleText.Text = Tr("DictationPageTitle");
        DictationPageHintText.Text = Tr("DictationPageHint");
        RecognitionSectionTitleText.Text = Tr("RecognitionSectionTitle");
        TranscriptionProviderLabelText.Text = Tr("TranscriptionProviderLabel");
        TranscriptionModelLabelText.Text = Tr("TranscriptionModelLabel");
        TranscriptionModelHintText.Text = Tr("TranscriptionModelHint");
        TranscriptionLanguageLabelText.Text = Tr("TranscriptionLanguageLabel");
        TranscriptionLanguageHintText.Text = Tr("TranscriptionLanguageHint");
        DictationKeyLabelText.Text = Tr("DictationKeyLabel");
        DictationKeyHintText.Text = Tr("DictationKeyHint");
        DictationBehaviorSectionTitleText.Text = Tr("DictationBehaviorSectionTitle");
        InsertDictationLabelText.Text = Tr("InsertDictationLabel");
        InsertDictationHintText.Text = Tr("InsertDictationHint");
        RestoreDictationClipboardLabelText.Text = Tr("RestoreDictationClipboardLabel");
        RestoreDictationClipboardHintText.Text = Tr("RestoreDictationClipboardHint");
        DictationRoutingTitleText.Text = Tr("DictationRoutingTitle");
        DictationRoutingHintText.Text = Tr("DictationRoutingHint");
        RefreshTranscriptionLanguageOptions();
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
        SendVoiceHotkeyLabelText.Text = Tr("SendVoiceHotkeyLabel");
        SendVoiceHotkeyHintText.Text = Tr("SendVoiceHotkeyHint");
        ClearSpeakLatestHotkeyButton.Content = Tr("ResetHotkeyButton");
        ClearClipboardHotkeyButton.Content = Tr("ClearHotkeyButton");
        ClearCodexMicHotkeyButton.Content = Tr("ClearHotkeyButton");
        ClearSendVoiceHotkeyButton.Content = Tr("ClearHotkeyButton");
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
        LiveNarrationSectionTitleText.Text = Tr("LiveNarrationSectionTitle");
        LiveNarrationLabelText.Text = Tr("LiveNarrationLabel");
        LiveNarrationHintText.Text = Tr("LiveNarrationHint");
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
        TrayMinimizeButton.Content = Tr("MinimizeToTrayButton");
        SpeakButton.Content = Tr("SpeakButton");
        AccentHintText.Text = Tr("AccentHint");
        UpdateHeaderTitle();
        ApplyFloatingButtonLocalization();
        ApplyAccessibilityNames();
        RefreshApiKeyStatus();
    }

    private void UpdateHeaderTitle()
    {
        HeaderTitle.Text = _currentPage switch
        {
            "Speech" => Tr("HeaderSpeech"),
            "Dictation" => Tr("HeaderDictation"),
            "Hotkeys" => Tr("HeaderHotkeys"),
            "Integration" => Tr("HeaderIntegration"),
            _ => Tr("HeaderGeneral")
        };
    }

    private void ApplyFloatingButtonLocalization()
    {
        _floatingButtonWindow?.SetLocalizedTooltips(
            Tr("FloatingMicTooltip"),
            Tr("FloatingResumeTooltip"),
            Tr("FloatingRecordingTooltip"),
            Tr("FloatingProcessingTooltip"),
            Tr("FloatingPauseTooltip"),
            Tr("FloatingPlayTooltip"),
            Tr("FloatingLiveOffTooltip"),
            Tr("FloatingLiveOnTooltip"));
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
