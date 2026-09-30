using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace KCDMP_launcher.Models
{
    /// <summary>Which build of the game this launcher starts.</summary>
    public enum GamePlatform { None, Steam, GamePass }

    /// <summary>What was found of the Steam Modding Tools, for telling the user what is missing.</summary>
    public enum SteamState { NoSteam, NoModdingTools, FilesMissing, Found }

    /// <summary>
    /// Host World fork: finding the game on this machine. The stock launcher
    /// only knew the Steam Modding Tools build, and only through the path its
    /// installer seeded; this one also starts the Xbox Game Pass build, and
    /// looks for both itself so that the installer has nothing to ask.
    /// </summary>
    public static class GameInstalls
    {
        private const string GamePassContent = @"Kingdom Come- Deliverance II\Content";

        /// <summary>
        /// Steam application id of "Kingdom Come: Deliverance II Modding tools"
        /// (installdir KCD2Mod). The retail game is a separate entry, 1771300,
        /// and cannot run this mod.
        /// </summary>
        public const string ModdingToolsAppId = "2429020";

        /// <summary>
        /// The Modding Tools build links its engine modules as separate DLLs;
        /// the plugin needs these two (see Home.IsModdingToolsBuild, which
        /// forwards here).
        /// </summary>
        public static bool IsModdingToolsBuild(string? gamePath)
        {
            if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath)) return false;
            string dir = Path.GetDirectoryName(gamePath) ?? "";
            return dir.Length > 0
                && File.Exists(Path.Combine(dir, "Framework.dll"))
                && File.Exists(Path.Combine(dir, "CrySystem.dll"));
        }

        /// <summary>
        /// The Game Pass build is the retail monolith shipped as a GDK
        /// package: MicrosoftGame.Config sits beside the executable.
        /// </summary>
        public static bool IsGamePassBuild(string? gamePath)
        {
            if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath)) return false;
            string dir = Path.GetDirectoryName(gamePath) ?? "";
            return dir.Length > 0
                && !IsModdingToolsBuild(gamePath)
                && File.Exists(Path.Combine(dir, "MicrosoftGame.Config"));
        }

        /// <summary>
        /// The games folder named by a drive's .GamingRoot file: "RGBX", a
        /// 4-byte version, then the folder name in UTF-16. Null when the file
        /// is not one.
        /// </summary>
        public static string? ParseGamingRoot(byte[] bytes)
        {
            if (bytes.Length <= 8 || bytes[0] != 'R' || bytes[1] != 'G' || bytes[2] != 'B' || bytes[3] != 'X')
                return null;
            string name = Encoding.Unicode.GetString(bytes, 8, (bytes.Length - 8) & ~1).Trim('\0');
            return name.Length == 0 ? null : name;
        }

        /// <summary>The Game Pass KingdomCome.exe on one of these drives, or null.</summary>
        public static string? FindGamePassExe(IEnumerable<string> driveRoots)
        {
            foreach (var root in driveRoots)
            {
                string? folder = null;
                try
                {
                    string marker = Path.Combine(root, ".GamingRoot");
                    if (File.Exists(marker)) folder = ParseGamingRoot(File.ReadAllBytes(marker));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                // XboxGames is the Xbox app's own default, for a marker that cannot be read.
                foreach (var f in new[] { folder, "XboxGames" })
                {
                    if (string.IsNullOrEmpty(f)) continue;
                    string exe = Path.Combine(root, f, GamePassContent, "KingdomCome.exe");
                    if (File.Exists(exe)) return exe;
                }
            }
            return null;
        }

        public static string? FindGamePassExe()
        {
            try
            {
                return FindGamePassExe(DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                    .Select(d => d.RootDirectory.FullName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }

        /// <summary>The library paths in Steam's libraryfolders.vdf.</summary>
        public static IReadOnlyList<string> ParseLibraryFolders(string vdf) =>
            Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"")
                .Select(m => m.Groups[1].Value.Replace(@"\\", @"\"))
                .ToList();

        /// <summary>The first of these folders that exists, with Steam's forward slashes turned round.</summary>
        public static string? FirstExistingDir(IEnumerable<string?> candidates)
        {
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                string dir = c.Replace('/', '\\').TrimEnd('\\');
                if (dir.Length == 2 && dir[1] == ':') dir += "\\";   // "D:" alone is the current folder on D
                if (Directory.Exists(dir)) return dir;
            }
            return null;
        }

        private static string? RegistryString(RegistryHive hive, RegistryView view, string subKey, string name)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(subKey);
                return key?.GetValue(name) as string;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return null;
            }
        }

        /// <summary>
        /// Where Steam is installed. The per-user value first; then the
        /// machine-wide ones, which are all there is when Windows is logged
        /// in as a different account from the one that runs Steam -- the
        /// usual state of a server.
        /// </summary>
        public static string? SteamRoot() => FirstExistingDir(
        [
            RegistryString(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath"),
            RegistryString(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"),
            RegistryString(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Valve\Steam", "InstallPath"),
        ]);

        /// <summary>
        /// The Steam folder itself plus every library in libraryfolders.vdf
        /// that is there right now (a library on an unplugged drive is not).
        /// </summary>
        public static IReadOnlyList<string> SteamLibraries(string? steamRoot)
        {
            if (string.IsNullOrEmpty(steamRoot)) return [];
            var libs = new List<string> { steamRoot };
            try
            {
                string vdf = Path.Combine(steamRoot, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdf))
                    libs.AddRange(ParseLibraryFolders(File.ReadAllText(vdf))
                        .Select(p => p.Replace('/', '\\').TrimEnd('\\'))
                        .Where(Directory.Exists));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static IReadOnlyList<string> SteamLibraries() => SteamLibraries(SteamRoot());

        /// <summary>The "installdir" of a Steam appmanifest, or null.</summary>
        public static string? InstallDirFromManifest(string acf)
        {
            var m = Regex.Match(acf, "\"installdir\"\\s+\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value.Replace(@"\\", @"\") : null;
        }

        private static string ModdingToolsManifest(string library) =>
            Path.Combine(library, "steamapps", $"appmanifest_{ModdingToolsAppId}.acf");

        /// <summary>The Modding Tools exe under an install folder: the known layout, then any Bin\&lt;config&gt;.</summary>
        private static string? ModdingToolsExeUnder(string installDir)
        {
            string known = Path.Combine(installDir, "Bin", "Win64ReleaseSteamLTO_DLL", "KingdomCome.exe");
            if (IsModdingToolsBuild(known)) return known;

            string bin = Path.Combine(installDir, "Bin");
            if (!Directory.Exists(bin)) return null;
            try
            {
                foreach (var config in Directory.EnumerateDirectories(bin))
                {
                    string exe = Path.Combine(config, "KingdomCome.exe");
                    if (IsModdingToolsBuild(exe)) return exe;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return null;
        }

        /// <summary>
        /// The Modding Tools KingdomCome.exe in one of these Steam libraries.
        /// &lt;game&gt;\Bin\&lt;config&gt;\KingdomCome.exe is three folders
        /// below steamapps\common; going no deeper keeps this quick on a full
        /// library.
        /// </summary>
        public static string? FindModdingToolsExe(IEnumerable<string> libraries)
        {
            var shallow = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 4,
                IgnoreInaccessible = true,
            };
            var libs = libraries.ToList();

            // What Steam itself says: the tools' appmanifest names their folder.
            foreach (var lib in libs)
            {
                try
                {
                    string acf = ModdingToolsManifest(lib);
                    if (!File.Exists(acf)) continue;
                    if (InstallDirFromManifest(File.ReadAllText(acf)) is not string installDir) continue;
                    if (ModdingToolsExeUnder(Path.Combine(lib, "steamapps", "common", installDir)) is string exe) return exe;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }

            // No usable manifest (a copied install, say): look for the files.
            foreach (var lib in libs)
            {
                string common = Path.Combine(lib, @"steamapps\common");
                if (!Directory.Exists(common)) continue;
                try
                {
                    foreach (var exe in Directory.EnumerateFiles(common, "KingdomCome.exe", shallow))
                        if (IsModdingToolsBuild(exe)) return exe;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            return null;
        }

        // The scan above walks every Steam library, and "not found" is the
        // normal answer on a Game Pass machine: once per run is enough. LOOK
        // AGAIN in Settings asks for a fresh look.
        private static (string? Root, IReadOnlyList<string> Libraries, string? Exe)? _steam;

        private static (string? Root, IReadOnlyList<string> Libraries, string? Exe) Steam(bool refresh = false)
        {
            if (_steam is null || refresh)
            {
                string? root = SteamRoot();
                var libs = SteamLibraries(root);
                _steam = (root, libs, FindModdingToolsExe(libs));
            }
            return _steam.Value;
        }

        public static string? FindModdingToolsExe() => Steam().Exe;

        /// <summary>
        /// What is there of the Modding Tools. The three ways of "not found"
        /// need three different things said: no Steam, tools never installed,
        /// and tools that Steam lists but whose files are not on disk (a
        /// download still running, or cancelled).
        /// </summary>
        public static SteamState DiagnoseSteam(string? steamRoot, IEnumerable<string> libraries)
        {
            if (string.IsNullOrEmpty(steamRoot)) return SteamState.NoSteam;
            var libs = libraries.ToList();
            if (FindModdingToolsExe(libs) is not null) return SteamState.Found;
            return libs.Any(lib => File.Exists(ModdingToolsManifest(lib))) ? SteamState.FilesMissing : SteamState.NoModdingTools;
        }

        public static SteamState DiagnoseSteam(bool refresh = false)
        {
            var (root, libs, exe) = Steam(refresh);
            if (string.IsNullOrEmpty(root)) return SteamState.NoSteam;
            if (exe is not null) return SteamState.Found;
            return libs.Any(lib => File.Exists(ModdingToolsManifest(lib))) ? SteamState.FilesMissing : SteamState.NoModdingTools;
        }

        public static string SteamAdvice(SteamState state) => state switch
        {
            SteamState.NoSteam =>
                "Steam was not found on this PC. The Steam side of this mod needs Steam, Kingdom Come: Deliverance II and its Modding tools.",
            SteamState.NoModdingTools =>
                "Steam is here, but the KCD2 Modding tools are not installed. The normal game cannot run this mod: it needs the free " +
                "\"Kingdom Come: Deliverance II Modding tools\" entry in your Steam library (Library, filter: Tools). " +
                "Install it, start it once from Steam so it finishes setting up, then look again.",
            SteamState.FilesMissing =>
                "Steam lists the KCD2 Modding tools, but their files are not on disk: the download is still running or was cancelled. " +
                "Let Steam finish (or use Properties > Installed Files > Verify), then look again.",
            _ => "The KCD2 Modding tools were found.",
        };

        /// <summary>The folder that holds Data\ (and Mods\), above the executable.</summary>
        public static string? DataRootOf(string gamePath)
        {
            string? dir = Path.GetDirectoryName(gamePath);
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "Data"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        /// <summary>Where the Modding Tools build loads this mod from.</summary>
        public static string? SteamModDir(string gamePath)
        {
            string? root = DataRootOf(gamePath);
            return root is null ? null : Path.Combine(root, "Mods", "kdcmp");
        }

        /// <summary>Where the Game Pass build loads this mod from.</summary>
        public static string GamePassModDir() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "kingdomcome_mods", "kdcmp");

        /// <summary>The Steam build's saves. "Saved Games" is a shell folder the user can move.</summary>
        public static string SteamSaveRoot()
        {
            string? saved = null;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
                saved = key?.GetValue("{4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4}") as string;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }

            if (string.IsNullOrWhiteSpace(saved))
                saved = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
            return Path.Combine(Environment.ExpandEnvironmentVariables(saved), "kingdomcome2", "saves");
        }

        /// <summary>
        /// Which build Launch starts. "auto" prefers the Steam build when both
        /// are configured: it is the one with the native plugin.
        /// </summary>
        public static GamePlatform ResolvePlatform(AppSettings s)
        {
            bool steam = IsModdingToolsBuild(s.GamePath);
            bool gamePass = IsGamePassBuild(s.GamePassPath);
            return (s.Platform ?? "").Trim().ToLowerInvariant() switch
            {
                "steam" => steam ? GamePlatform.Steam : GamePlatform.None,
                "gamepass" => gamePass ? GamePlatform.GamePass : GamePlatform.None,
                _ => steam ? GamePlatform.Steam : gamePass ? GamePlatform.GamePass : GamePlatform.None,
            };
        }

        /// <summary>
        /// Fills in whichever game path is missing or no longer valid. True
        /// when a path changed, so the caller knows to save the settings.
        /// </summary>
        public static bool Detect(AppSettings s, Func<string?> findSteam, Func<string?> findGamePass)
        {
            bool changed = false;
            if (!IsModdingToolsBuild(s.GamePath) && findSteam() is string steam)
            {
                s.GamePath = steam;
                changed = true;
            }
            if (!IsGamePassBuild(s.GamePassPath) && findGamePass() is string gamePass)
            {
                s.GamePassPath = gamePass;
                changed = true;
            }
            return changed;
        }

        public static bool Detect(AppSettings s) => Detect(s, FindModdingToolsExe, FindGamePassExe);
    }
}
