// ------------------------------------------------------------
// 
// Copyright (c) @Jasontiw. All rights reserved.
// 
// ------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace PortKill;

[Guid("f1172d0c-c4f4-4a1a-9560-e949e8993e17")]
public sealed partial class PortKill : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly PortKillCommandsProvider _provider = new();

    public PortKill(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose() => this._extensionDisposedEvent.Set();
}
