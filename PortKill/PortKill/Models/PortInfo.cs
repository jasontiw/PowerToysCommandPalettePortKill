// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
using System;

namespace PortKill.Models;

public sealed class PortInfo
{
    public required int Port { get; init; }
    public required string Protocol { get; init; }
    public required string LocalAddress { get; init; }
    public string RemoteAddress { get; init; } = string.Empty;
    public required string State { get; init; }
    public required int ProcessId { get; init; }

    public override string ToString() => $"{Port} ({Protocol})";
}
