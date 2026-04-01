// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Commands;
using PortKill.Pages;

namespace PortKill;

/// <summary>
/// Command provider that registers all Port Kill commands with the Command Palette.
/// </summary>
public sealed partial class PortKillCommandsProvider : CommandProvider
{
    public PortKillCommandsProvider()
    {
        DisplayName = "Port Kill";
        // Using custom PNG icon
        Icon = Icons.AppIcon;
        Id = "com.portkill.provider";
    }

    /// <summary>
    /// Returns the top-level commands exposed by this extension.
    /// </summary>
    public override ICommandItem[] TopLevelCommands()
    {
        return
        [
            new ListItem(new PortKillPage())
            {
                Title = "Port Kill",
                Subtitle = "Find and kill processes blocking TCP ports",
                // Using custom PNG icon
                Icon = Icons.AppIcon,
                MoreCommands = []
            }
        ];
    }

    /// <summary>
    /// Returns fallback commands that can be invoked without navigating to the extension.
    /// </summary>
    public override IFallbackCommandItem[] FallbackCommands()
    {
        return
        [
            new FallbackKillPortCommand()
        ];
    }
}
