using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;
    private bool _busy;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

    public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        => (_execute, _canExecute) = (execute, canExecute);

    public bool CanExecute(object? parameter) => !_busy && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        _busy = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(parameter); }
        finally
        {
            _busy = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>A top-level module tile on the launcher screen.</summary>
public class ModuleVM : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Glyph { get; init; } = "✦";
    public bool IsAvailable { get; init; } = true;

    /// <summary>True for a tile that opens a second tile grid instead of a table.</summary>
    public bool IsHub { get; init; }

    /// <summary>True to skip the record grid and open a dedicated editor.</summary>
    public bool IsDirectEditor { get; init; }

    public ModuleVM[] Children { get; init; } = Array.Empty<ModuleVM>();

    /// <summary>Friendly titles of the data tables this module edits.</summary>
    public string[] Tables { get; init; } = Array.Empty<string>();

    /// <summary>Optional row filter: only records whose <see cref="FilterPath"/> equals <see cref="FilterValue"/>.</summary>
    public string? FilterPath { get; init; }
    public string? FilterValue { get; init; }

    public string Tagline => IsAvailable ? Description : "coming soon";

    /// <summary>Filled by the shell so tiles can invoke <c>OpenModuleCommand</c> without walking the visual tree.</summary>
    public ICommand? OpenCommand { get; set; }
}

/// <summary>Where a displayed field is stored so grouped editors can write back.</summary>
public readonly record struct FieldRef(string Table, string RowName, string Path, bool IsAsset = false);

/// <summary>A visible column of the record list.</summary>
public class ColumnSpec
{
    public required string Header { get; init; }
    public required string Path { get; init; }
    public bool Numeric { get; init; }
}

/// <summary>
/// Generic record row. Column display values live in <see cref="Values"/>
/// keyed by dotted leaf path; deep edits queue into <see cref="AdvancedEdits"/>.
/// </summary>
public class RecordRow : ViewModelBase
{
    internal JObject Baseline { get; set; } = new();
    internal Dictionary<string, JToken> AdvancedEdits { get; } = new();

    /// <summary>Display strings keyed by column path (set by the view model).</summary>
    public Dictionary<string, string> Values { get; } = new();

    public string RowName { get; init; } = "";
    public string DisplayName { get; set; } = "";
    /// <summary>In-game effect text shown on list-row hover (pictos / weapons).</summary>
    public string HoverTitle { get; set; } = "";
    public string HoverBody { get; set; } = "";
    public string PrimaryTable { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public bool IsAsset { get; set; }

    /// <summary>Display path → underlying table/row/path for grouped records.</summary>
    public Dictionary<string, FieldRef> Fields { get; } = new();

    public bool IsDirty { get; private set; }
    public void Dirty() => IsDirty = true;
    public void Clean() => IsDirty = false;
}

/// <summary>One editable scalar property shown in the editor panels.</summary>
public class PropertyLeafVM : ViewModelBase
{
    public string Path { get; init; } = "";
    public string Label { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool IsEditable { get; init; }

    private string _value = "";
    public string Value
    {
        get => _value;
        set
        {
            if (!IsEditable) return;
            if (Set(ref _value, value))
                Edited?.Invoke(Path, value);
        }
    }

    /// <summary>Callback into the owning view model when the user commits an edit.</summary>
    public Action<string, string>? Edited { get; init; }
}
