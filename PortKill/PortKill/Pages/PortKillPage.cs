// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Models;
using PortKill.Services;
using System.Collections.Generic;
using System.Linq;

namespace PortKill.Pages;

/// <summary>
/// Sort options for port list.
/// </summary>
public enum PortSortOption
{
    PortAsc,
    PortDesc,
    ProcessName,
    MemoryHigh,
    MemoryLow
}

/// <summary>
/// Main entry page for the Port Kill extension.
/// Acts as a unified dashboard showing all active ports with details panel.
/// </summary>
internal sealed partial class PortKillPage : ListPage
{
    public static PortSortOption CurrentSort { get; set; } = PortSortOption.PortAsc;

    public PortKillPage()
    {
        // Using custom PNG icon
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png");
        Title = "Port Kill";
        Name = "Open";
        PlaceholderText = "Filter by port or process name...";
        
        // Enable details panel (list + detail pattern like ClipboardHistory)
        ShowDetails = true;
    }

    /// <inheritdoc/>
    public override IListItem[] GetItems()
    {
        var entries = PortService.Instance.GetActivePorts();

        if (entries.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "No active ports found",
                    Subtitle = "All ports are free or no processes are listening",
                    Icon = new IconInfo("\uE73E") // Checkmark icon
                },
                // Keep Common ports option even when empty
                new ListItem(new CommonDevPortsPage())
                {
                    Title = "Common ports",
                    Subtitle = "Quick view of 3000, 4200, 5000, 5173, 8000, 8080, 9000",
                    Icon = new IconInfo("\uE943") // Developer tools icon
                }
            ];
        }

        // Detect sort option from search text
        var sortOption = DetectSortOption(SearchText);
        
        // Apply sorting
        entries = SortPorts(entries, sortOption);

        // Build the item list
        var items = new List<IListItem>();

        // Add sort options at the top (only when no filter active)
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            items.AddRange(GetSortOptions(sortOption));
        }

        // Add port entries (each with Details for the right panel)
        items.AddRange(entries.Select(CreatePortListItem));

        // Keep Common ports option at the bottom
        items.Add(new ListItem(new CommonDevPortsPage())
        {
            Title = "Common ports",
            Subtitle = "Quick view of 3000, 4200, 5000, 5173, 8000, 8080, 9000",
            Icon = new IconInfo("\uE943") // Developer tools icon
        });

        return items.ToArray();
    }

    /// <summary>
    /// Detects the sort option from the search text.
    /// </summary>
    private static PortSortOption DetectSortOption(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return CurrentSort;

        // Check if user typed a sort command
        var lower = searchText.ToLowerInvariant();
        if (lower.Contains("sort:") || lower.Contains("ordenar:"))
        {
            if (lower.Contains("port") && lower.Contains("desc"))
                return PortSortOption.PortDesc;
            if (lower.Contains("port"))
                return PortSortOption.PortAsc;
            if (lower.Contains("process") || lower.Contains("nombre"))
                return PortSortOption.ProcessName;
            if (lower.Contains("memory") && lower.Contains("low"))
                return PortSortOption.MemoryLow;
            if (lower.Contains("memory") || lower.Contains("memoria"))
                return PortSortOption.MemoryHigh;
        }

        return CurrentSort;
    }

    /// <summary>
    /// Returns sort options to display at the top of the list.
    /// </summary>
    private static IEnumerable<IListItem> GetSortOptions(PortSortOption currentSort)
    {
        // Port ascending
        yield return new ListItem(new SortCommand(PortSortOption.PortAsc))
        {
            Title = "Sort: Port (ascending)",
            Subtitle = currentSort == PortSortOption.PortAsc ? "✓ Currently selected" : "Click to sort by port number",
            Icon = new IconInfo("\uE74A") // Sort up icon
        };

        // Port descending
        yield return new ListItem(new SortCommand(PortSortOption.PortDesc))
        {
            Title = "Sort: Port (descending)",
            Subtitle = currentSort == PortSortOption.PortDesc ? "✓ Currently selected" : "Click to sort by port number (highest first)",
            Icon = new IconInfo("\uE74B") // Sort down icon
        };

        // Process name
        yield return new ListItem(new SortCommand(PortSortOption.ProcessName))
        {
            Title = "Sort: Process name (A-Z)",
            Subtitle = currentSort == PortSortOption.ProcessName ? "✓ Currently selected" : "Click to sort alphabetically",
            Icon = new IconInfo("\uE8FD") // Text icon
        };

        // Memory high
        yield return new ListItem(new SortCommand(PortSortOption.MemoryHigh))
        {
            Title = "Sort: Memory (highest first)",
            Subtitle = currentSort == PortSortOption.MemoryHigh ? "✓ Currently selected" : "Click to sort by memory usage",
            Icon = new IconInfo("\uE9D9") // Chart icon
        };
    }

    /// <summary>
    /// Sorts the port entries based on the selected option.
    /// </summary>
    private static List<PortProcessEntry> SortPorts(List<PortProcessEntry> entries, PortSortOption sortOption)
    {
        CurrentSort = sortOption;

        return sortOption switch
        {
            PortSortOption.PortAsc => entries.OrderBy(e => e.Port.Port).ToList(),
            PortSortOption.PortDesc => entries.OrderByDescending(e => e.Port.Port).ToList(),
            PortSortOption.ProcessName => entries.OrderBy(e => e.Process?.Name ?? "").ToList(),
            PortSortOption.MemoryHigh => entries.OrderByDescending(e => e.Process?.MemoryUsageMB ?? 0).ToList(),
            PortSortOption.MemoryLow => entries.OrderBy(e => e.Process?.MemoryUsageMB ?? 0).ToList(),
            _ => entries
        };
    }

    /// <summary>
    /// Creates a list item for a port entry with Details for the side panel.
    /// </summary>
    private static IListItem CreatePortListItem(PortProcessEntry entry)
    {
        var processName = entry.Process?.Name ?? "Unknown";
        var pid = entry.Port.ProcessId;
        var memory = entry.Process?.MemoryUsageMB ?? 0;
        var port = entry.Port.Port;
        var protocol = entry.Port.Protocol;
        var state = entry.Port.State;
        var path = entry.Process?.ExecutablePath ?? "N/A";

        var subtitle = entry.IsSystemProcess
            ? $"Port {port} ({protocol}) — {processName} (PID {pid}) — SYSTEM PROCESS"
            : $"Port {port} ({protocol}) — {processName} (PID {pid}) — {memory} MB";

        // Icon based on process type
        var icon = entry.IsSystemProcess
            ? new IconInfo("\uE7BA")  // Shield for system
            : new IconInfo("\uE74D"); // Delete for user processes

        var listItem = new ListItem(new ConfirmKillPage(entry))
        {
            Title = $"Port {port} — {processName}",
            Subtitle = subtitle,
            Icon = icon,
            // Details for the right panel (list + detail pattern)
            Details = CreateDetails(entry, port, processName, pid, memory, protocol, state, path)
        };

        return listItem;
    }

    /// <summary>
    /// Creates the Details object for a port entry (shown in right panel).
    /// Pattern: https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.ClipboardHistory
    /// </summary>
    private static Details CreateDetails(PortProcessEntry entry, int port, string processName, int pid, long memory, string protocol, string state, string path)
    {
        var metadata = new List<IDetailsElement>();

        // Add process info as details elements
        metadata.Add(new DetailsElement
        {
            Key = "PID",
            Data = new DetailsLink(pid.ToString())
        });

        metadata.Add(new DetailsElement
        {
            Key = "Memory",
            Data = new DetailsLink($"{memory} MB")
        });

        metadata.Add(new DetailsElement
        {
            Key = "Protocol",
            Data = new DetailsLink(protocol)
        });

        metadata.Add(new DetailsElement
        {
            Key = "State",
            Data = new DetailsLink(state)
        });

        // System process warning
        if (entry.IsSystemProcess)
        {
            metadata.Add(new DetailsElement
            {
                Key = "Warning",
                Data = new DetailsLink("SYSTEM PROCESS - Cannot be terminated")
            });
        }

        return new Details
        {
            Title = processName,
            Body = entry.IsSystemProcess ? "System process - cannot be terminated" : "Ready to terminate",
            Metadata = [.. metadata]
        };
    }
}

/// <summary>
/// Command that applies a sort option and refreshes the list.
/// </summary>
internal sealed partial class SortCommand : InvokableCommand
{
    private readonly PortSortOption _sortOption;

    public SortCommand(PortSortOption sortOption)
    {
        _sortOption = sortOption;
    }

    public override string Name => "Sort";

    public override IconInfo Icon => _sortOption switch
    {
        PortSortOption.PortAsc => new IconInfo("\uE74A"),
        PortSortOption.PortDesc => new IconInfo("\uE74B"),
        PortSortOption.ProcessName => new IconInfo("\uE8FD"),
        PortSortOption.MemoryHigh => new IconInfo("\uE9D9"),
        _ => new IconInfo("\uE74A")
    };

    public override ICommandResult Invoke()
    {
        PortKillPage.CurrentSort = _sortOption;
        return CommandResult.GoHome();
    }
}
