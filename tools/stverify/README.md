# stverify

Checks that every `StringTableEntry` FText in a staged pack resolves against the
string tables that pack ships (DataTable rows, DataAsset properties and
blueprint bytecode text literals).

```
E33_REPO=<repo root> dotnet run --project tools/stverify -- <work/mod dir> [--strict]
```

`E33_REPO` is only used to find `tools/Mappings.usmap` through `EditorPaths`.
Without `--strict` the tool only reports; with it, any unresolved reference into
a shipped table exits non-zero.

A coherent English 1.3.5/1.3.6-era stage reports
`ST_MainCharacters_Skills=660, ST_PassiveEffects=1433, ST_Items=843` and zero
missing references apart from four dangling vanilla keys listed in
`docs/windows-stringtable-analysis.md`.
