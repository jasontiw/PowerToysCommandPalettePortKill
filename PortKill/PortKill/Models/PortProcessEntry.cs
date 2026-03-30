// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

namespace PortKill.Models;

public sealed class PortProcessEntry
{
    public required PortInfo Port { get; set; }
    public ProcessInfo? Process { get; set; }

    /// <summary>
    /// Whether this is a protected system process.
    /// </summary>
    public bool IsSystemProcess { get; set; }

    /// <summary>
    /// Whether the user can kill this process. False when IsSystemProcess is true.
    /// </summary>
    public bool CanKill => !IsSystemProcess;
}
