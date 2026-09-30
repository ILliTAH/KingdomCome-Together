using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace KCDMP_launcher.Models
{
    /// <summary>
    /// Host World fork: the two ways of starting the game that the stock
    /// launcher did not have -- an Xbox Game Pass player, and a dedicated
    /// world host -- are the scripts that ship beside the launcher
    /// (Start-GamePass.ps1, Start-WorldHost.ps1). The launcher is the menu in
    /// front of them; the console window a script opens is the agent's, the
    /// same window the stock launcher's agent has.
    /// </summary>
    public static class ScriptLauncher
    {
        public const string GamePassScript = "Start-GamePass.ps1";
        public const string WorldHostScript = "Start-WorldHost.ps1";

        private static ProcessStartInfo PowerShell(string baseDir, string script, IEnumerable<string> args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                UseShellExecute = false,
                WorkingDirectory = baseDir,
            };
            foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(baseDir, script) })
                psi.ArgumentList.Add(a);
            foreach (var a in args) psi.ArgumentList.Add(a);
            // The window would close with the error in it.
            psi.ArgumentList.Add("-PauseOnError");
            return psi;
        }

        /// <summary>A Game Pass player joining the relay at ip:port.</summary>
        public static ProcessStartInfo GamePass(string baseDir, AppSettings s, string ip, int port)
        {
            var args = new List<string>
            {
                "-RelayHost", ip,
                "-RelayPort", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-GameExe", s.GamePassPath,
            };
            if (!string.IsNullOrWhiteSpace(s.PlayerName)) { args.Add("-PlayerName"); args.Add(s.PlayerName.Trim()); }
            if (!s.AutoLoadLastSave) args.Add("-NoAutoLoad");
            return PowerShell(baseDir, GamePassScript, args);
        }

        /// <summary>
        /// The agent for a Steam player (the stock CONNECT step). Arguments go
        /// through ArgumentList: built as one string, a name ending in a
        /// backslash swallowed the closing quote and took "--no-voice" into
        /// the name with it, leaving the microphone on.
        /// </summary>
        public static ProcessStartInfo Agent(string agentPath, AppSettings s, string ip, int port, bool hosting)
        {
            var psi = new ProcessStartInfo
            {
                FileName = agentPath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(agentPath),
            };
            var args = new List<string> { "--host", ip, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (!string.IsNullOrWhiteSpace(s.PlayerName)) { args.Add("--name"); args.Add(s.PlayerName.Trim()); }
            if (!s.VoiceChatEnabled) args.Add("--no-voice");
            if (hosting) args.Add("--hosting");
            foreach (var a in args) psi.ArgumentList.Add(a);
            return psi;
        }

        /// <summary>This machine as the dedicated world host (Steam, Modding Tools build).</summary>
        public static ProcessStartInfo WorldHost(string baseDir, AppSettings s)
        {
            var args = new List<string>
            {
                "-GameExe", s.GamePath,
                "-RelayPort", s.HostPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            return PowerShell(baseDir, WorldHostScript, args);
        }

        /// <summary>
        /// settings.json as the launcher's own page reads it: missing or
        /// unreadable is the defaults.
        /// </summary>
        public static AppSettings LoadSettings(string path)
        {
            try
            {
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
            return new AppSettings();
        }

        /// <summary>
        /// "KCDMP_launcher.exe --play host[:port]": join without the window,
        /// for a shortcut. Game Pass only -- the Steam build needs the CONNECT
        /// step, which is a person saying their save has loaded. Returns the
        /// process exit code and a message for the caller to show on failure.
        /// </summary>
        public static (int Code, string Message) QuickPlay(string baseDir, string target)
        {
            string host = target;
            int port = 7778;
            int colon = target.LastIndexOf(':');
            if (colon > 0 && int.TryParse(target[(colon + 1)..], out int p)) { host = target[..colon]; port = p; }
            if (string.IsNullOrWhiteSpace(host)) return (2, "Usage: KCDMP_launcher.exe --play <host[:port]>");

            string settingsPath = Path.Combine(baseDir, "settings.json");
            var settings = LoadSettings(settingsPath);
            if (GameInstalls.Detect(settings))
            {
                try { File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }

            switch (GameInstalls.ResolvePlatform(settings))
            {
                case GamePlatform.GamePass:
                    if (!File.Exists(Path.Combine(baseDir, GamePassScript)))
                        return (3, $"{GamePassScript} is missing from {baseDir}.");
                    Process.Start(GamePass(baseDir, settings, host, port));
                    return (0, "");
                case GamePlatform.Steam:
                    return (4, "--play starts the Xbox Game Pass build only. On Steam, open the launcher and use JOIN SERVER, then CONNECT once your save has loaded.");
                default:
                    return (5, "No game found. Open the launcher and check Settings.");
            }
        }
    }
}
