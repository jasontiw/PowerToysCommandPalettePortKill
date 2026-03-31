// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

using PortKill.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace PortKill.Services;

/// <summary>
/// Core service for querying active ports and managing process lifecycle.
/// Uses netstat -ano as the primary data source for reliable PID-to-port mapping.
/// Implements TTL-based caching to reduce netstat spawn frequency.
/// </summary>
public sealed partial class PortService : IDisposable
{
    private static readonly Lazy<PortService> _instance = new(() => new PortService());

    /// <summary>
    /// Disposes the cache lock.
    /// </summary>
    public void Dispose()
    {
        _cacheLock?.Dispose();
    }

    /// <summary>
    /// Singleton instance of the port service.
    /// </summary>
    public static PortService Instance => _instance.Value;

    /// <summary>
    /// Predefined common development ports for quick access.
    /// </summary>
    public static readonly int[] CommonDevPorts = [3000, 4200, 5000, 5173, 8000, 8080, 9000];

    /// <summary>
    /// Protected system process names that should never be killed.
    /// </summary>
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "svchost", "csrss", "wininit", "lsass",
        "services", "smss", "winlogon", "dwm", "fontdrvhost",
        "Registry", "Memory Compression"
    };

    /// <summary>
    /// Protected system PIDs that should never be killed.
    /// </summary>
    private static readonly HashSet<int> SystemPids = new() { 0, 4 };

    #region Cache Implementation

    /// <summary>
    /// Cache entry storing port data and timestamp.
    /// </summary>
    private (List<PortProcessEntry> Data, DateTime Timestamp)? _cache;

    /// <summary>
    /// Time-to-live for cached port data. Default: 3 seconds.
    /// </summary>
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Lock for thread-safe cache access. Allows concurrent readers, exclusive writer.
    /// </summary>
    private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);

    /// <summary>
    /// Checks if the cache is valid (exists and not expired).
    /// </summary>
    private bool IsCacheValid()
    {
        return _cache.HasValue && 
               DateTime.UtcNow - _cache.Value.Timestamp < _cacheTtl;
    }

    /// <summary>
    /// Fetches fresh port data from netstat and updates the cache.
    /// Must be called with write lock held.
    /// </summary>
    private List<PortProcessEntry> FetchAndCachePorts()
    {
        var entries = FetchPortsFromNetstat();
        _cache = (entries, DateTime.UtcNow);
        return entries;
    }

    /// <summary>
    /// Invalidates the cache. Call after operations that need fresh data.
    /// </summary>
    public void InvalidateCache()
    {
        _cacheLock.EnterWriteLock();
        try
        {
            _cache = null;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    #endregion

    private PortService() { }

    /// <summary>
    /// Gets all active TCP ports with associated process information.
    /// Uses TTL-based caching to reduce netstat spawn frequency.
    /// </summary>
    /// <returns>List of port-process entries.</returns>
    public List<PortProcessEntry> GetActivePorts()
    {
        // Try to get from cache first (read lock - allows concurrent readers)
        _cacheLock.EnterReadLock();
        try
        {
            if (IsCacheValid())
            {
                return _cache!.Value.Data;
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        // Cache miss - acquire write lock and fetch fresh data
        _cacheLock.EnterWriteLock();
        try
        {
            // Double-check after acquiring write lock (another thread may have updated)
            if (!IsCacheValid())
            {
                return FetchAndCachePorts();
            }
            return _cache!.Value.Data;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets port-process entries filtered by port number.
    /// Uses cached data when available.
    /// </summary>
    /// <param name="port">The port number to filter by.</param>
    /// <returns>Matching entries.</returns>
    public List<PortProcessEntry> GetProcessByPort(int port)
    {
        return GetActivePorts()
            .Where(e => e.Port.Port == port)
            .ToList();
    }

    /// <summary>
    /// Gets port-process entries filtered by process name (partial, case-insensitive match).
    /// Uses cached data when available.
    /// </summary>
    /// <param name="name">The process name or partial name to search for.</param>
    /// <returns>Matching entries.</returns>
    public List<PortProcessEntry> GetProcessByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        return GetActivePorts()
            .Where(e => e.Process?.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
    }

    /// <summary>
    /// Checks whether a process is a protected system process.
    /// </summary>
    /// <param name="pid">The process ID to check.</param>
    /// <param name="processName">Optional process name for additional checks.</param>
    /// <returns>True if the process is protected.</returns>
    public bool IsSystemProcess(int pid, string? processName = null)
    {
        if (SystemPids.Contains(pid))
            return true;

        if (!string.IsNullOrEmpty(processName) && SystemProcessNames.Contains(processName))
            return true;

        return false;
    }

    /// <summary>
    /// Kills a process by PID.
    /// Invalidates cache on successful kill to ensure fresh data on next query.
    /// </summary>
    /// <param name="pid">The process ID to kill.</param>
    /// <returns>Result indicating success or failure reason.</returns>
    public KillResult KillProcess(int pid)
    {
        // Check system process protection
        if (IsSystemProcess(pid))
            return KillResult.SystemProcess;

        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();

            // Invalidate cache after successful kill
            InvalidateCache();

            return KillResult.Success;
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            return KillResult.AlreadyDead;
        }
        catch (System.ComponentModel.Win32Exception ex) when ((uint)ex.HResult == 0x80004005)
        {
            // Access denied
            return KillResult.AccessDenied;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PortService] Error killing process {pid}: {ex.Message}");
            return KillResult.AccessDenied;
        }
    }

    /// <summary>
    /// Kills all processes matching the given name.
    /// Invalidates cache after kills to ensure fresh data.
    /// </summary>
    /// <param name="name">The process name to kill.</param>
    /// <returns>Result indicating overall success or failure.</returns>
    public KillResult KillProcessByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return KillResult.AlreadyDead;

        var entries = GetProcessByName(name);
        if (entries.Count == 0)
            return KillResult.AlreadyDead;

        KillResult lastResult = KillResult.AlreadyDead;
        foreach (var entry in entries)
        {
            if (entry.Process != null && entry.CanKill)
            {
                lastResult = KillProcess(entry.Process.Pid);
            }
        }

        return lastResult;
    }

    /// <summary>
    /// Fetches fresh port data from netstat. Used internally by the cache system.
    /// </summary>
    private List<PortProcessEntry> FetchPortsFromNetstat()
    {
        var entries = new List<PortProcessEntry>();

        try
        {
            var netstatOutput = RunNetstatAno();
            var parsedPorts = ParseNetstatOutput(netstatOutput);

            // Group by PID to avoid repeated Process.GetProcessById calls
            var processCache = new Dictionary<int, ProcessInfo?>();

            foreach (var portInfo in parsedPorts)
            {
                if (!processCache.TryGetValue(portInfo.ProcessId, out var processInfo))
                {
                    processInfo = GetProcessInfo(portInfo.ProcessId);
                    processCache[portInfo.ProcessId] = processInfo;
                }

                entries.Add(new PortProcessEntry
                {
                    Port = portInfo,
                    Process = processInfo,
                    IsSystemProcess = IsSystemProcess(portInfo.ProcessId, processInfo?.Name)
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PortService] Error getting active ports: {ex.Message}");
        }

        return entries;
    }

    /// <summary>
    /// Runs netstat -ano and returns the raw output.
    /// </summary>
    private static string RunNetstatAno()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return string.Empty;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    /// <summary>
    /// Parses netstat -ano output into PortInfo objects.
    /// Format: Proto  Local Address  Foreign Address  State  PID
    /// </summary>
    private static List<PortInfo> ParseNetstatOutput(string output)
    {
        var ports = new List<PortInfo>();
        if (string.IsNullOrWhiteSpace(output))
            return ports;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Skip header lines
            if (trimmed.StartsWith("Proto", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Active", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                continue;

            // TCP lines: Proto  LocalAddress  ForeignAddress  State  PID
            // UDP lines: Proto  LocalAddress  *:*  PID
            var protocol = parts[0].ToUpperInvariant();

            if (protocol == "TCP" && parts.Length >= 5)
            {
                var localAddress = parts[1];
                var remoteAddress = parts[2];
                var state = parts[3];
                var pidStr = parts[4];

                if (int.TryParse(pidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) &&
                    TryParsePort(localAddress, out var port))
                {
                    ports.Add(new PortInfo
                    {
                        Port = port,
                        Protocol = protocol,
                        LocalAddress = localAddress,
                        RemoteAddress = remoteAddress,
                        State = state,
                        ProcessId = pid
                    });
                }
            }
            else if (protocol == "UDP" && parts.Length >= 4)
            {
                var localAddress = parts[1];
                var pidStr = parts[3];

                if (int.TryParse(pidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) &&
                    TryParsePort(localAddress, out var port))
                {
                    ports.Add(new PortInfo
                    {
                        Port = port,
                        Protocol = protocol,
                        LocalAddress = localAddress,
                        RemoteAddress = parts.Length > 2 ? parts[2] : string.Empty,
                        State = string.Empty,
                        ProcessId = pid
                    });
                }
            }
        }

        return ports;
    }

    /// <summary>
    /// Extracts port number from an address string like "0.0.0.0:3000" or "[::]:8080".
    /// </summary>
    private static bool TryParsePort(string address, out int port)
    {
        port = 0;

        if (string.IsNullOrEmpty(address))
            return false;

        // Find the last colon (handles both IPv4 and IPv6)
        var lastColon = address.LastIndexOf(':');
        if (lastColon < 0)
            return false;

        var portStr = address[(lastColon + 1)..];
        return int.TryParse(portStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
    }

    /// <summary>
    /// Gets process info for a given PID, returning null if process cannot be accessed.
    /// </summary>
    private static ProcessInfo? GetProcessInfo(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return new ProcessInfo
            {
                Pid = pid,
                Name = process.ProcessName,
                MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
                CpuPercent = 0,
                StartTime = SafeGetStartTime(process),
                ExecutablePath = SafeGetMainModulePath(process)
            };
        }
        catch
        {
            return null;
        }
    }

    private static DateTime SafeGetStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static string SafeGetMainModulePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
