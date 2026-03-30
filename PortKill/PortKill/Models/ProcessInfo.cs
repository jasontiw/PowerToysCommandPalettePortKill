// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

using System;

namespace PortKill.Models;

public sealed class ProcessInfo
{
    public required int Pid { get; init; }
    public required string Name { get; init; }
    public long MemoryUsageMB { get; init; }
    public double CpuPercent { get; init; }
    public DateTime StartTime { get; init; }
    public string ExecutablePath { get; init; } = string.Empty;
    public bool IsSystemProcess { get; init; }
    public bool CanKill { get; init; } = true;

    public string DisplayName => string.IsNullOrEmpty(Name) ? $"PID {Pid}" : Name;
}
