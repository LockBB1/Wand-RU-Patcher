using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace WandRuInstaller.Core.Runtime;

internal sealed class RuntimeInstallation
{
    const string ActivationName = "select-russian.pending";
    readonly string _root;
    readonly string _storage;
    readonly string[] _folders;
    readonly RuntimeShortcuts _shortcuts;

    internal RuntimeInstallation(string root, string storage, string[] shortcutFolders)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _storage = Path.GetFullPath(storage);
        _folders = shortcutFolders;
        _shortcuts = new RuntimeShortcuts(_root, _storage, _folders);
    }

    public static RuntimeInstallation ForRoot(string root)
    {
        var identity = Identity(root);
        var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new RuntimeInstallation(root, Path.Combine(data, "WandRuInstaller", "runtime", identity), new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(roaming, "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar")
        });
    }

    public bool HasState => _shortcuts.HasState;
    public bool IsInstalled => _shortcuts.IsInstalled && _shortcuts.Paths.All(path => IsOwnHelper(ShellShortcut.Read(path).Target));

    public static bool Required(string appDir)
    {
        var minimum = new Version(12, 61, 0);
        var byFolder = Version.TryParse(WandLocator.VersionOf(appDir), out var folder) && folder >= minimum;
        var root = Path.GetDirectoryName(Path.GetFullPath(appDir));
        if (root is not null && WandLocator.FindAppDirs(root).Any(directory => Version.TryParse(WandLocator.VersionOf(directory), out var version) && version >= minimum))
            return true; // A newer auxiliary service can serve a pinned older client too.
        if (!File.Exists(Path.Combine(appDir, "Wand.exe"))) return byFolder;
        var file = FileVersionInfo.GetVersionInfo(Path.Combine(appDir, "Wand.exe"));
        return new Version(Math.Max(0, file.FileMajorPart), Math.Max(0, file.FileMinorPart), Math.Max(0, file.FileBuildPart)) >= minimum ||
            byFolder;
    }

    public void Install(string appDir, Action<string>? log = null)
    {
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(appDir)), _root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Выбранная версия находится вне установки Wand.");
        if (WandLocator.Manifest(appDir) is not null && RuPatcher.IsAsarPatched(Path.Combine(appDir, "resources", "app.asar")))
            RuUnpatcher.Restore(appDir, log);
        var enhanced = RuntimeHost.ValidateOriginal(appDir);
        var helper = Deploy();
        RuntimeEnhancer.Pin(_storage, enhanced is null ? RuntimeEnhancer.CleanState : RuntimeEnhancer.Snapshot(appDir, enhanced.Value), true);
        var paths = _shortcuts.Paths.Concat(_shortcuts.Discover()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0)
        {
            if (_folders.Length == 0 || string.IsNullOrWhiteSpace(_folders[0])) throw new IOException("Недоступна папка ярлыков пользователя.");
            var fallback = Path.Combine(_folders[0], "Wand.lnk");
            if (File.Exists(fallback)) throw new InvalidOperationException("Существующий ярлык Wand принадлежит другой установке.");
            paths = new[] { fallback };
        }
        var marker = Path.Combine(_storage, ActivationName);
        RuntimeShortcuts.EnsureNoLinks(marker);
        var temporary = marker + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            RuntimeShortcuts.WriteDurable(temporary, Encoding.UTF8.GetBytes(Identity(_root)));
            RuntimeShortcuts.Replace(temporary, marker);
        }
        finally { File.Delete(temporary); }
        _shortcuts.Install(helper, paths);
        if (!IsInstalled) throw new IOException("Не удалось проверить установку перевода через ярлык Wand.");
    }

    public void Restore() => _shortcuts.Restore();

    public static (string Path, string Identity)? CurrentActivation(string root) => ActivationForExecutable(root, Bundle()[0]);

    internal static (string Path, string Identity)? ActivationForExecutable(string root, string executable)
    {
        var package = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var packages = Directory.GetParent(package);
        var name = Path.GetFileName(package);
        if (!Path.GetFileName(executable).Equals("WandRuInstaller.exe", StringComparison.OrdinalIgnoreCase) ||
            packages?.Name != "packages" || packages.Parent is null || name.Length != 64 || name.Any(c => !Uri.IsHexDigit(c))) return null;
        var storage = packages.Parent.FullName;
        var vendor = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (storage.Equals(vendor, StringComparison.OrdinalIgnoreCase) ||
            storage.StartsWith(Path.EndsInDirectorySeparator(vendor) ? vendor : vendor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Данные активации WRP должны храниться вне папки Wand.");
        var path = Path.Combine(storage, ActivationName);
        RuntimeShortcuts.EnsureNoLinks(path);
        return (path, Identity(root));
    }

    static string Identity(string root) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\').ToUpperInvariant())));

    public ProcessStartInfo CreateStartInfo()
    {
        if (!IsInstalled) throw new InvalidOperationException("Ярлык перевода Wand изменён или повреждён. Повторите установку.");
        var link = ShellShortcut.Read(_shortcuts.Paths[0]);
        return new ProcessStartInfo(link.Target) { Arguments = link.Arguments, UseShellExecute = false, CreateNoWindow = true };
    }

    bool IsOwnHelper(string path)
    {
        if (!File.Exists(path) || !Path.GetFileName(path).Equals("WandRuInstaller.exe", StringComparison.OrdinalIgnoreCase)) return false;
        var package = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (!string.Equals(Path.GetDirectoryName(package), Path.Combine(_storage, "packages"), StringComparison.OrdinalIgnoreCase)) return false;
        RuntimeShortcuts.EnsureNoLinks(package);
        var names = new[] { "WandRuInstaller.exe", "WandRuInstaller.dll", "WandRuInstaller.deps.json", "WandRuInstaller.runtimeconfig.json", "Newtonsoft.Json.dll" };
        var files = names.Select(name => Path.Combine(package, name)).ToArray();
        if (!files.Skip(1).Any(File.Exists)) files = files[..1];
        if (files.Any(file => !File.Exists(file))) return false;
        foreach (var file in files) RuntimeShortcuts.EnsureNoLinks(file);
        return Path.GetFileName(package) == PackageIdentity(files.Select(HashFile).ToArray(), files.Select(Path.GetFileName).Select(name => name!).ToArray());
    }

    internal string Deploy()
    {
        RuntimeShortcuts.EnsureNoLinks(_storage);
        if (_storage.Equals(_root, StringComparison.OrdinalIgnoreCase) ||
            _storage.StartsWith(Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Помощник WRP должен храниться вне папки Wand.");
        var files = Bundle();
        var hashes = files.Select(file => HashFile(file)).ToArray();
        var names = files.Select((file, index) => index == 0 ? "WandRuInstaller.exe" : Path.GetFileName(file)).ToArray();
        var package = Path.Combine(_storage, "packages", PackageIdentity(hashes, names));
        RuntimeShortcuts.EnsureNoLinks(package);
        Directory.CreateDirectory(package);
        for (var i = 0; i < files.Length; i++)
        {
            var destination = Path.Combine(package, i == 0 ? "WandRuInstaller.exe" : Path.GetFileName(files[i]));
            RuntimeShortcuts.EnsureNoLinks(destination);
            if (File.Exists(destination) && HashFile(destination).SequenceEqual(hashes[i])) continue;
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(files[i], temporary);
                if (!HashFile(temporary).SequenceEqual(hashes[i])) throw new IOException("Не удалось проверить копию помощника WRP.");
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
                if (!HashFile(destination).SequenceEqual(hashes[i])) throw new IOException("Не удалось проверить установленную копию помощника WRP.");
            }
            finally { File.Delete(temporary); }
        }
        return Path.Combine(package, "WandRuInstaller.exe");
    }

    static string PackageIdentity(byte[][] hashes, string[] names)
    {
        using var identity = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var i = 0; i < hashes.Length; i++)
        {
            identity.AppendData(Encoding.UTF8.GetBytes(names[i]));
            identity.AppendData(hashes[i]);
        }
        return Convert.ToHexString(identity.GetHashAndReset());
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "Empty assembly location selects the running single-file product image.")]
    static string[] Bundle()
    {
        var assembly = typeof(RuntimeInstallation).Assembly;
        var location = assembly.Location;
        if (location.Length == 0)
        {
            if (Assembly.GetEntryAssembly() != assembly || Environment.ProcessPath is not { } executable)
                throw new IOException("Не удалось найти исполняемый файл WRP.");
            return new[] { executable };
        }
        var directory = Path.GetDirectoryName(location)!;
        return new[]
        {
            Path.ChangeExtension(location, ".exe"), location,
            Path.ChangeExtension(location, ".deps.json"), Path.ChangeExtension(location, ".runtimeconfig.json"),
            Path.Combine(directory, "Newtonsoft.Json.dll")
        };
    }

    static byte[] HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
