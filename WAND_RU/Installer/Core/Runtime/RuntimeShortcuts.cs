using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WandRuInstaller.Core.Runtime;

internal sealed class RuntimeShortcuts(string wandRoot, string stateDirectory, IReadOnlyList<string> shortcutFolders)
{
    const int MaxLinks = 512, MaxStateBytes = 16 * 1024 * 1024, MaxLinkBytes = 1024 * 1024;
    readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(wandRoot));
    readonly string _stateDirectory = Path.GetFullPath(stateDirectory);
    string StatePath => Path.Combine(_stateDirectory, "shortcuts.json");
    public bool HasState => File.Exists(StatePath);
    public string[] Paths => Load()?.Links.Select(link => link.Path).ToArray() ?? Array.Empty<string>();
    public bool IsInstalled => Load() is { Links.Count: > 0 } state &&
        state.Links.All(link => File.Exists(link.Path) && Hash(ReadLink(link.Path)) == link.WrittenHash);

    public IEnumerable<string> Discover()
    {
        foreach (var folder in shortcutFolders.Where(Directory.Exists))
            foreach (var path in Directory.EnumerateFiles(folder, "*.lnk", new EnumerationOptions
            { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            {
                bool ours;
                try { ours = IsWand(ShellShortcut.Read(path).Target); }
                catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException) { continue; }
                if (ours) yield return path;
            }
    }

    public void Install(string helper, IEnumerable<string> shortcuts)
    {
        using var operation = AcquireLock();
        var state = Load() ?? new State { Root = _root };
        var paths = shortcuts.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (state.Links.Select(link => link.Path).Concat(paths).Distinct(StringComparer.OrdinalIgnoreCase).Count() > MaxLinks)
            throw new InvalidOperationException("Слишком много ярлыков Wand. Установка отменена до изменения ярлыков.");
        var originalBytes = state.Links.Sum(link => (long)(link.Original?.Length ?? 0));
        var plans = new List<(Link Link, string Temporary)>();
        try
        {
            foreach (var path in paths)
            {
                ValidatePath(path);
                var previous = state.Links.SingleOrDefault(link => string.Equals(link.Path, path, StringComparison.OrdinalIgnoreCase));
                byte[]? original;
                string arguments;
                if (previous is not null)
                {
                    RequireUnchanged(previous);
                    original = previous.Original;
                    arguments = previous.Arguments;
                }
                else if (File.Exists(path))
                {
                    var info = ShellShortcut.Read(path);
                    if (!IsWand(info.Target)) throw new InvalidOperationException("Ярлык больше не принадлежит этой установке Wand.");
                    original = ReadLink(path);
                    originalBytes += original.Length;
                    if (originalBytes > MaxStateBytes) throw new InvalidOperationException("Слишком большой объём данных отката ярлыков Wand.");
                    arguments = info.Arguments;
                }
                else { original = null; arguments = ""; }
                var temporary = path + ".wrp-" + Guid.NewGuid().ToString("N") + ".lnk";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    ShellShortcut.Redirect(File.Exists(path) ? path : null, temporary, helper,
                        "--runtime-launch " + Quote(_root) + (arguments.Length == 0 ? "" : " -- " + arguments), Path.Combine(_root, "Wand.exe"));
                    plans.Add((new Link { Path = path, Original = original, Arguments = arguments,
                        WrittenHash = Hash(ReadLink(temporary)), PriorHash = previous is null ? null : new string('F', 64) }, temporary));
                }
                catch { File.Delete(temporary); throw; }
            }
            if (plans.Count == 0) throw new InvalidOperationException("Не найден ярлык Wand для установки перевода.");
            var replacing = plans.Select(plan => plan.Link.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var complete = new State { Root = _root, Links = state.Links.Where(link => !replacing.Contains(link.Path)).Concat(plans.Select(plan => plan.Link)).ToList() };
            Serialize(complete);
            foreach (var plan in plans)
            {
                var previous = state.Links.SingleOrDefault(link => string.Equals(link.Path, plan.Link.Path, StringComparison.OrdinalIgnoreCase));
                if (previous is not null)
                {
                    RequireUnchanged(previous);
                    plan.Link.PriorHash = File.Exists(plan.Link.Path) ? Hash(ReadLink(plan.Link.Path)) : null;
                }
                else if (File.Exists(plan.Link.Path) && (plan.Link.Original is null || Hash(ReadLink(plan.Link.Path)) != Hash(plan.Link.Original)))
                    throw new InvalidOperationException("Ярлык изменился во время установки. Повторите установку.");
                state.Links.RemoveAll(link => string.Equals(link.Path, plan.Link.Path, StringComparison.OrdinalIgnoreCase));
                state.Links.Add(plan.Link);
                Save(state); // Durable undo exists before replacing any shortcut.
                Replace(plan.Temporary, plan.Link.Path);
                if (Hash(ReadLink(plan.Link.Path)) != plan.Link.WrittenHash) throw new IOException("Не удалось проверить установленный ярлык Wand.");
                plan.Link.PriorHash = null;
                Save(state);
            }
        }
        finally { foreach (var plan in plans) File.Delete(plan.Temporary); }
    }

    public void Restore()
    {
        using var operation = AcquireLock();
        var state = Load() ?? throw new InvalidOperationException("Перевод через ярлык не установлен.");
        foreach (var link in state.Links)
        {
            if (!File.Exists(link.Path)) continue;
            var hash = Hash(ReadLink(link.Path));
            if (link.Original is not null && hash == Hash(link.Original)) continue;
            if (!IsOwnedHash(link, hash) && IsWand(ShellShortcut.Read(link.Path).Target)) continue;
            if (!IsOwnedHash(link, hash)) throw new InvalidOperationException("Ярлык Wand изменён пользователем. Изменения сохранены; данные отката оставлены.");
            if (link.Original is null) File.Delete(link.Path);
            else
            {
                var temporary = link.Path + ".wrp-" + Guid.NewGuid().ToString("N") + ".lnk";
                try
                {
                    WriteDurable(temporary, link.Original);
                    Replace(temporary, link.Path);
                    if (Hash(ReadLink(link.Path)) != Hash(link.Original)) throw new IOException("Не удалось проверить восстановленный ярлык Wand.");
                }
                finally { File.Delete(temporary); }
            }
        }
        File.Delete(StatePath);
    }

    void RequireUnchanged(Link link)
    {
        if (!File.Exists(link.Path)) return;
        var hash = Hash(ReadLink(link.Path));
        if (!IsOwnedHash(link, hash) && (link.Original is null || hash != Hash(link.Original)))
            throw new InvalidOperationException("Ярлык Wand изменён пользователем. Сначала восстановите или удалите этот ярлык.");
    }

    bool IsWand(string target)
    {
        if (!Path.IsPathFullyQualified(target)) return false;
        var path = Path.GetFullPath(target);
        return string.Equals(path, Path.Combine(_root, "Wand.exe"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(path), "Wand.exe", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), _root, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(Path.GetDirectoryName(path))!.StartsWith("app-", StringComparison.OrdinalIgnoreCase);
    }

    void ValidatePath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase) ||
            !shortcutFolders.Any(folder => path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Некорректный путь ярлыка в данных WRP.");
        EnsureNoLinks(path);
    }

    internal static void EnsureNoLinks(string path)
    {
        for (var item = path; item is not null; item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Ярлык WRP не должен проходить через ссылку файловой системы.");
    }

    State? Load()
    {
        ValidateStateLocation();
        if (!HasState) return null;
        var state = JsonSerializer.Deserialize<State>(ReadBounded(StatePath, MaxStateBytes)) ?? throw new InvalidDataException("Повреждены данные ярлыков WRP.");
        if (state.Version != 1 || !string.Equals(state.Root, _root, StringComparison.OrdinalIgnoreCase) || state.Links is null || state.Links.Count > MaxLinks)
            throw new InvalidDataException("Данные ярлыков WRP принадлежат другой установке.");
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in state.Links)
        {
            if (link is null || link.Path is null || link.Arguments is null || link.WrittenHash is null ||
                link.WrittenHash.Length != 64 || link.WrittenHash.Any(c => !Uri.IsHexDigit(c)) ||
                link.PriorHash is not null && (link.PriorHash.Length != 64 || link.PriorHash.Any(c => !Uri.IsHexDigit(c))) || link.Original?.Length > MaxLinkBytes)
                throw new InvalidDataException("Повреждены данные ярлыков WRP.");
            ValidatePath(link.Path);
            if (Path.GetFullPath(link.Path) != link.Path || !unique.Add(link.Path)) throw new InvalidDataException("Повторяющийся путь ярлыка WRP.");
        }
        return state;
    }

    void Save(State state)
    {
        var temporary = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteDurable(temporary, Serialize(state)); Replace(temporary, StatePath); }
        finally { File.Delete(temporary); }
    }

    static byte[] Serialize(State state)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
        if (state.Links.Count > MaxLinks || bytes.Length > MaxStateBytes)
            throw new InvalidOperationException("Слишком большой объём данных отката ярлыков Wand. Установка отменена до изменения ярлыков.");
        return bytes;
    }

    void ValidateStateLocation()
    {
        if (string.Equals(_stateDirectory, _root, StringComparison.OrdinalIgnoreCase) ||
            _stateDirectory.StartsWith(Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Данные WRP должны храниться вне папки Wand.");
        EnsureNoLinks(_stateDirectory);
        EnsureNoLinks(StatePath);
    }

    FileStream AcquireLock()
    {
        ValidateStateLocation();
        Directory.CreateDirectory(_stateDirectory);
        var path = Path.Combine(_stateDirectory, "shortcuts.lock");
        EnsureNoLinks(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    static byte[] ReadLink(string path) => ReadBounded(path, MaxLinkBytes);
    static byte[] ReadBounded(string path, int maximum)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maximum) throw new InvalidDataException("Слишком большой файл данных ярлыков Wand.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static bool IsOwnedHash(Link link, string hash) => hash == link.WrittenHash || hash == link.PriorHash;
    static string Quote(string argument)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    internal static void Replace(string source, string destination)
    {
        if (File.Exists(destination)) File.Replace(source, destination, null);
        else File.Move(source, destination);
    }
    internal static void WriteDurable(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }
    sealed class State
    {
        public int Version { get; set; } = 1;
        public string Root { get; set; } = "";
        public List<Link> Links { get; set; } = new();
    }
    sealed class Link
    {
        public string Path { get; set; } = "";
        public byte[]? Original { get; set; }
        public string Arguments { get; set; } = "";
        public string WrittenHash { get; set; } = "";
        public string? PriorHash { get; set; }
    }
}
