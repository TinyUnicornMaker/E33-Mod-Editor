using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Skill-button stain icons. Vanilla <c>WBP_GenericElement_StainIconeOnly</c>
/// maps Dark (EAttackType ordinal 6) to <c>EX_NoObject</c> — a blank white
/// square. This kit points that case at <c>T_HUD_Icon_Element_DARK</c> and
/// builds an either-or generate widget (Light / Dark with a slash) used by
/// Chiaroscuro's cloned StainsMechanic.
/// </summary>
public static class StainUiKit
{
    public const string IconeOnly = "WBP_GenericElement_StainIconeOnly";
    public const string RequiredStains = "WBP_SkillButtonModifier_RequiredStains";
    public const string SlashWidget = "WBP_LuneGenerateSlash";
    public const string EitherOrWidget = "WBP_SkillButtonModifier_RequiredStainsEitherOr";
    public const string EitherOrMechanic = "BP_SkillDataComponent_StainsMechanic_EitherOr";
    public const string VanillaMechanic = "BP_SkillDataComponent_StainsMechanic";
    public const string DarkTexPkg = "/Game/UI/Resources/Textures/Icons/Elements/T_HUD_Icon_Element_DARK";
    public const string DarkTexName = "T_HUD_Icon_Element_DARK";

    public static int Apply(string modDir)
    {
        int n = 0;
        n += PatchDarkIcon(modDir);
        n += BuildSlashWidget(modDir);
        n += BuildEitherOrModifier(modDir);
        n += BuildEitherOrMechanic(modDir);
        return n;
    }

    private static UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null ? new UAsset(p, EngineVersion.VER_UE5_4, map) : new UAsset(p, EngineVersion.VER_UE5_4);
    }

    private static string Stage(string srcUasset, string modDir, string? newStem = null)
    {
        var full = srcUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
        var rel = full[(idx + marker.Length)..];
        if (newStem is not null)
            rel = rel.Replace(Path.GetFileNameWithoutExtension(srcUasset), newStem, StringComparison.Ordinal);
        var dest = Path.Combine(modDir, "Sandfall", "Content", rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        return dest;
    }

    private static string EnsureExtracted(string filter)
    {
        var hit = FileCopy.FindTablesAsset(filter + ".uasset");
        if (hit is not null) return hit;
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter);
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, filter);
        return FileCopy.FindTablesAsset(filter + ".uasset")
            ?? throw new FileNotFoundException(filter);
    }

    /// <summary>Dark (byte 6) was NoObject → white square. Point it at the HUD Dark element icon.</summary>
    private static int PatchDarkIcon(string modDir)
    {
        var src = EnsureExtracted(IconeOnly);
        var dest = Stage(src, modDir);
        FileCopy.AssetPair(src, dest);
        var u = Open(dest);
        var uber = u.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal));
        if (uber?.ScriptBytecode is null) return 0;

        var darkIdx = LuneSkillsKit.EnsureObjectImport(u, DarkTexPkg, DarkTexName, "Texture2D", "/Script/Engine");
        // The compiled ubergraph assigns the texture locals TWICE (one block per use
        // site); the LAST assignment, right before the SetBrushFromTexture switch, is the
        // one that wins at runtime. Patching only the first left the effective one as
        // NoObject -> still a white square. Patch every occurrence.
        int patched = 0;
        uint pos = 0;
        for (int i = 0; i < uber.ScriptBytecode.Length; i++)
        {
            var stmt = uber.ScriptBytecode[i];
            uint oldSize = (uint)stmt.GetSize(u);
            if (stmt is EX_LetObj let
                && let.VariableExpression is EX_LocalVariable lv
                && (lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? "") == "Temp_object_Variable_2"
                && let.AssignmentExpression is EX_NoObject)
            {
                let.AssignmentExpression = new EX_ObjectConst { Value = darkIdx };
                patched++;
            }
            uint newSize = (uint)stmt.GetSize(u);
            if (newSize > oldSize)
                MayhemFixKit.ShiftAbsolute(u, uber, pos, newSize - oldSize, null);
            pos += newSize; // post-patch size keeps later offsets accurate
        }
        if (patched > 0)
        {
            MayhemFixKit.RecalcContextSkips(u, uber);
            u.Write(dest);
        }
        return patched;
    }

    private static int BuildSlashWidget(string modDir)
    {
        var src = EnsureExtracted(RequiredStains);
        var dest = Stage(src, modDir, SlashWidget);
        FileCopy.AssetPair(src, dest);
        var u = Open(dest);
        LuneSkillsKit.RetargetPairs(u, dest, (RequiredStains, SlashWidget));
        foreach (var ex in u.Exports.OfType<NormalExport>())
        {
            if (ex.ObjectName.ToString() == "PlusText")
            {
                foreach (var p in ex.Data)
                    if (p is TextPropertyData t && p.Name.ToString() == "Text")
                    {
                        t.HistoryType = TextHistoryType.Base;
                        t.CultureInvariantString = new FString("/");
                        t.Value = new FString("/");
                    }
            }
        }
        u.FolderName = new FString("/Game/Gameplay/Battle/Stains/" + SlashWidget);
        u.Write(dest);
        return 1;
    }

    private static int BuildEitherOrModifier(string modDir)
    {
        var src = EnsureExtracted(RequiredStains);
        var dest = Stage(src, modDir, EitherOrWidget);
        FileCopy.AssetPair(src, dest);
        var u = Open(dest);
        LuneSkillsKit.RetargetPairs(u, dest, (RequiredStains, EitherOrWidget));
        InsertSlashEntries(u);
        u.FolderName = new FString("/Game/Gameplay/Battle/Stains/" + EitherOrWidget);
        u.Write(dest);
        return 1;
    }

    /// <summary>
    /// Before the first icon of the 2nd+ stain type, spawn <see cref="SlashWidget"/>
    /// in the DynamicEntryBox so generate reads Light / Dark instead of Light Dark.
    /// </summary>
    private static void InsertSlashEntries(UAsset u)
    {
        var fn = u.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "UpdateStainsList");
        if (fn?.ScriptBytecode is null) return;
        var stmts = fn.ScriptBytecode.ToList();

        int createIdx = -1;
        EX_LetObj? createTpl = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isCreate = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_FinalFunction ffn && ffn.StackNode is not null && ffn.StackNode.IsImport()
                    && ffn.StackNode.ToImport(u)?.ObjectName.ToString() == "BP_CreateEntryOfClass")
                    isCreate = true;
            });
            if (isCreate && stmts[i] is EX_LetObj lo) { createIdx = i; createTpl = lo; break; }
        }
        if (createIdx < 0 || createTpl is null) return;

        var slashIdx = LuneSkillsKit.EnsureObjectImport(u,
            "/Game/Gameplay/Battle/Stains/" + SlashWidget, SlashWidget + "_C",
            SlashWidget + "_C", "/Script/UMG");

        var slashCreate = (EX_LetObj)MayhemFixKit.CloneKismet(createTpl);
        MayhemFixKit.Walk(new[] { slashCreate }, e =>
        {
            if (e is EX_ObjectConst oc)
                oc.Value = slashIdx;
        });

        KismetPropertyPointer? indexPtr = null, tempPtr = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is not EX_LocalVariable lv) return;
                var nm = lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? "";
                if (nm == "Temp_int_Array_Index_Variable" && indexPtr is null) indexPtr = lv.Variable;
                if (nm == "Temp_int_Variable" && tempPtr is null) tempPtr = lv.Variable;
            });
        if (indexPtr is null || tempPtr is null) return;

        FPackageIndex? lessEq = null;
        for (int i = 0; i < u.Imports.Count; i++)
            if (u.Imports[i].ObjectName.ToString() == "LessEqual_IntInt")
            { lessEq = FPackageIndex.FromImport(i); break; }
        if (lessEq is null) return;

        // JumpIfNot(LessEqual(1, Array_Index)): skip slash on type index 0.
        var condSecondType = new EX_FinalFunction
        {
            StackNode = lessEq,
            Parameters = new KismetExpression[]
            {
                new EX_IntConst { Value = 1 },
                new EX_LocalVariable { Variable = indexPtr },
            },
        };
        // JumpIfNot(LessEqual(Temp_int_Variable, 1)): skip slash on 2nd+ copy of the same type.
        var condFirstIcon = new EX_FinalFunction
        {
            StackNode = lessEq,
            Parameters = new KismetExpression[]
            {
                new EX_LocalVariable { Variable = tempPtr },
                new EX_IntConst { Value = 1 },
            },
        };

        uint insertOffset = 0;
        for (int i = 0; i < createIdx; i++) insertOffset += (uint)stmts[i].GetSize(u);
        var j1 = new EX_JumpIfNot { BooleanExpression = condSecondType, CodeOffset = 0 };
        var j2 = new EX_JumpIfNot { BooleanExpression = condFirstIcon, CodeOffset = 0 };
        uint j1Size = (uint)j1.GetSize(u);
        uint j2Size = (uint)j2.GetSize(u);
        uint slashSize = (uint)slashCreate.GetSize(u);
        uint after = insertOffset + j1Size + j2Size + slashSize;
        j1.CodeOffset = after;
        j2.CodeOffset = after;
        MayhemFixKit.ShiftAbsolute(u, fn, insertOffset, j1Size + j2Size + slashSize, null);
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(createIdx, new KismetExpression[] { j1, j2, slashCreate });
        fn.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(u, fn);
    }

    private static int BuildEitherOrMechanic(string modDir)
    {
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work"), VanillaMechanic + ".uasset")
            ?? EnsureExtracted(VanillaMechanic);
        var dest = Stage(src, modDir, EitherOrMechanic);
        FileCopy.AssetPair(src, dest);
        var u = Open(dest);
        LuneSkillsKit.RetargetPairs(u, dest,
            (VanillaMechanic, EitherOrMechanic),
            (RequiredStains, EitherOrWidget));
        u.FolderName = new FString("/Game/Gameplay/SkillTree/SkillDataComponents/UniqueMechanic_Stains/" + EitherOrMechanic);
        u.Write(dest);
        // The DA child export's class is this clone; graft the vanilla StainsMechanic
        // schema under the new class name so UAssetAPI can write the maps.
        StainsMechanicKit.EnsureSchemas();
        var map = DataTableEditor.Mappings;
        if (map is not null && map.Schemas.TryGetValue("BP_SkillDataComponent_StainsMechanic_C", out var schema))
            map.Schemas[EitherOrMechanic + "_C"] = schema;
        return 1;
    }
}
