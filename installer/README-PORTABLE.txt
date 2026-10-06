VOICE BUTTON - PORTABLE

Run "Voice Button Portable.exe". No installation or separate .NET runtime is required.

The portable package keeps settings and diagnostics in its local data folder.
The OpenAI API key is never included in this archive. Enter your own key in
Speech settings; Voice Button stores it in Windows Credential Manager for the
current Windows user.

Reusable speech audio is stored outside this folder in the current user's
local application data. The cache keeps no more than 20 completed sessions,
250 MB, or three days and is not included when this portable folder is shared.
Raw dictation recordings are not saved to disk.

The microphone action uses built-in dictation in Codex and ChatGPT. In other
applications, Voice Button records speech, transcribes it with OpenAI, and
attempts to paste the text into the field that had focus when recording began.
The transcript remains available in the clipboard when insertion is uncertain.

Optional Noks integration recognizes dictation that begins with "Noks" or
"Нокс", sends the remaining text to the pinned Noks chat in Codex, and lets
the speaker control read the latest Noks assistant response.

Project: https://github.com/nick-nalivaiko/voice-button
