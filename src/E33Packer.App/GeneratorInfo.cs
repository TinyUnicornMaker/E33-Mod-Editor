using E33ModEditor.Core.Packer;

namespace E33Packer.App;

/// <summary>
/// Version and pack-name constants shared by the UI, manifests and tests.
/// Keep the revision in sync with the shipped 1.4.0 container names
/// (<c>ExpeditionInfinity_&lt;seed&gt;_v1.4.0_P</c>).
/// </summary>
public static class GeneratorInfo
{
    public const string AppVersion = "1.4.0";
    public const string PackRevision = "_v1.4.0";

    public static string PackName(int seed) =>
        FullPackKit.ModNameFor(seed, PackRevision);
}
