// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PortKill.Commands;

/// <summary>
/// A no-operation command used as a placeholder for items that shouldn't do anything when clicked.
/// </summary>
internal sealed partial class NoOpCommand : InvokableCommand
{
    public override string Name => string.Empty;

    public override ICommandResult Invoke()
    {
        return CommandResult.KeepOpen();
    }
}
