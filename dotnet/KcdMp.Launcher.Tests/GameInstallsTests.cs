using System.Text;
using KCDMP_launcher.Models;

namespace KcdMp.Launcher.Tests;

/// <summary>A scratch folder that is removed again, with helpers to lay out a fake game.</summary>
public sealed class Scratch : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "kcdmp-launcher-tests-" + Guid.NewGuid().ToString("N"));

    public Scratch() => Directory.CreateDirectory(Root);

    public string File(string relative, string content = "")
    {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public string Bytes(string relative, byte[] content)
    {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>A Steam library holding a Modding Tools install; returns the exe.</summary>
    public string ModdingTools(string library = "SteamLibrary", string game = "KCD2Mod")
    {
        string bin = Path.Combine(library, "steamapps", "common", game, "Bin", "Win64ReleaseSteamLTO_DLL");
        File(Path.Combine(bin, "Framework.dll"));
        File(Path.Combine(bin, "CrySystem.dll"));
        File(Path.Combine(library, "steamapps", "common", game, "steam_appid.txt"), "1");
        File(Path.Combine(library, "steamapps", "common", game, "Data", "x.pak"));
        return File(Path.Combine(bin, "KingdomCome.exe"));
    }

    /// <summary>A drive with a Game Pass install under the named games folder; returns the exe.</summary>
    public string GamePass(string drive = "X", string gamesFolder = "GAMEPASS", bool marker = true)
    {
        if (marker)
            Bytes(Path.Combine(drive, ".GamingRoot"),
                [.. "RGBX"u8, 1, 0, 0, 0, .. Encoding.Unicode.GetBytes(gamesFolder + "\0")]);
        string content = Path.Combine(drive, gamesFolder, "Kingdom Come- Deliverance II", "Content");
        File(Path.Combine(content, "MicrosoftGame.Config"));
        return File(Path.Combine(content, "KingdomCome.exe"));
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}

public class GameInstallsTests
{
    [Fact]
    public void The_games_folder_is_read_from_the_drive_marker()
    {
        byte[] marker = [.. "RGBX"u8, 1, 0, 0, 0, .. Encoding.Unicode.GetBytes("GAMEPASS\0")];
        Assert.Equal("GAMEPASS", GameInstalls.ParseGamingRoot(marker));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]           // not a marker
    [InlineData(new byte[] { (byte)'R', (byte)'G', (byte)'B', (byte)'X', 1, 0, 0, 0 })]   // no name
    public void A_file_that_is_not_a_marker_names_no_folder(byte[] bytes) =>
        Assert.Null(GameInstalls.ParseGamingRoot(bytes));

    [Fact]
    public void The_Game_Pass_game_is_found_through_the_marker()
    {
        using var s = new Scratch();
        s.File(Path.Combine("C", "nothing.txt"));
        string exe = s.GamePass("X", "My Games");    // a space in the folder name
        string? found = GameInstalls.FindGamePassExe([Path.Combine(s.Root, "C"), Path.Combine(s.Root, "X")]);
        Assert.Equal(exe, found);
        Assert.True(GameInstalls.IsGamePassBuild(found));
    }

    [Fact]
    public void The_Xbox_apps_default_folder_is_tried_without_a_marker()
    {
        using var s = new Scratch();
        string exe = s.GamePass("C", "XboxGames", marker: false);
        Assert.Equal(exe, GameInstalls.FindGamePassExe([Path.Combine(s.Root, "C")]));
    }

    [Fact]
    public void No_Game_Pass_game_is_no_path()
    {
        using var s = new Scratch();
        s.File(Path.Combine("C", "nothing.txt"));
        Assert.Null(GameInstalls.FindGamePassExe([Path.Combine(s.Root, "C")]));
    }

    [Fact]
    public void Steam_library_paths_are_read_from_libraryfolders()
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"X:\\\\SteamLibrary\"\n\t}\n}";
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"X:\SteamLibrary"], GameInstalls.ParseLibraryFolders(vdf));
    }

    [Fact]
    public void The_Modding_Tools_build_is_found_and_the_retail_game_is_not()
    {
        using var s = new Scratch();
        // The retail game: the same exe name, no module DLLs.
        s.File(Path.Combine("Lib1", "steamapps", "common", "KingdomComeDeliverance2", "Bin", "Win64MasterMasterSteamPGO", "KingdomCome.exe"));
        string exe = s.ModdingTools("Lib2");
        string? found = GameInstalls.FindModdingToolsExe([Path.Combine(s.Root, "Lib1"), Path.Combine(s.Root, "Lib2"), Path.Combine(s.Root, "missing")]);
        Assert.Equal(exe, found);
        Assert.False(GameInstalls.IsGamePassBuild(found));
    }

    [Fact]
    public void The_mod_goes_beside_the_games_Data_folder()
    {
        using var s = new Scratch();
        string exe = s.ModdingTools();
        Assert.Equal(
            Path.Combine(s.Root, "SteamLibrary", "steamapps", "common", "KCD2Mod", "Mods", "kdcmp"),
            GameInstalls.SteamModDir(exe));
    }

    [Fact]
    public void Automatic_prefers_Steam_and_falls_back_to_Game_Pass()
    {
        using var s = new Scratch();
        string steam = s.ModdingTools();
        string gamePass = s.GamePass();

        Assert.Equal(GamePlatform.Steam, GameInstalls.ResolvePlatform(new AppSettings { GamePath = steam, GamePassPath = gamePass }));
        Assert.Equal(GamePlatform.GamePass, GameInstalls.ResolvePlatform(new AppSettings { GamePassPath = gamePass }));
        Assert.Equal(GamePlatform.GamePass, GameInstalls.ResolvePlatform(new AppSettings { GamePath = steam, GamePassPath = gamePass, Platform = "gamepass" }));
        Assert.Equal(GamePlatform.None, GameInstalls.ResolvePlatform(new AppSettings { GamePassPath = gamePass, Platform = "steam" }));
        Assert.Equal(GamePlatform.None, GameInstalls.ResolvePlatform(new AppSettings()));
    }

    // The Game Pass exe in the Steam field (or the other way round) must not
    // pass for the build that field is for.
    [Fact]
    public void A_path_to_the_other_build_does_not_count()
    {
        using var s = new Scratch();
        string steam = s.ModdingTools();
        string gamePass = s.GamePass();
        Assert.Equal(GamePlatform.None, GameInstalls.ResolvePlatform(new AppSettings { GamePath = gamePass, GamePassPath = steam }));
    }

    [Fact]
    public void Detection_fills_what_is_missing_and_leaves_what_is_valid()
    {
        using var s = new Scratch();
        string steam = s.ModdingTools();
        string gamePass = s.GamePass();
        string otherSteam = s.ModdingTools("Other");

        var settings = new AppSettings { GamePath = steam, GamePassPath = @"C:\gone\KingdomCome.exe" };
        bool changed = GameInstalls.Detect(settings, () => otherSteam, () => gamePass);

        Assert.True(changed);
        Assert.Equal(steam, settings.GamePath);          // valid: not replaced
        Assert.Equal(gamePass, settings.GamePassPath);   // stale: replaced
        Assert.False(GameInstalls.Detect(settings, () => otherSteam, () => gamePass));
    }

    [Fact]
    public void Detection_with_nothing_installed_changes_nothing()
    {
        var settings = new AppSettings();
        Assert.False(GameInstalls.Detect(settings, () => null, () => null));
        Assert.Equal("", settings.GamePath);
        Assert.Equal("", settings.GamePassPath);
    }
}

public class ModPackageTests
{
    private static string Package(Scratch s, string pak = "pak v2")
    {
        s.File(Path.Combine("pkg", "mod.manifest"), "<manifest/>");
        s.File(Path.Combine("pkg", "Data", "kdcmp.pak"), pak);
        return Path.Combine(s.Root, "pkg");
    }

    [Fact]
    public void A_game_without_the_mod_gets_both_files_and_nothing_else()
    {
        using var s = new Scratch();
        string pkg = Package(s);
        s.File(Path.Combine("pkg", "Data", "Libs", "Tables", "loose.xml"));   // pak sources must never be deployed loose
        string mod = Path.Combine(s.Root, "game", "Mods", "kdcmp");

        Assert.False(ModPackage.IsCurrent(mod, pkg));
        ModPackage.Install(mod, pkg);

        Assert.True(ModPackage.IsCurrent(mod, pkg));
        Assert.Equal(
            ["Data", "mod.manifest"],
            Directory.GetFileSystemEntries(mod).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(["kdcmp.pak"], Directory.GetFileSystemEntries(Path.Combine(mod, "Data")).Select(Path.GetFileName));
    }

    // Loose pak sources in the mod folder (an old developer deploy) stop the
    // game from starting: "114 tables are not loaded".
    [Fact]
    public void Leftovers_in_the_mod_folder_are_removed()
    {
        using var s = new Scratch();
        string pkg = Package(s);
        string mod = Path.Combine(s.Root, "game", "Mods", "kdcmp");
        s.File(Path.Combine("game", "Mods", "kdcmp", "Data", "Libs", "Tables", "loose.xml"));
        s.File(Path.Combine("game", "Mods", "kdcmp", "Data", "Scripts", "Startup", "kdcmp.lua"));
        s.File(Path.Combine("game", "Mods", "kdcmp", "notes.txt"));
        string neighbour = s.File(Path.Combine("game", "Mods", "othermod", "Data", "other.pak"), "not ours");

        ModPackage.Install(mod, pkg);

        Assert.Equal(["kdcmp.pak"], Directory.GetFileSystemEntries(Path.Combine(mod, "Data")).Select(Path.GetFileName));
        Assert.Equal(["Data", "mod.manifest"],
            Directory.GetFileSystemEntries(mod).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("not ours", File.ReadAllText(neighbour));
    }

    [Fact]
    public void An_older_pak_is_not_current_and_is_replaced()
    {
        using var s = new Scratch();
        string pkg = Package(s);
        string mod = Path.Combine(s.Root, "game", "Mods", "kdcmp");
        s.File(Path.Combine("game", "Mods", "kdcmp", "Data", "kdcmp.pak"), "pak v1");

        Assert.False(ModPackage.IsCurrent(mod, pkg));
        ModPackage.Install(mod, pkg);
        Assert.Equal("pak v2", File.ReadAllText(Path.Combine(mod, "Data", "kdcmp.pak")));
    }

    [Fact]
    public void The_skip_save_is_installed_under_its_own_name_once()
    {
        using var s = new Scratch();
        string save = s.File(Path.Combine("pkg", "save", "autosave018.whs"), "save bytes");
        string saves = Path.Combine(s.Root, "saves");
        string mine = s.File(Path.Combine("saves", "playline0", "autosave018.whs"), "the player's own");

        Assert.True(ModPackage.InstallSkipSave(saves, save));
        Assert.False(ModPackage.InstallSkipSave(saves, save));
        Assert.Equal("save bytes", File.ReadAllText(Path.Combine(saves, "playline0", "kcdmpskip.whs")));
        Assert.Equal("the player's own", File.ReadAllText(mine));
    }

    [Fact]
    public void A_package_without_the_save_installs_nothing()
    {
        using var s = new Scratch();
        Assert.False(ModPackage.InstallSkipSave(Path.Combine(s.Root, "saves"), Path.Combine(s.Root, "no.whs")));
        Assert.False(Directory.Exists(Path.Combine(s.Root, "saves")));
    }
}

public class ScriptLauncherTests
{
    private static readonly string Base = @"C:\Users\Some Player\AppData\Local\KCDMP-HostWorld";

    [Fact]
    public void A_Game_Pass_join_passes_the_relay_the_game_and_the_name()
    {
        var settings = new AppSettings { GamePassPath = @"X:\My Games\Kingdom Come- Deliverance II\Content\KingdomCome.exe", PlayerName = " Sir Henry " };
        var psi = ScriptLauncher.GamePass(Base, settings, "10.1.2.3", 7779);

        Assert.EndsWith("powershell.exe", psi.FileName);
        Assert.Equal(Base, psi.WorkingDirectory);
        Assert.Equal(
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Base, "Start-GamePass.ps1"),
             "-RelayHost", "10.1.2.3", "-RelayPort", "7779", "-GameExe", settings.GamePassPath,
             "-PlayerName", "Sir Henry", "-PauseOnError"],
            psi.ArgumentList);
    }

    [Fact]
    public void No_name_and_no_auto_load_are_passed_as_such()
    {
        var settings = new AppSettings { GamePassPath = @"X:\g\KingdomCome.exe", AutoLoadLastSave = false };
        var args = ScriptLauncher.GamePass(Base, settings, "h", 7778).ArgumentList;

        Assert.DoesNotContain("-PlayerName", args);
        Assert.Contains("-NoAutoLoad", args);
    }

    [Fact]
    public void A_world_host_is_started_on_the_Steam_game_and_the_host_port()
    {
        var settings = new AppSettings { GamePath = @"D:\Steam Library\KCD2Mod\Bin\x\KingdomCome.exe", HostPort = 7790 };
        var psi = ScriptLauncher.WorldHost(Base, settings);

        Assert.Equal(
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Base, "Start-WorldHost.ps1"),
             "-GameExe", settings.GamePath, "-RelayPort", "7790", "-PauseOnError"],
            psi.ArgumentList);
    }

    // Built as one string, "Bob\" swallowed the closing quote and took
    // --no-voice into the name: the microphone stayed on.
    [Fact]
    public void A_Steam_agent_gets_each_argument_whole()
    {
        var settings = new AppSettings { PlayerName = @"Bob\", VoiceChatEnabled = false };
        var psi = ScriptLauncher.Agent(@"C:\app\KcdMpClient.exe", settings, "10.1.2.3", 7778, hosting: true);

        Assert.Equal(["--host", "10.1.2.3", "--port", "7778", "--name", @"Bob\", "--no-voice", "--hosting"], psi.ArgumentList);
        Assert.Equal(@"C:\app", psi.WorkingDirectory);
        Assert.Equal("", psi.Arguments);
    }

    [Fact]
    public void A_Steam_agent_without_a_name_lets_the_agent_pick_one()
    {
        var psi = ScriptLauncher.Agent(@"C:\app\KcdMpClient.exe", new AppSettings(), "h", 7778, hosting: false);
        Assert.Equal(["--host", "h", "--port", "7778"], psi.ArgumentList);
    }

    [Fact]
    public void Settings_that_cannot_be_read_are_the_defaults()
    {
        using var s = new Scratch();
        Assert.Equal("auto", ScriptLauncher.LoadSettings(Path.Combine(s.Root, "none.json")).Platform);
        Assert.Equal("auto", ScriptLauncher.LoadSettings(s.File("bad.json", "{ not json")).Platform);
        Assert.Equal("gamepass", ScriptLauncher.LoadSettings(s.File("ok.json", "{\"Platform\":\"gamepass\"}")).Platform);
    }

    // An existing settings.json from the stock launcher has none of the new
    // fields; it must read as the defaults for them.
    [Fact]
    public void A_stock_settings_file_gets_the_new_defaults()
    {
        using var s = new Scratch();
        var settings = ScriptLauncher.LoadSettings(s.File("stock.json", "{\"GamePath\":\"C:\\\\g\\\\KingdomCome.exe\",\"HostPort\":7778}"));
        Assert.Equal(@"C:\g\KingdomCome.exe", settings.GamePath);
        Assert.Equal("", settings.GamePassPath);
        Assert.Equal("auto", settings.Platform);
        Assert.True(settings.AutoLoadLastSave);
    }
}
