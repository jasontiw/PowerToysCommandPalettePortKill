// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Commands;
using PortKill.Dock;
using PortKill.Pages;

namespace PortKill;

/// <summary>
/// Command provider that registers all Port Kill commands with the Command Palette.
/// Also provides the Dock band for quick port status access.
/// </summary>
public sealed partial class PortKillCommandsProvider : CommandProvider
{
    public PortKillCommandsProvider()
    {
        DisplayName = "Port Kill";
        // Using custom PNG icon
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.targetsize-24.png");
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
                Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.targetsize-24.png"),
                MoreCommands = [
                    new CommandContextItem(new ListPortsPage())
                    {
                        Title = "List active ports",
                        Icon = new IconInfo("\uE7C3")
                    },
                    new CommandContextItem(new CommonDevPortsPage())
                    {
                        Title = "Common ports",
                        Icon = new IconInfo("\uE943")
                    }
                ]
            }
        ];
    }

    /// <summary>
    /// Returns the Dock band showing common dev port status.
    /// </summary>
    public override ICommandItem[] GetDockBands()
    {
        return [new PortKillDockBand()];
    }
}
