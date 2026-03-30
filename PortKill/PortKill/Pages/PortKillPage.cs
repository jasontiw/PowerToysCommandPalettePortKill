// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PortKill.Pages;

namespace PortKill.Commands;

/// <summary>
/// Main entry page for the Port Kill extension.
/// Shows the primary navigation options: list ports and common dev ports.
/// </summary>
internal sealed partial class PortKillPage : ListPage
{
    public PortKillPage()
    {
        // Using custom PNG icon
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.targetsize-24.png");
        Title = "Port Kill";
        Name = "Open";
    }

    /// <inheritdoc/>
    public override IListItem[] GetItems()
    {
        return
        [
            new ListItem(new ListPortsPage())
            {
                Title = "List active ports",
                Subtitle = "Show all TCP/UDP ports in use",
                Icon = new IconInfo("\uE7C3") // Network icon
            },
            new ListItem(new CommonDevPortsPage())
            {
                Title = "Common dev ports",
                Subtitle = "Quick view of 3000, 4200, 5000, 5173, 8000, 8080, 9000",
                Icon = new IconInfo("\uE943") // Developer tools icon
            }
        ];
    }
}
