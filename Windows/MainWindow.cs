using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace HousingHistory.Windows;

public class MainWindow : Window, IDisposable
{
    private static readonly Vector4 PlacedColor = new(0.40f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 RemovedColor = new(0.92f, 0.52f, 0.40f, 1f);
    private static readonly Vector4 MovedColor = new(0.45f, 0.70f, 0.95f, 1f);

    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Housing History##HousingHistoryMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(540, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        DrawFilter("Placed", cfg.ShowPlaced, v => { cfg.ShowPlaced = v; cfg.Save(); });
        ImGui.SameLine();
        DrawFilter("Removed", cfg.ShowRemoved, v => { cfg.ShowRemoved = v; cfg.Save(); });
        ImGui.SameLine();
        DrawFilter("Moved", cfg.ShowMoved, v => { cfg.ShowMoved = v; cfg.Save(); });
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            plugin.Monitor.Clear();

        ImGui.Separator();

        using var table = ImRaii.Table("##history", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 64);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Position (X, Y, Z · rot)", ImGuiTableColumnFlags.WidthFixed, 190);
        ImGui.TableHeadersRow();

        var any = false;
        foreach (var e in plugin.Monitor.Entries)
        {
            if (!IsVisible(e.Action, cfg))
                continue;

            any = true;
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Text(e.Time.ToString("HH:mm:ss"));

            ImGui.TableNextColumn();
            DrawAction(e.Action);

            ImGui.TableNextColumn();
            ImGui.Text(e.ItemName);

            ImGui.TableNextColumn();
            if (e.Action == HistoryAction.Moved && e.FromPosition is { } from)
            {
                // Old (greyed) then new — copy the old coords back into BDTH to undo.
                ImGui.TextDisabled(Format(from, e.FromRotation));
                ImGui.Text($"→ {Format(e.Position, e.Rotation)}");
            }
            else
            {
                ImGui.Text(Format(e.Position, e.Rotation));
            }
        }

        if (!any)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No changes logged yet.");
        }
    }

    private static bool IsVisible(HistoryAction a, Configuration cfg) => a switch
    {
        HistoryAction.Placed => cfg.ShowPlaced,
        HistoryAction.Removed => cfg.ShowRemoved,
        HistoryAction.Moved => cfg.ShowMoved,
        _ => true,
    };

    private static void DrawAction(HistoryAction a)
    {
        switch (a)
        {
            case HistoryAction.Placed: ImGui.TextColored(PlacedColor, "Placed"); break;
            case HistoryAction.Removed: ImGui.TextColored(RemovedColor, "Removed"); break;
            case HistoryAction.Moved: ImGui.TextColored(MovedColor, "Moved"); break;
        }
    }

    private static string Format(Vector3 p, float rotationRadians)
    {
        var deg = rotationRadians * 180f / MathF.PI;
        return $"{p.X:0.00}, {p.Y:0.00}, {p.Z:0.00} · {deg:0}°";
    }

    private static void DrawFilter(string label, bool value, Action<bool> set)
    {
        var v = value;
        if (ImGui.Checkbox(label, ref v))
            set(v);
    }
}
