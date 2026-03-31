// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using System.Globalization;
using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Services;

namespace PortKill.Commands;

/// <summary>
/// Fallback command that allows users to type a port number directly
/// and see a "Kill port X" option when a process is using that port.
/// Uses FallbackCommandItem from the Toolkit with the correct constructor pattern.
/// </summary>
internal sealed partial class FallbackKillPortCommand : FallbackCommandItem
{
    private const string _id = "com.portkill.fallback.killport";
    private readonly NoOpCommand _emptyCommand = new();

    public FallbackKillPortCommand() : base("Kill port", _id)
    {
        Command = _emptyCommand;
        Title = string.Empty;
        Subtitle = string.Empty;
        // Using custom PNG icon like the main app
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png");
    }

    /// <inheritdoc/>
    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = _emptyCommand;
            Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png");
            return;
        }

        // Try to parse as port number
        if (!int.TryParse(query.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            // Not a valid port number - hide the fallback
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = _emptyCommand;
            Icon = null;
            return;
        }

        // Validate port range
        if (port < 1 || port > 65535)
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = _emptyCommand;
            Icon = null;
            return;
        }

        // Look for processes on this port
        var entries = PortService.Instance.GetProcessByPort(port);

        if (entries.Count == 0)
        {
            // Port is not in use - show informative message
            Title = $"Port {port} is not in use";
            Subtitle = "No process is currently bound to this port";
            Command = _emptyCommand;
            Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png"); // Info icon
            return;
        }

        // Check if there's a killable process (filter out entries without process)
        var killableEntries = entries.Where(e => e.Process != null && e.CanKill).ToList();

        if (killableEntries.Count == 0)
        {
            // Port in use but no killable process (system process or no process info)
            var firstEntry = entries.First();
            var processName = firstEntry.Process?.Name ?? "Unknown";
            Title = $"Port {port} in use by system process";
            Subtitle = $"Process: {processName} (protected)";
            Command = _emptyCommand;
            Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png"); // Info icon
            return;
        }

        // Group by unique process ID to avoid counting same process multiple times
        // (a process can have multiple network entries on the same port)
        var uniqueProcesses = killableEntries
            .GroupBy(e => e.Process!.Pid)
            .Select(g => g.First())
            .ToList();

        // Found killable process(es)
        if (uniqueProcesses.Count == 1)
        {
            // Single process - show direct kill option
            var entry = uniqueProcesses[0];
            Title = $"Kill port {port}";
            Subtitle = $"{entry.Process!.Name} (PID: {entry.Process.Pid})";
            Command = new KillProcessCommand(entry.Process.Pid, entry.Process.Name);
            Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png");
        }
        else
        {
            // Multiple processes on same port - show selection
            Title = $"Port {port} has {uniqueProcesses.Count} processes";
            Subtitle = "Select which process to kill";

            var commands = uniqueProcesses
                .Select(e => new CommandContextItem(new KillProcessCommand(e.Process!.Pid, e.Process.Name))
                {
                    Title = $"{e.Process.Name} (PID: {e.Process.Pid})",
                    Subtitle = $"Port {port}"
                })
                .ToArray();

            MoreCommands = commands;
            Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-100.png");
        }
    }
}