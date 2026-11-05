namespace E33ModEditor.Core;

/// <summary>
/// File.Copy(src, dest, overwrite: true) throws IOException
/// "used by another process" on Unix when src and dest are the same path.
/// Vanilla sources live under work/tables; a previous pack already put copies
/// in work/mod, so FindAsset(work) can pick the dest file as the source.
/// </summary>
public static class FileCopy
{
    public static void Overwrite(string source, string dest)
    {
        var src = Path.GetFullPath(source);
        var dst = Path.GetFullPath(dest);
        if (PathsEqual(src, dst)) return;
        var dir = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        IOException? last = null;
        for (int i = 0; i < 5; i++)
        {
            try
            {
                File.Copy(src, dst, overwrite: true);
                return;
            }
            catch (IOException ex) when (i < 4)
            {
                last = ex;
                Thread.Sleep(40 * (i + 1));
            }
        }
        throw last ?? new IOException("Failed to copy " + src);
    }

    public static void AssetPair(string srcUasset, string destUasset)
    {
        Overwrite(srcUasset, destUasset);
        var uexp = Path.ChangeExtension(srcUasset, ".uexp");
        if (File.Exists(uexp))
            Overwrite(uexp, Path.ChangeExtension(destUasset, ".uexp"));
    }

    /// <summary>Vanilla extracted tables, never work/mod or work/mod_test.</summary>
    public static string? FindTablesAsset(string fileName) =>
        FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), fileName);

    public static string? FindUnder(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal)
        || (OperatingSystem.IsWindows() && string.Equals(a, b, StringComparison.OrdinalIgnoreCase));
}
