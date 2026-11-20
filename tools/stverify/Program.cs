using E33ModEditor.Core;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

var strict = args.Contains("--strict");
// Dangling in vanilla DT_PassiveEffects too; not caused by the mod.
var vanillaDangling = new HashSet<string>(StringComparer.Ordinal) {
    "PASSIVE_Weapon_ATKSPDUpNoAP_Description",
    "PASSIVE_Weapon_ATKLifestealLow_Description",
    "PASSIVE_Weapon_ATKLifestealMedium_Description",
    "PASSIVE_Weapon_ATKLifestealHigh_Description",
};
var map = DataTableEditor.Mappings!;
var modDir = args.First(a => !a.StartsWith("--"));
var tables = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
var refs = new List<(string Asset, string Table, string Key)>();

string Normalize(string id)
{
    var last = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
    if (last.Contains('/')) last = last[(last.LastIndexOf('/') + 1)..];
    return last;
}

foreach (var p in Directory.EnumerateFiles(modDir, "ST_*.uasset", SearchOption.AllDirectories))
{
    var a = new UAsset(p, EngineVersion.VER_UE5_4, map);
    var e = a.Exports.OfType<StringTableExport>().First();
    if (!tables.TryGetValue(e.Table.TableNamespace.Value ?? "", out var set)) tables[e.Table.TableNamespace.Value ?? ""] = set = new(StringComparer.Ordinal);
    foreach (var k in e.Table.Keys) set.Add(k.Value ?? "");
}
Console.Error.WriteLine("tables: " + string.Join(", ", tables.Select(kv => $"{kv.Key}={kv.Value.Count}")));

void AddText(string asset, TextPropertyData t)
{
    if (t.HistoryType != TextHistoryType.StringTableEntry) return;
    var table = Normalize(t.TableId?.Value?.Value ?? "");
    var key = t.Value?.Value ?? "";
    if (table.Length > 0 && key.Length > 0) refs.Add((asset, table, key));
}
void Walk(string asset, PropertyData p)
{
    switch (p)
    {
        case TextPropertyData t: AddText(asset, t); break;
        case StructPropertyData sp: foreach (var c in sp.Value) Walk(asset, c); break;
        case ArrayPropertyData ap: foreach (var c in ap.Value) Walk(asset, c); break;
        case MapPropertyData mp: foreach (var kv in mp.Value) { Walk(asset, kv.Key); Walk(asset, kv.Value); } break;
    }
}
string Str(KismetExpression e) => e is EX_StringConst c ? (c.Value ?? "") : e is EX_UnicodeStringConst u ? (u.Value ?? "") : "";

int scanned = 0, failed = 0;
foreach (var p in Directory.EnumerateFiles(modDir, "*.uasset", SearchOption.AllDirectories))
{
    if (Path.GetFileName(p).StartsWith("ST_", StringComparison.Ordinal)) continue;
    try
    {
        var a = new UAsset(p, EngineVersion.VER_UE5_4, map);
        foreach (var ex in a.Exports)
        {
            if (ex is DataTableExport dte) foreach (var row in dte.Table.Data) Walk(p, row);
            if (ex is NormalExport ne) foreach (var d in ne.Data) Walk(p, d);
            if (ex is FunctionExport fn && fn.ScriptBytecode != null)
                foreach (var exp in fn.ScriptBytecode)
                {
                    uint off = 0;
                    exp.Visit(a, ref off, (e, _) =>
                    {
                        if (e is EX_TextConst tc && tc.Value.TextLiteralType == EBlueprintTextLiteralType.StringTableEntry)
                        {
                            var t = Normalize(Str(tc.Value.StringTableId));
                            var k = Str(tc.Value.StringTableKey);
                            if (t.Length > 0 && k.Length > 0) refs.Add((p, t, k));
                        }
                    });
                }
        }
        scanned++;
    }
    catch { failed++; }
}
Console.Error.WriteLine($"scanned {scanned}, failed {failed}, refs {refs.Count}");

var unknownTable = refs.Where(r => !tables.ContainsKey(r.Table)).ToList();
var known = refs.Where(r => tables.ContainsKey(r.Table)).ToList();
var missing = known.Where(r => !tables[r.Table].Contains(r.Key)).ToList();
var actionable = missing.Where(r => !vanillaDangling.Contains(r.Key)).ToList();
Console.WriteLine($"known {known.Count}, missing {missing.Count} (actionable {actionable.Count}), unknown-table {unknownTable.Count}");
foreach (var g in missing.GroupBy(m => m.Table))
{
    Console.WriteLine($"== {g.Key}: {g.Count()} missing");
    foreach (var m in g.Select(x => $"{Path.GetFileName(x.Asset)} -> {x.Key}").Distinct().Take(50)) Console.WriteLine("   " + m);
}
Console.WriteLine("== unknown tables: " + string.Join(", ", unknownTable.GroupBy(x=>x.Table).Select(g=>$"{g.Key}({g.Count()})")));
if (strict && actionable.Count > 0) Environment.ExitCode = 1;
