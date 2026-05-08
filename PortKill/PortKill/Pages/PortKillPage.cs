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
using Microsoft.Win32;
using Windows.UI;

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
        // Ensure fresh data when page is opened
        PortService.Instance.RefreshIfNeeded();

        // Get entries from the ObservableCollection
        var entries = PortService.Instance.Ports.ToList();

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

        // Create native ITag[] for visual badges on the item
        var tags = CreateItemTags(protocol, state);

        var listItem = new ListItem(command)
        {
            // Title now shows port and process name - concise
            Title = $"{port} — {processName}",
            Subtitle = subtitle,
            Icon = icon,
            // Native ITag badges (shown as colored badges on the item)
            Tags = tags,
            // Details for the right panel (list + detail pattern)
            Details = CreateDetails(entry, port, processName, pid, memory, protocol, state)
            // Double-click kills the process
        };

        return listItem;
    }

    /// <summary>
    /// Creates native ITag[] badges for the list item (Protocol + State only).
    /// Colors adapt to dark/light mode based on system theme detection.
    /// </summary>
    private static ITag[] CreateItemTags(string protocol, string state)
    {
        var isDarkMode = IsSystemDarkMode();
        var tags = new List<ITag>();

        // Protocol tag - colors adapt to theme
        var isTcp = protocol == "TCP";
        tags.Add(new Tag(protocol)
        {
            Foreground = isTcp
                ? (isDarkMode ? ColorHelpers.FromRgb(86, 156, 214) : ColorHelpers.FromRgb(0, 90, 190))
                : (isDarkMode ? ColorHelpers.FromRgb(160, 160, 160) : ColorHelpers.FromRgb(100, 100, 100)),
            Background = isTcp
                ? (isDarkMode ? ColorHelpers.FromRgb(32, 55, 80) : ColorHelpers.FromRgb(180, 210, 255))
                : (isDarkMode ? ColorHelpers.FromRgb(60, 60, 60) : ColorHelpers.FromRgb(220, 220, 220))
        });

        // State tag - semantic colors that adapt to theme
        if (!string.IsNullOrEmpty(state))
        {
            var (fg, bg) = state switch
            {
                "LISTENING" => (
                    isDarkMode ? ColorHelpers.FromRgb(72, 200, 72) : ColorHelpers.FromRgb(0, 140, 0),
                    isDarkMode ? ColorHelpers.FromRgb(32, 60, 32) : ColorHelpers.FromRgb(180, 230, 180)
                ),
                "ESTABLISHED" => (
                    isDarkMode ? ColorHelpers.FromRgb(255, 180, 100) : ColorHelpers.FromRgb(220, 130, 0),
                    isDarkMode ? ColorHelpers.FromRgb(60, 45, 25) : ColorHelpers.FromRgb(255, 220, 180)
                ),
                "TIME_WAIT" => (
                    isDarkMode ? ColorHelpers.FromRgb(180, 180, 180) : ColorHelpers.FromRgb(120, 120, 120),
                    isDarkMode ? ColorHelpers.FromRgb(50, 50, 50) : ColorHelpers.FromRgb(220, 220, 220)
                ),
                "CLOSE_WAIT" => (
                    isDarkMode ? ColorHelpers.FromRgb(255, 130, 130) : ColorHelpers.FromRgb(200, 80, 80),
                    isDarkMode ? ColorHelpers.FromRgb(60, 35, 35) : ColorHelpers.FromRgb(255, 200, 200)
                ),
                _ => (
                    isDarkMode ? ColorHelpers.FromRgb(170, 170, 170) : ColorHelpers.FromRgb(110, 110, 110),
                    isDarkMode ? ColorHelpers.FromRgb(45, 45, 45) : ColorHelpers.FromRgb(210, 210, 210)
                )
            };
            tags.Add(new Tag(state) { Foreground = fg, Background = bg });
        }

        return [.. tags];
    }

    /// <summary>
    /// Detects if the system is using dark mode by reading the Windows registry.
    /// </summary>
    private static bool IsSystemDarkMode()
    {
        try
        {
            var registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\CurrentVersion\Themes\Personalize";
            var value = Registry.GetValue(registryKey, "AppsUseLightTheme", 1);
            // 0 = Dark mode, 1 = Light mode (default to light if not found)
            return value is int intValue && intValue == 0;
        }
        catch
        {
            // Default to light mode if registry read fails
            return false;
        }
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