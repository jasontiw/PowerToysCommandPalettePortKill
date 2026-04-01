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
/// Main entry page for the Port Kill extension.
/// Acts as a unified dashboard showing all active ports with details panel.
/// </summary>
internal sealed partial class PortKillPage : ListPage
{
    public PortKillPage()
    {
        // Using custom PNG icon
        Icon = Icons.AppIcon;
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
                    Icon = Icons.CheckmarkIcon
                }
            ];
        }

        // Deduplicate entries by Port + PID combination
        entries = entries
            .GroupBy(e => (e.Port.Port, e.Port.ProcessId))
            .Select(g => g.First())
            .ToList();

        // Sort by port number ascending (default)
        entries = entries.OrderBy(e => e.Port.Port).ToList();

        // Add port entries (each with Details for the right panel)
        var items = entries.Select(CreatePortListItem).ToList();

        return items.ToArray();
    }

    /// <summary>
    /// Creates a list item for a port entry with Details for the side panel.
    /// Double-click kills the process.
    /// </summary>
    private static IListItem CreatePortListItem(PortProcessEntry entry)
    {
        var processName = entry.Process?.Name ?? "Unknown";
        var pid = entry.Port.ProcessId;
        var memory = entry.Process?.MemoryUsageMB ?? 0;
        var port = entry.Port.Port;
        var protocol = entry.Port.Protocol;
        var state = entry.Port.State;

        // Simplified subtitle - avoid redundancy with title
        var subtitle = entry.IsSystemProcess
            ? $"PID {pid} | {memory} MB | SYSTEM PROCESS"
            : $"PID {pid} | {memory} MB";

        // Icon based on process type
        var icon = entry.IsSystemProcess
            ? Icons.ShieldIcon  // Shield for system
            : Icons.DeleteIcon; // Delete for user processes

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
            Details = CreateDetails(entry, port, processName, pid, memory, protocol, state)
            // Double-click kills the process
        };

        return listItem;
    }

    /// <summary>
    /// Creates the Details object for a port entry (shown in right panel).
    /// </summary>
    private static Details CreateDetails(PortProcessEntry entry, int port, string processName, int pid, long memory, string protocol, string state)
    {
        var metadata = new List<IDetailsElement>();

        // Add process info as details elements
        metadata.Add(new DetailsElement { Key = "PID", Data = new DetailsLink(pid.ToString()) });
        metadata.Add(new DetailsElement { Key = "Memory", Data = new DetailsLink($"{memory} MB") });
        metadata.Add(new DetailsElement { Key = "Protocol", Data = new DetailsLink(protocol) });
        metadata.Add(new DetailsElement { Key = "State", Data = new DetailsLink(state) });

        // System process warning
        if (entry.IsSystemProcess)
        {
            metadata.Add(new DetailsElement { Key = "Warning", Data = new DetailsLink("SYSTEM PROCESS - Cannot be terminated") });
        }

        return new Details
        {
            Title = processName,
            Body = entry.IsSystemProcess ? "System process - cannot be terminated" : "Ready to terminate",
            Metadata = [.. metadata]
        };
    }
}