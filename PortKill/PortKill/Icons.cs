// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PortKill;

internal static class Icons
{
    // Custom SVG icons
    internal static IconInfo AppIcon { get; } = IconHelpers.FromRelativePath("Assets\\PortKillApp.svg");

    // Font icons (Segoe MDL2 Assets)
    internal static IconInfo DeleteIcon { get; } = new("\uE74D");
    internal static IconInfo ShieldIcon { get; } = new("\uE7BA");
    internal static IconInfo CheckmarkIcon { get; } = new("\uE73E");
    internal static IconInfo BackIcon { get; } = new("\uE72B");
    internal static IconInfo CancelIcon { get; } = new("\uE711");
    internal static IconInfo CopyIcon { get; } = new("\xE8C8");

    // Fluent UI icons (using light/dark theme variants)
    internal static IconInfo Copy { get; } = Create("ic_fluent_copy_20_regular");

    /// <summary>
    /// Creates an icon that supports light/dark theme by loading both variants.
    /// </summary>
    private static IconInfo Create(string name)
    {
        return IconHelpers.FromRelativePaths($"Assets\\Icons\\{name}.light.svg", $"Assets\\Icons\\{name}.dark.svg");
    }
}
