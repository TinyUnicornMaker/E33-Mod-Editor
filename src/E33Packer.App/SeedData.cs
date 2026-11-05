using System.Diagnostics;
using System.Reflection;

namespace E33Packer.App;

/// <summary>
/// The standalone app must not depend on a repo checkout. These are the mod's
/// durable JSON inputs that are not embedded in E33ModEditor.Core (weapon
/// curve/over-level tables, the core skill-graph layout and the spoiler
/// baseline dumps). They are embedded in the app and copied into the app data
/// root before a build.
/// </summary>
internal static class SeedData
{
    private static readonly string[] Files =
    {
        "weapon_curve.json",
        "weapon_equipment_scaling.json",
        "weapon_upgrade_costs.json",
        "skill_graph.json",
        "items.json",
        "chests.json",
        "enemies.json",
        "DT_WeaponDefinitions.json",
    };

    public static void Ensure()
    {
        try
        {
            var asm = typeof(SeedData).Assembly;
            var work = Path.Combine(AppSettings.DataRoot, "work");
            Directory.CreateDirectory(work);
            foreach (var name in Files)
            {
                var resource = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase));
                if (resource is null) continue;
                using var stream = asm.GetManifestResourceStream(resource);
                if (stream is null) continue;
                using var file = File.Create(Path.Combine(work, name));
                stream.CopyTo(file);
            }

            // Several core kits were written for the full editor's scratch
            // layout and probe for donors under work/probe-*/extracted. The
            // standalone app has only work/tables, so give those legacy
            // lookups the same asset tree via directory links.
            var tables = Path.Combine(work, "tables");
            if (Directory.Exists(tables))
            {
                foreach (var legacy in new[]
                {
                    Path.Combine(work, "probe-twilight"),
                    Path.Combine(work, "probe-verso"),
                    Path.Combine(work, "probe-skill"),
                    Path.Combine(work, "probe-custom"),
                    Path.Combine(work, "extract_maelle"),
                    Path.Combine(AppSettings.DataRoot, "extracted"),
                })
                    EnsureLink(legacy, tables);
            }
        }
        catch
        {
            // Best effort; the build reports a missing input with a real path.
        }
    }

    private static void EnsureLink(string linkPath, string target)
    {
        try
        {
            if (Directory.Exists(linkPath) || File.Exists(linkPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    Directory.CreateSymbolicLink(linkPath, target);
                }
                catch
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /J \"{linkPath}\" \"{target}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    p?.WaitForExit();
                }
            }
            else
            {
                Directory.CreateSymbolicLink(linkPath, target);
            }
        }
        catch { /* best effort */ }
    }
}
