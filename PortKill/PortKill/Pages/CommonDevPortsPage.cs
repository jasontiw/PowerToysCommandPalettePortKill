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
/// Page showing common development ports with their status.
/// Allows quick kill of processes occupying these ports.
/// </summary>
internal sealed partial class CommonDevPortsPage : ListPage
{
    public CommonDevPortsPage()
    {
        Icon = new IconInfo("\uE943"); // Developer tools icon
        Title = "Common ports";
        Name = "Common ports";
    }

    /// <inheritdoc/>
    public override IListItem[] GetItems()
    {
        var allPorts = PortService.Instance.GetActivePorts();
        var commonPorts = PortService.CommonDevPorts;

        // Group by port to handle multiple entries per port
        var portGroups = commonPorts.Select(port =>
        {
            var entries = allPorts.Where(e => e.Port.Port == port).ToList();
            return (Port: port, Entries: entries);
        }).ToList();

        // Sort: occupied ports first (by port number), then available
        portGroups = portGroups
            .OrderByDescending(g => g.Entries.Count > 0)
            .ThenBy(g => g.Port)
            .ToList();

        var items = new List<IListItem>();

        // Add summary at top
        var occupiedCount = portGroups.Count(p => p.Entries.Count > 0);
        var availableCount = portGroups.Count - occupiedCount;

        items.Add(new ListItem(new NoOpCommand())
        {
            Title = $"Summary: {occupiedCount} occupied, {availableCount} available",
            Subtitle = "Click on an occupied port to kill the process",
            Icon = new IconInfo("\uE946") // Terminal icon
        });

        // Add port items
        foreach (var group in portGroups)
        {
            if (group.Entries.Count > 0)
            {
                // Port is occupied - show first process
                var entry = group.Entries.First();
                items.Add(CreateOccupiedPortItem(group.Port, entry));
            }
            else
            {
                // Port is available
                items.Add(CreateAvailablePortItem(group.Port));
            }
        }

        return items.ToArray();
    }

    /// <summary>
    /// Creates a list item for an occupied port.
    /// </summary>
    private static IListItem CreateOccupiedPortItem(int port, PortProcessEntry entry)
    {
        var processName = entry.Process?.Name ?? "Unknown";
        var pid = entry.Port.ProcessId;
        var memory = entry.Process?.MemoryUsageMB ?? 0;

        var subtitle = entry.IsSystemProcess
            ? $"Occupied by {processName} (PID {pid}) — SYSTEM PROCESS"
            : $"Occupied by {processName} (PID {pid}) — {memory} MB";

        // Icon: shield for system, delete for user processes
        var icon = entry.IsSystemProcess
            ? new IconInfo("\uE7BA")  // Shield
            : new IconInfo("\uE74D"); // Delete

        return new ListItem(new ConfirmKillPage(entry))
        {
            Title = $"Port {port} — IN USE",
            Subtitle = subtitle,
            Icon = icon
        };
    }

    /// <summary>
    /// Creates a list item for an available port.
    /// </summary>
    private static IListItem CreateAvailablePortItem(int port)
    {
        return new ListItem(new NoOpCommand())
        {
            Title = $"Port {port}",
            Subtitle = "Available",
            Icon = new IconInfo("\uE73E") // Checkmark
        };
    }
}
