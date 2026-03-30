// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------
namespace PortKill.Models;

/// <summary>
/// Result of a kill process operation.
/// </summary>
public enum KillResult
{
    /// <summary>
    /// Process was successfully terminated.
    /// </summary>
    Success,

    /// <summary>
    /// Insufficient permissions to kill the process.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Process no longer exists.
    /// </summary>
    AlreadyDead,

    /// <summary>
    /// Process is a protected system process and cannot be killed.
    /// </summary>
    SystemProcess
}
