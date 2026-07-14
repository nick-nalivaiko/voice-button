using System.IO;

namespace VoiceButton.Services;

public static class AppStorage
{
    private const string PortableMarkerFileName = "portable.mode";

    public static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, PortableMarkerFileName));

    public static string DataDirectory => IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceButton");
}
