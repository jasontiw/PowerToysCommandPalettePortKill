// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Commands;
using PortKill.Models;
using System;

namespace PortKill.Pages;

/// <summary>
/// Confirmation page shown before killing a process.
/// Displays process details and requires user confirmation.
/// </summary>
internal sealed partial class ConfirmKillPage : ContentPage
{
    private readonly PortProcessEntry _entry;

    /// <summary>
    /// Creates a confirmation page for the specified port-process entry.
    /// </summary>
    /// <param name="entry">The port-process entry to display.</param>
    public ConfirmKillPage(PortProcessEntry entry)
    {
        _entry = entry;
        Title = "Confirm kill";
        Icon = new IconInfo("\uE74D"); // Delete icon
        Id = $"confirm-kill-{entry.Port.Port}-{entry.Port.ProcessId}";

        // Build content and commands based on whether it's a system process
        var processName = entry.Process?.Name ?? "Unknown";
        var pid = entry.Port.ProcessId;
        var memory = entry.Process?.MemoryUsageMB ?? 0;
        var startTime = entry.Process?.StartTime;
        var exePath = entry.Process?.ExecutablePath ?? "Unknown";

        if (entry.IsSystemProcess)
        {
            _body = $"""
            ## Cannot Kill System Process

            **{processName}** (PID {pid}) is a protected system process.

            Killing this process may cause system instability or require a restart.
            """;

            Commands =
            [
                new CommandContextItem(
                    title: "Go back",
                    name: "Go back",
                    subtitle: "Return to previous page",
                    result: CommandResult.GoBack())
                {
                    Icon = new IconInfo("\uE72B") // Back arrow
                }
            ];
        }
        else
        {
            var startTimeStr = startTime.HasValue && startTime.Value != DateTime.MinValue
                ? startTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "Unknown";

            _body = $"""
            ## Kill {entry.Port.Protocol} Port {entry.Port.Port}?

            | Property | Value |
            |----------|-------|
            | **Process** | {processName} |
            | **PID** | {pid} |
            | **Memory** | {memory} MB |
            | **Started** | {startTimeStr} |
            | **Path** | `{exePath}` |
            | **Local Address** | {entry.Port.LocalAddress} |
            | **State** | {entry.Port.State} |
            """;

            Commands =
            [
                new CommandContextItem(
                    title: $"Kill {processName} (PID {pid})",
                    name: "Kill",
                    subtitle: "Kill the process",
                    result: CommandResult.GoBack(),
                    action: () => new KillProcessCommand(pid, processName).Invoke())
                {
                    Icon = new IconInfo("\uE74D") // Delete icon
                },
                new CommandContextItem(
                    title: "Cancel",
                    name: "Cancel",
                    subtitle: "Do not kill",
                    result: CommandResult.GoBack())
                {
                    Icon = new IconInfo("\uE711") // Cancel icon
                }
            ];
        }
    }

    private readonly string _body;

    /// <inheritdoc/>
    public override IContent[] GetContent() => [new MarkdownContent(_body)];
}

/// <summary>
/// Simple command that returns to the previous page.
/// </summary>
internal sealed partial class BackCommand : InvokableCommand
{
    public override string Name => "Cancel";

    public override IconInfo Icon => new("\uE711"); // Cancel icon

    public override ICommandResult Invoke()
    {
        return CommandResult.GoBack();
    }
}
