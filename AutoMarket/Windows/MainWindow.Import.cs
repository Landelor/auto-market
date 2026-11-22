using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using Dalamud.Bindings.ImGui;

namespace AutoMarket.Windows;

public sealed partial class MainWindow
{
    private readonly record struct ImportEntry(string RequestedName, int Quantity, ItemChoice? Matched);

    private static readonly Regex ImportLinePattern = new(@"^\s*(\d+)\s*[xX]?\s+(.+?)\s*$", RegexOptions.Compiled);

    private string importText = string.Empty;
    private readonly List<ImportEntry> importEntries = [];
    private readonly List<ImportEntry> importBuildQueue = [];
    private readonly List<string> importLog = [];
    private int importBuildIndex = -1;
    private System.Threading.Tasks.Task<List<MarketQuote>>? importBuildTask;
    private CancellationTokenSource? importBuildCancellation;

    private bool ImportBuildRunning => importBuildIndex >= 0 && importBuildIndex < importBuildQueue.Count;

    private void DrawImport()
    {
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("Import list from Artisan"))
            return;

        ImGui.TextWrapped("Paste an Artisan ingredient export (lines like \"40x Dense Aluminum Ore\"), then parse it to match items before queuing a route.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextMultiline("##AutoMarketImportText", ref importText, 8192, new Vector2(-1, 120));

        if (ImGui.Button("Parse list"))
            ParseImportText();
        ImGui.SameLine();
        if (ImGui.Button("Clear import"))
        {
            importText = string.Empty;
            importEntries.Clear();
            importLog.Clear();
        }

        if (importEntries.Count > 0)
        {
            var matchedCount = importEntries.Count(entry => entry.Matched is not null);
            ImGui.TextWrapped($"Matched {matchedCount:N0} of {importEntries.Count:N0} lines to a marketable item.");
            if (ImGui.BeginTable("##AutoMarketImportPreview", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Requested");
                ImGui.TableSetupColumn("Matched item");
                ImGui.TableSetupColumn("Qty");
                ImGui.TableHeadersRow();
                foreach (var entry in importEntries)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(entry.RequestedName);
                    ImGui.TableNextColumn();
                    if (entry.Matched is { } matched)
                        ImGui.Text(matched.Name);
                    else
                        ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No exact match - search it manually above");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.Quantity:N0}");
                }
                ImGui.EndTable();
            }

            if (ImportBuildRunning)
            {
                ImGui.TextDisabled($"Looking up listings... {importBuildIndex + 1:N0}/{importBuildQueue.Count:N0}");
                if (ImGui.SmallButton("Cancel import"))
                {
                    importBuildCancellation?.Cancel();
                    importBuildIndex = importBuildQueue.Count;
                }
            }
            else if (matchedCount > 0 && ImGui.Button($"Queue optimized plans for {matchedCount:N0} matched items"))
            {
                StartImportBuild();
            }
        }

        if (importLog.Count > 0)
        {
            ImGui.TextDisabled("Import results:");
            foreach (var line in importLog)
                ImGui.BulletText(line);
        }
    }

    private void ParseImportText()
    {
        importEntries.Clear();
        importLog.Clear();
        foreach (var rawLine in importText.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0)
                continue;

            var match = ImportLinePattern.Match(line);
            if (!match.Success)
            {
                importEntries.Add(new ImportEntry(line, 1, MatchImportItem(line)));
                continue;
            }

            var quantity = int.TryParse(match.Groups[1].Value, out var parsed) ? Math.Max(1, parsed) : 1;
            var name = match.Groups[2].Value.Trim();
            importEntries.Add(new ImportEntry(name, quantity, MatchImportItem(name)));
        }

        status = importEntries.Count == 0
            ? "No lines found to import."
            : $"Parsed {importEntries.Count:N0} lines, {importEntries.Count(entry => entry.Matched is not null):N0} matched a marketable item.";
    }

    /// <summary>
    /// Resolves an Artisan-exported ingredient name to exactly one marketable item. Only an exact
    /// (case-insensitive) name match, or an unambiguous single substring match, is accepted - an
    /// ambiguous or absent match is left null so the caller never silently buys the wrong item.
    /// </summary>
    private ItemChoice? MatchImportItem(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return null;

        var exact = items.Where(item => item.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0)
            return exact[0];

        var fuzzy = items
            .Where(item => item.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name.Length)
            .ToList();
        return fuzzy.Count == 1 ? fuzzy[0] : null;
    }

    private void StartImportBuild()
    {
        var scope = plugin.CurrentRegion;
        if (string.IsNullOrWhiteSpace(scope))
        {
            status = "Log in to a supported region before importing.";
            return;
        }

        importBuildQueue.Clear();
        importBuildQueue.AddRange(importEntries.Where(entry => entry.Matched is not null));
        if (importBuildQueue.Count == 0)
            return;

        importLog.Clear();
        importBuildIndex = 0;
        BeginImportSearch(scope, importBuildQueue[0]);
    }

    private void BeginImportSearch(string scope, ImportEntry entry)
    {
        importBuildCancellation = new CancellationTokenSource();
        importBuildTask = plugin.MarketClient.FindListingsAsync(scope, entry.Matched!.Value.Id, importBuildCancellation.Token);
    }

    private void FinishImportBuild()
    {
        if (!ImportBuildRunning || importBuildTask is not { IsCompleted: true } completed)
            return;

        var entry = importBuildQueue[importBuildIndex];
        var item = entry.Matched!.Value;
        try
        {
            var result = completed.GetAwaiter().GetResult();
            var merged = plugin.MergeWithCache(item.Id, result).Where(MatchesQuality).ToList();
            var owned = plugin.GetOwnedQuantity(item.Id);
            var remaining = (int)Math.Clamp((long)entry.Quantity - owned, 0, int.MaxValue);
            if (remaining <= 0)
            {
                importLog.Add($"{item.Name}: already have {owned:N0}/{entry.Quantity:N0}; skipped.");
            }
            else
            {
                var plan = PurchasePlanner.PlanQuotes(merged, remaining, 0, 0);
                if (plan.Count == 0)
                {
                    importLog.Add($"{item.Name}: no purchasable listings found.");
                }
                else
                {
                    cart.Add(new CartOrder(nextCartId++, item.Id, item.Name, remaining, 0, plan));
                    importLog.Add($"{item.Name}: added {plan.Sum(listing => listing.Quantity):N0}/{remaining:N0} units, {plan.Sum(listing => listing.Total):N0} gil estimated.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            importLog.Add($"{item.Name}: search cancelled.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Import search failed for {ItemId}", item.Id);
            importLog.Add($"{item.Name}: search failed ({ex.Message}).");
        }
        finally
        {
            importBuildTask = null;
            importBuildCancellation?.Dispose();
            importBuildCancellation = null;
            importBuildIndex++;
            if (importBuildIndex < importBuildQueue.Count)
                BeginImportSearch(plugin.CurrentRegion!, importBuildQueue[importBuildIndex]);
            else
                status = $"Import finished: {importLog.Count:N0} items processed into the shopping list.";
        }
    }
}
