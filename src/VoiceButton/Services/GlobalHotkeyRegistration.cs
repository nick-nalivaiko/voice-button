using VoiceButton.Models;

namespace VoiceButton.Services;

public sealed record GlobalHotkeyRegistration(string Id, string Label, HotkeyGesture Gesture);
