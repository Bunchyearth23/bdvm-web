using System;
using System.Collections.Generic;
using System.Linq;

namespace BDVM.Web;

public enum WebConnectionState { Loading, Online, Offline, Reconnecting }

public sealed class WebShellSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string WebApiVersion { get; set; } = WebModuleHost.CurrentApi.ToString();
    public WebConnectionState ConnectionState { get; set; }
    public string CorrelationId { get; set; } = "";
    public IReadOnlyList<WebShellModule> Modules { get; set; } = Array.Empty<WebShellModule>();
    public IReadOnlyList<WebShellNavigation> Navigation { get; set; } = Array.Empty<WebShellNavigation>();
}

public sealed class WebShellModule
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Version { get; set; } = "";
    public IReadOnlyList<string> Capabilities { get; set; } = Array.Empty<string>();
}

public sealed class WebShellNavigation
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
    public int Order { get; set; }
    public string OwnerModuleId { get; set; } = "";
}

public sealed class WebShellService
{
    private readonly WebModuleHost host;
    public WebShellService(WebModuleHost host) { this.host = host ?? throw new ArgumentNullException(nameof(host)); }

    public WebShellSnapshot Snapshot(WebConnectionState state, string correlationId)
    {
        var loaded = host.Modules.OrderBy(x => x.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        return new WebShellSnapshot
        {
            ConnectionState = state,
            CorrelationId = correlationId ?? "",
            Modules = loaded.Select(x => new WebShellModule { Id = x.Manifest.Id, DisplayName = x.Manifest.DisplayName, Version = x.Manifest.ModuleVersion, Capabilities = x.Manifest.Capabilities.ToArray() }).ToArray(),
            Navigation = loaded.SelectMany(x => x.Navigation.Select(item => new WebShellNavigation { Id = item.Id, Label = item.Label, Path = item.Path, Order = item.Order, OwnerModuleId = x.Manifest.Id }))
                .OrderBy(x => x.Order).ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }
}
