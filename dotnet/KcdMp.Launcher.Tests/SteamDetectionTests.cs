using KCDMP_launcher.Models;

namespace KcdMp.Launcher.Tests;

/// <summary>
/// Finding the Steam Modding Tools, and saying what is missing when they are
/// not there. A launcher that only shows two empty path boxes leaves the
/// person at a server machine with nothing to go on (that happened).
/// </summary>
public class SteamDetectionTests
{
    private const string Manifest =
        "\"AppState\"\n{\n\t\"appid\"\t\t\"2429020\"\n\t\"name\"\t\t\"Kingdom Come: Deliverance II Modding tools\"\n\t\"installdir\"\t\t\"KCD2Mod\"\n}\n";

    private static string Lib(Scratch s, string name = "SteamLibrary") => Path.Combine(s.Root, name);

    [Fact]
    public void The_install_folder_is_read_from_the_app_manifest()
    {
        Assert.Equal("KCD2Mod", GameInstalls.InstallDirFromManifest(Manifest));
        Assert.Null(GameInstalls.InstallDirFromManifest("\"AppState\"\n{\n}\n"));
    }

    [Fact]
    public void The_first_Steam_folder_that_exists_is_used()
    {
        using var s = new Scratch();
        s.File(Path.Combine("Steam", "steam.exe"));
        string real = Path.Combine(s.Root, "Steam");

        // Steam writes its own path with forward slashes; the first candidate is a stale entry.
        Assert.Equal(real, GameInstalls.FirstExistingDir([Path.Combine(s.Root, "gone"), null, "", real.Replace('\\', '/') + "/"]));
        Assert.Null(GameInstalls.FirstExistingDir([null, Path.Combine(s.Root, "gone")]));
    }

    [Fact]
    public void Libraries_are_the_Steam_folder_and_every_listed_one_that_exists()
    {
        using var s = new Scratch();
        string steam = Path.Combine(s.Root, "Steam");
        string other = Path.Combine(s.Root, "Other Library");
        Directory.CreateDirectory(other);
        s.File(Path.Combine("Steam", "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steam.Replace(@"\", @"\\") + "\"\n\t}\n" +
            "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + other.Replace(@"\", @"\\") + "\"\n\t}\n" +
            "\t\"2\"\n\t{\n\t\t\"path\"\t\t\"Q:\\\\unplugged\"\n\t}\n}\n");

        Assert.Equal([steam, other], GameInstalls.SteamLibraries(steam));
        Assert.Empty(GameInstalls.SteamLibraries(null));
    }

    // The manifest names the folder, so the tools are found wherever Steam
    // put them and whatever the build folder under Bin is called.
    [Fact]
    public void The_Modding_Tools_are_found_through_their_app_manifest()
    {
        using var s = new Scratch();
        s.File(Path.Combine("SteamLibrary", "steamapps", "appmanifest_2429020.acf"), Manifest);
        string bin = Path.Combine("SteamLibrary", "steamapps", "common", "KCD2Mod", "Bin", "Win64SomeOtherConfig");
        s.File(Path.Combine(bin, "Framework.dll"));
        s.File(Path.Combine(bin, "CrySystem.dll"));
        string exe = s.File(Path.Combine(bin, "KingdomCome.exe"));

        Assert.Equal(exe, GameInstalls.FindModdingToolsExe([Lib(s)]));
        Assert.Equal(SteamState.Found, GameInstalls.DiagnoseSteam(Lib(s), [Lib(s)]));
    }

    [Fact]
    public void No_Steam_is_said_as_such()
    {
        Assert.Equal(SteamState.NoSteam, GameInstalls.DiagnoseSteam(null, []));
    }

    // A machine with only the normal game: the usual case on a server someone
    // "has the game" on.
    [Fact]
    public void The_retail_game_alone_means_the_Modding_Tools_are_not_installed()
    {
        using var s = new Scratch();
        s.File(Path.Combine("SteamLibrary", "steamapps", "appmanifest_1771300.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"1771300\"\n\t\"installdir\"\t\t\"KingdomComeDeliverance2\"\n}\n");
        s.File(Path.Combine("SteamLibrary", "steamapps", "common", "KingdomComeDeliverance2", "Bin", "Win64MasterMasterSteamPGO", "KingdomCome.exe"));

        Assert.Null(GameInstalls.FindModdingToolsExe([Lib(s)]));
        Assert.Equal(SteamState.NoModdingTools, GameInstalls.DiagnoseSteam(Lib(s), [Lib(s)]));
    }

    // Steam lists the tools but the files are not there: a download still
    // running, or cancelled. "Install them" would be useless advice.
    [Fact]
    public void A_manifest_without_the_files_is_a_download_that_did_not_finish()
    {
        using var s = new Scratch();
        s.File(Path.Combine("SteamLibrary", "steamapps", "appmanifest_2429020.acf"), Manifest);

        Assert.Equal(SteamState.FilesMissing, GameInstalls.DiagnoseSteam(Lib(s), [Lib(s)]));
    }

    [Theory]
    [InlineData(SteamState.NoSteam, "Steam was not found")]
    [InlineData(SteamState.NoModdingTools, "Modding tools")]
    [InlineData(SteamState.FilesMissing, "not on disk")]
    public void Each_state_has_advice_that_says_what_to_do(SteamState state, string mentions) =>
        Assert.Contains(mentions, GameInstalls.SteamAdvice(state));
}
