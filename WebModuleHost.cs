using System;
using System.Collections.Generic;
using System.Linq;
using BDVM.Common;

namespace BDVM.Web;

public enum WebModuleLoadState { Loaded, Refused, Failed }

public sealed class WebModuleLoadResult
{
    public WebModuleLoadState State { get; set; }
    public string ModuleId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class LoadedWebModule
{
    public BdvmWebModuleManifest Manifest { get; set; } = new BdvmWebModuleManifest();
    public IReadOnlyList<BdvmWebRoute> Routes { get; set; } = Array.Empty<BdvmWebRoute>();
    public IReadOnlyList<BdvmWebNavigationItem> Navigation { get; set; } = Array.Empty<BdvmWebNavigationItem>();
    public IReadOnlyList<BdvmWebAsset> Assets { get; set; } = Array.Empty<BdvmWebAsset>();
    public IReadOnlyList<BdvmRealtimeSubscription> Subscriptions { get; set; } = Array.Empty<BdvmRealtimeSubscription>();
}

public sealed class WebModuleHost
{
    public static readonly BdvmApiVersion CurrentApi = new BdvmApiVersion(1, 0);
    private readonly Dictionary<string, LoadedWebModule> modules = new Dictionary<string, LoadedWebModule>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> navigation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<LoadedWebModule> Modules => modules.Values.ToArray();

    public WebModuleLoadResult Load(IBdvmWebModule module)
    {
        if (module == null) return Refused("", "missing-module", "Module instance is required.");
        var manifest = module.Manifest;
        var validation = ValidateManifest(manifest);
        if (validation != null) return validation;

        var staging = new StagingRegistrar();
        try { module.Register(staging); }
        catch (Exception exception) { return Failed(manifest.Id, "registration-failed", exception.Message); }

        var contentError = ValidateContent(manifest, staging);
        if (contentError != null) return contentError;

        var loaded = new LoadedWebModule
        {
            Manifest = manifest,
            Routes = staging.Routes.ToArray(),
            Navigation = staging.Navigation.ToArray(),
            Assets = staging.Assets.ToArray(),
            Subscriptions = staging.Subscriptions.ToArray()
        };
        modules.Add(manifest.Id, loaded);
        foreach (var route in loaded.Routes) routes.Add(RouteKey(route));
        foreach (var asset in loaded.Assets) assets.Add(asset.Key);
        foreach (var item in loaded.Navigation) navigation.Add(item.Id);
        return new WebModuleLoadResult { State = WebModuleLoadState.Loaded, ModuleId = manifest.Id, Code = "loaded" };
    }

    private WebModuleLoadResult? ValidateManifest(BdvmWebModuleManifest manifest)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.DisplayName) || string.IsNullOrWhiteSpace(manifest.ModuleVersion))
            return Refused(manifest?.Id ?? "", "invalid-manifest", "Id, display name and version are required.");
        if (modules.ContainsKey(manifest.Id)) return Refused(manifest.Id, "duplicate-module", "Module Id is already loaded.");
        if (manifest.RequiredWebApi == null || !manifest.RequiredWebApi.Supports(CurrentApi)) return Refused(manifest.Id, "incompatible-web-api", "Module does not support Web API " + CurrentApi + ".");
        if (!ValidNamespace(manifest.RouteNamespace, "/api/modules/") || !ValidNamespace(manifest.AssetNamespace, "modules/"))
            return Refused(manifest.Id, "invalid-namespace", "Reserved route and asset namespaces are required.");
        if (Duplicates(manifest.Permissions) || Duplicates(manifest.Capabilities)) return Refused(manifest.Id, "duplicate-declaration", "Manifest declarations must be unique.");
        return null;
    }

    private WebModuleLoadResult? ValidateContent(BdvmWebModuleManifest manifest, StagingRegistrar staging)
    {
        var declaredPermissions = new HashSet<string>(manifest.Permissions, StringComparer.OrdinalIgnoreCase);
        foreach (var route in staging.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Method) || string.IsNullOrWhiteSpace(route.Path) || !route.Path.StartsWith(manifest.RouteNamespace + "/", StringComparison.OrdinalIgnoreCase))
                return Refused(manifest.Id, "route-outside-namespace", route.Path);
            if (!string.IsNullOrWhiteSpace(route.Permission) && !declaredPermissions.Contains(route.Permission)) return Refused(manifest.Id, "undeclared-permission", route.Permission);
            if (routes.Contains(RouteKey(route)) || staging.Routes.Count(x => RouteKey(x) == RouteKey(route)) != 1) return Refused(manifest.Id, "route-collision", RouteKey(route));
        }
        foreach (var asset in staging.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Key) || !asset.Key.StartsWith(manifest.AssetNamespace + "/", StringComparison.OrdinalIgnoreCase)) return Refused(manifest.Id, "asset-outside-namespace", asset.Key);
            if (assets.Contains(asset.Key) || staging.Assets.Count(x => string.Equals(x.Key, asset.Key, StringComparison.OrdinalIgnoreCase)) != 1) return Refused(manifest.Id, "asset-collision", asset.Key);
        }
        foreach (var item in staging.Navigation)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Label) || string.IsNullOrWhiteSpace(item.Path)) return Refused(manifest.Id, "invalid-navigation", item.Id);
            if (navigation.Contains(item.Id) || staging.Navigation.Count(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)) != 1) return Refused(manifest.Id, "navigation-collision", item.Id);
        }
        foreach (var subscription in staging.Subscriptions)
            if (string.IsNullOrWhiteSpace(subscription.Topic) || (!string.IsNullOrWhiteSpace(subscription.Permission) && !declaredPermissions.Contains(subscription.Permission))) return Refused(manifest.Id, "invalid-subscription", subscription.Topic);
        return null;
    }

    private static bool ValidNamespace(string value, string prefix) => !string.IsNullOrWhiteSpace(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.Length > prefix.Length && !value.EndsWith("/", StringComparison.Ordinal);
    private static bool Duplicates(IReadOnlyList<string> values) => values == null || values.Any(string.IsNullOrWhiteSpace) || values.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() != 1);
    private static string RouteKey(BdvmWebRoute route) => route.Method.Trim().ToUpperInvariant() + " " + route.Path.Trim();
    private static WebModuleLoadResult Refused(string id, string code, string detail) => new WebModuleLoadResult { State = WebModuleLoadState.Refused, ModuleId = id, Code = code, Detail = detail };
    private static WebModuleLoadResult Failed(string id, string code, string detail) => new WebModuleLoadResult { State = WebModuleLoadState.Failed, ModuleId = id, Code = code, Detail = detail };

    private sealed class StagingRegistrar : IBdvmWebRegistrar
    {
        public List<BdvmWebRoute> Routes { get; } = new List<BdvmWebRoute>();
        public List<BdvmWebNavigationItem> Navigation { get; } = new List<BdvmWebNavigationItem>();
        public List<BdvmWebAsset> Assets { get; } = new List<BdvmWebAsset>();
        public List<BdvmRealtimeSubscription> Subscriptions { get; } = new List<BdvmRealtimeSubscription>();
        public void AddRoute(BdvmWebRoute route) => Routes.Add(route ?? throw new ArgumentNullException(nameof(route)));
        public void AddNavigation(BdvmWebNavigationItem item) => Navigation.Add(item ?? throw new ArgumentNullException(nameof(item)));
        public void AddAsset(BdvmWebAsset asset) => Assets.Add(asset ?? throw new ArgumentNullException(nameof(asset)));
        public void AddSubscription(BdvmRealtimeSubscription subscription) => Subscriptions.Add(subscription ?? throw new ArgumentNullException(nameof(subscription)));
    }
}
