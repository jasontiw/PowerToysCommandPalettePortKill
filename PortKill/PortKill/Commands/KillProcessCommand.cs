// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Models;
using PortKill.Services;

namespace PortKill.Commands;

/// <summary>
/// Invokable command that kills a specific process by PID.
/// </summary>
internal sealed partial class KillProcessCommand : InvokableCommand
{
    private readonly int _pid;
    private readonly string _processName;

    /// <summary>
    /// Creates a kill command for the specified process.
    /// </summary>
    /// <param name="pid">The process ID to kill.</param>
    /// <param name="processName">The process name for display.</param>
    public KillProcessCommand(int pid, string processName)
    {
        _pid = pid;
        _processName = processName;
    }

    /// <inheritdoc/>
    public override string Name => $"Kill {_processName} (PID {_pid})";

    /// <inheritdoc/>
    public override IconInfo Icon => Icons.DeleteIcon;

    /// <inheritdoc/>
    public override ICommandResult Invoke()
    {
        var result = PortService.Instance.KillProcess(_pid);

        return result switch
        {
            KillResult.Success => CommandResult.ShowToast(new ToastArgs
            {
                Message = $"Process {_processName} (PID {_pid}) killed successfully.",
                Result = CommandResult.GoHome()  // Refresh the list after kill
            }),

            KillResult.AlreadyDead => CommandResult.ShowToast(new ToastArgs
            {
                Message = "Process no longer running.",
                Result = CommandResult.GoHome()
            }),

            KillResult.AccessDenied => CommandResult.ShowToast(new ToastArgs
            {
                Message = "Cannot kill this process. Try running as administrator.",
                Result = CommandResult.GoBack()
            }),

            KillResult.SystemProcess => CommandResult.ShowToast(new ToastArgs
            {
                Message = "Cannot kill system process. Run as admin?",
                Result = CommandResult.GoBack()
            }),

            _ => CommandResult.GoHome()
        };
    }
}
