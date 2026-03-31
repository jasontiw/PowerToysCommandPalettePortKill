// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Commands;
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
                new ListItem(new Microsoft.CommandPalette.Extensions.Toolkit.NoOpCommand())
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

        // Deduplicate entries by Port + PID combination
        entries = entries
            .GroupBy(e => (e.Port.Port, e.Port.ProcessId))
            .Select(g => g.First())
            .ToList();

        // Apply sorting (always use current sort setting - click on sort options updates it)
        entries = SortPorts(entries, CurrentSort);

        // Add port entries (each with Details for the right panel AND kill command in MoreCommands)
        var items = entries.Select(CreatePortListItem).ToList();

        // Add sort options as footer commands (at the bottom, always visible)
        items.Add(GetSortOptionsFooter(CurrentSort));

        return items.ToArray();
    }

    /// <summary>
    /// Creates a footer item with sort options as MoreCommands.
    /// </summary>
    private static IListItem GetSortOptionsFooter(PortSortOption currentSort)
    {
        var sortCommands = new List<ICommandContextItem>
        {
            new CommandContextItem(new SortCommand(PortSortOption.PortAsc))
            {
                Title = "Port (asc)",
                Icon = new IconInfo("\uE74A")
            },
            new CommandContextItem(new SortCommand(PortSortOption.PortDesc))
            {
                Title = "Port (desc)",
                Icon = new IconInfo("\uE74B")
            },
            new CommandContextItem(new SortCommand(PortSortOption.ProcessName))
            {
                Title = "Process name (A-Z)",
                Icon = new IconInfo("\uE8FD")
            },
            new CommandContextItem(new SortCommand(PortSortOption.MemoryHigh))
            {
                Title = "Memory (high first)",
                Icon = new IconInfo("\uE9D9")
            }
        };

        var currentSortLabel = CurrentSort switch
        {
            PortSortOption.PortAsc => "Port ↑",
            PortSortOption.PortDesc => "Port ↓",
            PortSortOption.ProcessName => "Name A-Z",
            PortSortOption.MemoryHigh => "Memory ↓",
            PortSortOption.MemoryLow => "Memory ↑",
            _ => "Sort"
        };

        return new ListItem(new Microsoft.CommandPalette.Extensions.Toolkit.NoOpCommand())
        {
            Title = $"⚡ Sort: {currentSortLabel}",
            Subtitle = "Click for more sort options",
            Icon = new IconInfo("\uE8B3"), // Sort icon
            MoreCommands = [.. sortCommands]
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
    /// Creates a list item for a port entry with Details for the side panel
    /// AND kill command directly visible in MoreCommands.
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

        // Simplified subtitle - avoid redundancy with title
        var subtitle = entry.IsSystemProcess
            ? $"PID {pid} | {memory} MB | SYSTEM PROCESS"
            : $"PID {pid} | {memory} MB";

        // Icon based on process type
        var icon = entry.IsSystemProcess
            ? new IconInfo("\uE7BA")  // Shield for system
            : new IconInfo("\uE74D"); // Delete for user processes

        // If system process, use NoOpCommand (can't kill)
        // If user process, use KillProcessCommand (double-click to kill)
        var command = entry.IsSystemProcess
            ? new Microsoft.CommandPalette.Extensions.Toolkit.NoOpCommand() as IInvokableCommand
            : new KillProcessCommand(pid, processName);

        var listItem = new ListItem(command)
        {
            // Title now shows port and process name - concise
            Title = $"{port} — {processName}",
            Subtitle = subtitle,
            Icon = icon,
            // Details for the right panel (list + detail pattern)
            Details = CreateDetails(entry, port, processName, pid, memory, protocol, state, path)
            // Double-click kills the process, no need for duplicate MoreCommands
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
