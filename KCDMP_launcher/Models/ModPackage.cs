using System;
using System.IO;
using System.Security.Cryptography;

namespace KCDMP_launcher.Models
{
    /// <summary>
    /// Host World fork: the game mod and the skip save that ship beside the
    /// launcher (mod\, save\), and putting them where the game reads them.
    /// The stock installer deployed the mod once, at install time; this is
    /// done at launch instead, so that an update of the launcher can never
    /// leave the game on an older mod.
    /// </summary>
    public static class ModPackage
    {
        public const string SkipSaveName = "kcdmpskip";

        public static string PackageDir => Path.Combine(AppContext.BaseDirectory, "mod");
        public static string SkipSaveFile => Path.Combine(AppContext.BaseDirectory, "save", "autosave018.whs");

        private static bool SameContent(string a, string b)
        {
            if (!File.Exists(a) || !File.Exists(b)) return false;
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            using var fa = File.OpenRead(a);
            using var fb = File.OpenRead(b);
            return SHA256.HashData(fa).AsSpan().SequenceEqual(SHA256.HashData(fb));
        }

        /// <summary>True when modDir already holds this package's pak.</summary>
        public static bool IsCurrent(string modDir, string packageDir) =>
            SameContent(Path.Combine(modDir, "Data", "kdcmp.pak"), Path.Combine(packageDir, "Data", "kdcmp.pak"));

        /// <summary>
        /// Only these two files, never the pak's loose sources: a loose
        /// Data\Libs\Tables inside a mod takes over the engine's table root
        /// (see installer\KCDMP.iss). The game keeps the pak open, so this
        /// is for a closed game.
        /// </summary>
        public static void Install(string modDir, string packageDir)
        {
            RemoveEverythingElse(modDir);
            Directory.CreateDirectory(Path.Combine(modDir, "Data"));
            File.Copy(Path.Combine(packageDir, "mod.manifest"), Path.Combine(modDir, "mod.manifest"), overwrite: true);
            File.Copy(Path.Combine(packageDir, "Data", "kdcmp.pak"), Path.Combine(modDir, "Data", "kdcmp.pak"), overwrite: true);
        }

        /// <summary>
        /// The mod's own folder holds the manifest and the pak and nothing
        /// else. Anything more is a leftover -- an old developer deploy with
        /// the pak's loose sources, which stops the game from starting -- and
        /// the stock installer emptied the folder for the same reason.
        /// </summary>
        private static void RemoveEverythingElse(string modDir)
        {
            if (!Directory.Exists(modDir)) return;
            string data = Path.Combine(modDir, "Data");
            foreach (var entry in Directory.GetFileSystemEntries(modDir))
            {
                string name = Path.GetFileName(entry);
                if (name.Equals("mod.manifest", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("Data", StringComparison.OrdinalIgnoreCase) && Directory.Exists(entry)) continue;
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true); else File.Delete(entry);
            }
            if (!Directory.Exists(data)) return;
            foreach (var entry in Directory.GetFileSystemEntries(data))
            {
                if (Path.GetFileName(entry).Equals("kdcmp.pak", StringComparison.OrdinalIgnoreCase) && File.Exists(entry)) continue;
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true); else File.Delete(entry);
            }
        }

        /// <summary>
        /// Puts the skip save into saves\playline&lt;N&gt; under its own name,
        /// so it never replaces a save of the player's. True when it copied.
        /// </summary>
        public static bool InstallSkipSave(string saveRoot, string saveFile, int playline = 0)
        {
            if (playline is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(playline), "The game has playlines 0 to 4.");
            if (!File.Exists(saveFile)) return false;
            string dir = Path.Combine(saveRoot, $"playline{playline}");
            string dst = Path.Combine(dir, SkipSaveName + ".whs");
            if (SameContent(saveFile, dst)) return false;
            Directory.CreateDirectory(dir);
            File.Copy(saveFile, dst, overwrite: true);
            return true;
        }
    }
}
