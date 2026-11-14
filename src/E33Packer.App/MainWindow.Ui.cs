using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace E33Packer.App;

public sealed partial class MainWindow
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush Panel = new SolidColorBrush(Color.Parse("#FF211D17"));
    private static readonly IBrush PanelAlt = new SolidColorBrush(Color.Parse("#FF2B261D"));
    private static readonly IBrush Line = new SolidColorBrush(Color.Parse("#FF3B3529"));

    private static StackPanel Stack(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 2 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private static TabItem Tab(string header, Control content) => new()
    {
        Header = header,
        VerticalContentAlignment = VerticalAlignment.Stretch,
        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(2, 8, 12, 28),
                Child = content,
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        },
    };

    private static Border Card(Control child) => new()
    {
        Background = Panel,
        BorderBrush = Line,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(14, 10, 14, 12),
        Margin = new Thickness(0, 0, 0, 10),
        Child = child,
    };

    private static TextBlock Head(string text) => new()
    {
        Text = text,
        Foreground = Gold,
        FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        FontSize = 17,
        Margin = new Thickness(0, 6, 0, 4),
    };

    private static TextBlock SubHead(string text) => new()
    {
        Text = text,
        Foreground = Gold,
        FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        FontSize = 14,
        Margin = new Thickness(0, 10, 0, 2),
    };

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static TextBlock Label(string text, double width = 0, string? tooltip = null)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Cream,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        if (width > 0) label.Width = width;
        if (!string.IsNullOrWhiteSpace(tooltip)) ToolTip.SetTip(label, tooltip);
        return label;
    }

    private static T Tip<T>(T control, string tooltip) where T : Control
    {
        ToolTip.SetTip(control, tooltip);
        return control;
    }

    /// <summary>Accept both invariant and comma decimal separators.</summary>
    private static bool TryParseNumber(string? text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        return double.TryParse((text ?? "").Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Percentages always show their sign and keep the % inside the box.</summary>
    private static string FormatPercent(double value)
    {
        var v = Math.Abs(value) < 0.00005 ? 0 : value;
        var sign = v > 0 ? "+" : "";
        return sign + v.ToString("0.####", CultureInfo.InvariantCulture) + "%";
    }

    private static bool TryParsePercent(string? text, out double value)
    {
        var s = (text ?? "").Trim();
        if (s.EndsWith("%", StringComparison.Ordinal))
            s = s[..^1].Trim();
        return TryParseNumber(s, out value);
    }

    /// <summary>
    /// Detach the reusable controls from the old visual tree before building a
    /// localized replacement. Avalonia refuses to re-parent a control while its
    /// previous parent still exists.
    /// </summary>
    private static void DetachTree(Control control)
    {
        switch (control)
        {
            case TabControl tabs:
                foreach (var item in tabs.Items.OfType<Control>().ToList())
                    DetachTree(item);
                tabs.Items.Clear();
                break;
            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>().ToList())
                    DetachTree(child);
                panel.Children.Clear();
                break;
            case Expander expander:
            {
                if (expander.Content is Control expanderChild) DetachTree(expanderChild);
                expander.Content = null;
                break;
            }
            case ScrollViewer scroll:
            {
                if (scroll.Content is Control scrollChild) DetachTree(scrollChild);
                scroll.Content = null;
                break;
            }
            case TabItem tabItem:
            {
                if (tabItem.Content is Control tabChild) DetachTree(tabChild);
                tabItem.Content = null;
                break;
            }
            case ContentControl content
                when content is not Button && content is not ToggleButton:
            {
                if (content.Content is Control contentChild) DetachTree(contentChild);
                content.Content = null;
                break;
            }
            case Decorator decorator:
            {
                if (decorator.Child is Control decoratorChild) DetachTree(decoratorChild);
                decorator.Child = null;
                break;
            }
        }
    }

    private CheckBox Toggle(string label, bool value, Action<bool> set, string? tooltip = null)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = value,
            Foreground = Cream,
            FontSize = 13,
            Margin = new Thickness(0, 3, 0, 3),
        };
        if (!string.IsNullOrWhiteSpace(tooltip)) ToolTip.SetTip(box, tooltip);
        var current = value;
        box.IsCheckedChanged += (_, _) =>
        {
            var now = box.IsChecked == true;
            if (now == current) return;
            current = now;
            set(now);
            MarkCustom();
        };
        return box;
    }

    private static void EnableWith(CheckBox toggle, params Control[] dependents)
    {
        void Sync()
        {
            var enabled = toggle.IsChecked == true;
            foreach (var dependent in dependents)
                dependent.IsEnabled = enabled;
        }
        toggle.IsCheckedChanged += (_, _) => Sync();
        Sync();
    }

    private Control IntRow(string label, int value, int min, int max, Action<int> set,
        string? suffix = null, string? tooltip = null)
    {
        var box = new TextBox
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            Width = 110,
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
        };
        var last = value;
        void Commit()
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                v = Math.Clamp(v, min, max);
                box.Text = v.ToString(CultureInfo.InvariantCulture);
                if (v != last)
                {
                    last = v;
                    set(v);
                    MarkCustom();
                }
            }
            else
            {
                box.Text = value.ToString(CultureInfo.InvariantCulture);
            }
        }
        box.TextChanged += (_, _) =>
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                && v >= min && v <= max && v != last)
            {
                last = v;
                set(v);
                MarkCustom();
            }
        };
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Return) Commit();
        };
        return LabeledRow(label, box, suffix, tooltip);
    }

    private Control DoubleRow(string label, double value, double min, double max,
        Action<double> set, string? suffix = null, string? tooltip = null, bool percent = false)
    {
        string Format(double v) => percent
            ? FormatPercent(v)
            : v.ToString("0.####", CultureInfo.InvariantCulture);
        bool TryParse(string? text, out double v) => percent
            ? TryParsePercent(text, out v)
            : TryParseNumber(text, out v);

        var display = Format(value);
        var box = new TextBox
        {
            Text = display,
            Width = percent ? 84 : 110,
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
        };
        var last = TryParse(display, out var initial) ? initial : value;
        void Commit()
        {
            if (TryParse(box.Text, out var v))
            {
                v = Math.Clamp(v, min, max);
                box.Text = Format(v);
                if (Math.Abs(v - last) > 1e-9)
                {
                    last = v;
                    set(v);
                    MarkCustom();
                }
            }
            else
            {
                box.Text = Format(value);
            }
        }
        box.TextChanged += (_, _) =>
        {
            if (TryParse(box.Text, out var v)
                && v >= min && v <= max && Math.Abs(v - last) > 1e-9)
            {
                last = v;
                set(v);
                MarkCustom();
            }
        };
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Return) Commit();
        };
        return LabeledRow(label, box, percent ? null : suffix, tooltip);
    }

    private static Control LabeledRow(string label, Control control, string? suffix,
        string? tooltip = null)
    {
        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("300,Auto,*"),
            Margin = new Thickness(0, 2),
        };
        if (!string.IsNullOrWhiteSpace(tooltip)) ToolTip.SetTip(grid, tooltip);
        grid.Children.Add(Label(label));
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        if (!string.IsNullOrEmpty(suffix))
        {
            var s = new TextBlock
            {
                Text = suffix,
                Foreground = Muted,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(s, 2);
            grid.Children.Add(s);
        }
        return grid;
    }

    private Control ComboRow(string label, IReadOnlyList<string> items, int selected,
        Action<int> set, string? tooltip = null)
    {
        var index = Math.Clamp(selected, 0, Math.Max(0, items.Count - 1));
        var box = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = index,
            Width = 240,
        };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0 || box.SelectedIndex == index) return;
            index = box.SelectedIndex;
            set(index);
            MarkCustom();
        };
        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("300,*"),
            Margin = new Thickness(0, 2),
        };
        if (!string.IsNullOrWhiteSpace(tooltip)) ToolTip.SetTip(grid, tooltip);
        grid.Children.Add(Label(label));
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        return grid;
    }

    /// <summary>Five percentage cells; tiers past <paramref name="editable"/>
    /// show a non-editable note instead of a text box.</summary>
    private Control PercentArrayRow(string label, double[] values, Action<int, double> set,
        string? tooltip = null, int editable = 5, string? lockedText = null,
        string? lockedTooltip = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(Label(label, 300, tooltip));
        for (int i = 0; i < 5; i++)
        {
            if (i >= editable)
            {
                var locked = new TextBlock
                {
                    Text = lockedText ?? "—",
                    Foreground = Muted,
                    FontSize = 12,
                    Width = 76,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (!string.IsNullOrWhiteSpace(lockedTooltip))
                    ToolTip.SetTip(locked, lockedTooltip);
                row.Children.Add(locked);
                continue;
            }

            int index = i;
            var box = new TextBox
            {
                Text = FormatPercent(values[index] * 100),
                Width = 76,
                FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
            };
            var last = values[index] * 100;
            void Commit()
            {
                if (TryParsePercent(box.Text, out var v))
                {
                    v = Math.Clamp(v, -100, 100000);
                    box.Text = FormatPercent(v);
                    if (Math.Abs(v - last) < 1e-9) return;
                    last = v;
                    set(index, v / 100.0);
                    MarkCustom();
                }
                else
                {
                    box.Text = FormatPercent(last);
                }
            }
            box.LostFocus += (_, _) => Commit();
            box.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Return) Commit();
            };
            row.Children.Add(box);
        }
        return row;
    }

    /// <summary>Five numeric cells for a T1–T5 array.</summary>
    private Control TierArrayRow(string label, double[] values, Action<int, double> set,
        double min = 0, double max = 1000, string? tooltip = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(Label(label, 300, tooltip));
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            var box = new TextBox
            {
                Text = values[index].ToString("0.###", CultureInfo.InvariantCulture),
                Width = 58,
                FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
            };
            var last = values[index];
            void Commit()
            {
                if (TryParseNumber(box.Text, out var v))
                {
                    v = Math.Clamp(v, min, max);
                    box.Text = v.ToString("0.###", CultureInfo.InvariantCulture);
                    if (Math.Abs(v - last) < 1e-9) return;
                    last = v;
                    set(index, v);
                    MarkCustom();
                }
                else
                {
                    box.Text = last.ToString("0.###", CultureInfo.InvariantCulture);
                }
            }
            box.LostFocus += (_, _) => Commit();
            box.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Return) Commit();
            };
            row.Children.Add(box);
        }
        return row;
    }

    private static Control TierHeader()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new Border { Width = 300 });
        foreach (var t in new[] { "T1", "T2", "T3", "T4", "T5" })
        {
            row.Children.Add(new TextBlock
            {
                Text = t,
                Foreground = Gold,
                FontSize = 12,
                Width = 76,
                TextAlignment = TextAlignment.Center,
            });
        }
        return row;
    }
}
