using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace C0_0nverter;

internal static class ShellContextMenu
{
    private const string VideoMenuKey = @"Software\Classes\SystemFileAssociations\video\shell\C0_0nverter";
    private const string RegistrationIndex = @"Software\C0_0nverter\ExplorerMenu";
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".wmv", ".mpg", ".mpeg", ".3gp", ".flv", ".ts", ".m2ts"];

    public static void Register()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return;

        // Replace only our own per-user menu entries; file defaults are never changed.
        UnregisterEntries(refreshShell: false);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { VideoMenuKey };
        foreach (var extension in VideoExtensions)
        {
            using var association = Registry.ClassesRoot.OpenSubKey(extension);
            var progId = association?.GetValue("")?.ToString();
            if (!string.IsNullOrWhiteSpace(progId))
                roots.Add($@"Software\Classes\{progId}\shell\C0_0nverter");
        }

        var index = new List<string>();
        foreach (var root in roots)
        {
            CreateMenu(root, exe);
            index.Add(root);
            var configure = root[..root.LastIndexOf('\\')] + @"\C0_0nverter_Configure";
            CreateConfigureVerb(configure, exe);
            index.Add(configure);
        }
        SaveIndex(index);
        NotifyShell();
    }

    public static void Unregister() => UnregisterEntries(refreshShell: true);

    private static void CreateMenu(string menuKey, string exe)
    {
        Registry.CurrentUser.DeleteSubKeyTree(menuKey, throwOnMissingSubKey: false);
        using (var menu = Registry.CurrentUser.CreateSubKey(menuKey, writable: true))
        {
            menu.SetValue("MUIVerb", "C0_0nverter");
            menu.SetValue("Icon", $"\"{exe}\",0");
        }

        RegisterVerb(menuKey, "Configure", "Выбрать формат и настройки…", exe, "--from-explorer \"%1\"");
        RegisterVerb(menuKey, "MP4", "Конвертировать в MP4", exe, "--from-explorer \"%1\" --auto-format MP4");
        RegisterVerb(menuKey, "MKV", "Конвертировать в MKV", exe, "--from-explorer \"%1\" --auto-format MKV");
        RegisterVerb(menuKey, "WebM", "Конвертировать в WebM", exe, "--from-explorer \"%1\" --auto-format WebM");
        RegisterVerb(menuKey, "MP3", "Извлечь звук в MP3", exe, "--from-explorer \"%1\" --auto-format MP3");
    }

    private static void RegisterVerb(string menuKey, string verb, string caption, string exe, string arguments)
    {
        using var key = Registry.CurrentUser.CreateSubKey($"{menuKey}\\ExtendedSubCommandsKey\\Shell\\{verb}", writable: true);
        key.SetValue("MUIVerb", caption);
        key.SetValue("Icon", $"\"{exe}\",0");
        using var command = key.CreateSubKey("command", writable: true);
        command.SetValue("", $"\"{exe}\" {arguments}", RegistryValueKind.String);
    }

    private static void CreateConfigureVerb(string keyPath, string exe)
    {
        Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue("MUIVerb", "Выбрать формат в C0_0nverter…");
        key.SetValue("Icon", $"\"{exe}\",0");
        using var command = key.CreateSubKey("command", writable: true);
        command.SetValue("", $"\"{exe}\" --from-explorer \"%1\"", RegistryValueKind.String);
    }

    private static void SaveIndex(IEnumerable<string> menuKeys)
    {
        Registry.CurrentUser.DeleteSubKeyTree(RegistrationIndex, throwOnMissingSubKey: false);
        using var index = Registry.CurrentUser.CreateSubKey(RegistrationIndex, writable: true);
        var i = 0;
        foreach (var key in menuKeys) index.SetValue($"Key{i++}", key, RegistryValueKind.String);
    }

    private static void UnregisterEntries(bool refreshShell)
    {
        using (var index = Registry.CurrentUser.OpenSubKey(RegistrationIndex))
        {
            if (index is not null)
                foreach (var value in index.GetValueNames())
                    if (index.GetValue(value) is string key && key.StartsWith(@"Software\Classes\", StringComparison.OrdinalIgnoreCase))
                        Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(VideoMenuKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(RegistrationIndex, throwOnMissingSubKey: false);
        if (refreshShell) NotifyShell();
    }

    private static void NotifyShell() => SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
