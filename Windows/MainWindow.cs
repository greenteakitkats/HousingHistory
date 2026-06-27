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

    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Housing History##HousingHistoryMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        var showPlaced = cfg.ShowPlaced;
        if (ImGui.Checkbox("Placed", ref showPlaced))
        {
            cfg.ShowPlaced = showPlaced;
            cfg.Save();
        }

        ImGui.SameLine();
        var showRemoved = cfg.ShowRemoved;
        if (ImGui.Checkbox("Removed", ref showRemoved))
        {
            cfg.ShowRemoved = showRemoved;
            cfg.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            plugin.Monitor.Clear();

        ImGui.Separator();

        using var table = ImRaii.Table("##history", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        var any = false;
        foreach (var e in plugin.Monitor.Entries)
        {
            if (e.Action == HistoryAction.Placed && !cfg.ShowPlaced) continue;
            if (e.Action == HistoryAction.Removed && !cfg.ShowRemoved) continue;

            any = true;
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Text(e.Time.ToString("HH:mm:ss"));

            ImGui.TableNextColumn();
            if (e.Action == HistoryAction.Placed)
                ImGui.TextColored(PlacedColor, "Placed");
            else
                ImGui.TextColored(RemovedColor, "Removed");

            ImGui.TableNextColumn();
            ImGui.Text(e.ItemName);
        }

        if (!any)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No changes logged yet.");
        }
    }
}
