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

    [Fact]
    public void A_machine_is_a_world_host_only_when_told_so()
    {
        var c = new ClientConfig();
        Assert.False(c.WorldHost);
        c.ApplyCommandLine(["--world-host"]);
        Assert.True(c.WorldHost);
        Assert.True(c.WorldHostFollow);
    }

    [Fact]
    public void A_world_host_can_be_started_without_follow()
    {
        var c = new ClientConfig();
        c.ApplyCommandLine(["--world-host", "--no-world-follow"]);
        Assert.True(c.WorldHost);
        Assert.False(c.WorldHostFollow);
    }

    // A host role left in kcdmp-client.json would turn a player's machine
    // into a host the next time its agent starts.
    [Fact]
    public void The_world_host_role_is_never_written_to_the_config_file()
    {
        var c = new ClientConfig();
        c.ApplyCommandLine(["--world-host"]);
        Assert.DoesNotContain("WorldHost", System.Text.Json.JsonSerializer.Serialize(c),
            StringComparison.OrdinalIgnoreCase);
    }

    // Every other game recognises the host by this prefix.
    [Theory]
    [InlineData("[HOST] world", "[HOST] world")]
    [InlineData("server", "[HOST] server")]
    [InlineData("", "[HOST] world")]
    [InlineData(null, "[HOST] world")]
    public void A_world_host_is_always_named_as_one(string? given, string expected) =>
        Assert.Equal(expected, ClientConfig.WorldHostName(given));

    // Everyone else's game would treat that player as the host and hide them.
    [Theory]
    [InlineData("Henry", "Henry")]
    [InlineData("[HOST] Henry", "Henry")]
    [InlineData("[HOST][HOST]Henry", "Henry")]
    [InlineData("  [HOST]  ", "Player")]
    public void A_player_cannot_be_named_as_a_world_host(string given, string expected) =>
        Assert.Equal(expected, ClientConfig.OrdinaryPlayerName(given));

    // Guests count the host as present by these packets, so they are paced
    // by the clock: a loop tick is not 10 ms on Windows, and much longer on a
    // host whose every tick waits for an HTTP round trip.
    [Theory]
    [InlineData(1.9, false)]
    [InlineData(2.0, true)]
    [InlineData(30.0, true)]
    public void A_still_player_reports_its_position_every_two_seconds(double secondsSinceLastSend, bool due) =>
        Assert.Equal(due, GameBridge.PositionHeartbeatDue(TimeSpan.FromSeconds(secondsSinceLastSend)));

    [Theory]
    [InlineData(true, "if KCD2MP_AssertWorldHost then KCD2MP_AssertWorldHost(true) end")]
    [InlineData(false, "if KCD2MP_AssertWorldHost then KCD2MP_AssertWorldHost(false) end")]
    public void The_host_role_is_asserted_through_the_mod(bool follow, string lua) =>
        Assert.Equal(lua, GameBridge.WorldHostAssertLua(follow));
}
