// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Models;
using PortKill.Pages;
using PortKill.Services;
using System.Collections.Generic;
using System.Linq;

namespace PortKill.Dock;

/// <summary>
/// Dock band that shows common dev port status as buttons.
/// Displays ✓ for free ports and ✗ for occupied ports.
/// Clicking an occupied port opens the confirmation kill page.
/// </summary>
internal sealed partial class PortKillDockBand : WrappedDockItem
{
    public PortKillDockBand()
        : base([], "com.portkill.devports", "Port Kill")
    {
        Items = BuildItems();
    }

    private static IListItem[] BuildItems()
    {
        List<PortProcessEntry> entries;
        try
        {
            entries = PortService.Instance.GetActivePorts();
        }
        catch
        {
            entries = [];
        }

        var portLookup = entries
            .Where(e => e.Port.State == "LISTENING")
            .GroupBy(e => e.Port.Port)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<IListItem>();

        foreach (var port in PortService.CommonDevPorts)
        {
            if (portLookup.TryGetValue(port, out var entry))
            {
                // Port is occupied
                var processName = entry.Process?.Name ?? "Unknown";
                var pid = entry.Port.ProcessId;

                items.Add(new ListItem(new ConfirmKillPage(entry))
                {
                    Title = $"{port}",
                    Subtitle = $"✗ {processName} (PID {pid})",
                    Icon = new IconInfo("\uE711") // Cancel icon
                });
            }
            else
            {
                // Port is free
                items.Add(new ListItem(new NoOpCommand())
                {
                    Title = $"{port}",
                    Subtitle = "✓ Free",
                    Icon = new IconInfo("\uE73E") // Checkmark icon
                });
            }
        }

        // Add a "Refresh" button at the end
        items.Add(new ListItem(new RefreshDockCommand())
        {
            Title = "↻",
            Subtitle = "Refresh port status",
            Icon = new IconInfo("\uE72C") // Refresh icon
        });

        return items.ToArray();
    }
}

/// <summary>
/// Command that returns KeepOpen to stay on the dock.
/// </summary>
internal sealed partial class RefreshDockCommand : InvokableCommand
{
    public override string Name => "Refresh";

    public override IconInfo Icon => new("\uE72C"); // Refresh icon

    public override ICommandResult Invoke()
    {
        return CommandResult.KeepOpen();
    }
}
