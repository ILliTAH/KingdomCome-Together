namespace KcdMp.Client.Tests;

public class ConfigAndLocatorTests
{
    [Fact]
    public void Transport_remoteconsole_is_recognised()
    {
        var c = new ClientConfig();
        c.ApplyCommandLine(["--transport", "RemoteConsole"]);
        Assert.True(c.UsesRemoteConsole);
    }

    [Fact]
    public void Default_transport_is_unchanged()
    {
        var c = new ClientConfig();
        Assert.Equal("logtail", c.Transport);
        Assert.False(c.UsesRemoteConsole);
    }

    [Fact]
    public void Game_pass_log_is_in_documents() =>
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "kcd.log"),
            KcdLogLocator.GamePassLogPath);

    [Fact]
    public void Remoteconsole_flag_is_not_written_to_the_config_file() =>
        Assert.DoesNotContain("UsesRemoteConsole", System.Text.Json.JsonSerializer.Serialize(new ClientConfig()),
            StringComparison.OrdinalIgnoreCase);
}
