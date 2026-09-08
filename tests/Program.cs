using System;
using System.Linq;
using BDVM.Common;
using BDVM.Web;

internal static class Program
{
    private static int checks;
    private static int failures;
    private static int Main()
    {
        Run(TestAuthenticatedIntentAndReplay);
        Run(TestSecurityRefusals);
        Run(TestPayloadAndRateLimits);
        Run(TestShellAndModuleIsolation);
        Run(TestNamespaceAndCapabilityCollisions);
        Console.WriteLine("BDVM.Web tests: " + (checks - failures) + "/" + checks + " passed");
        return failures == 0 ? 0 : 1;
    }

    private static void TestAuthenticatedIntentAndReplay()
    {
        var host = Host(); var sessions = new WebSessionRegistry(); var executor = new FakeExecutor();
        var session = sessions.Create("player-a", "localhost:8080", new[] { "test.write" });
        var gateway = new WebIntentGateway(host, sessions, executor);
        var request = Request(session); var envelope = Intent("request-1", "correlation-1");
        var first = gateway.Submit(request, envelope); var replay = gateway.Submit(request, envelope);
        Check(first.State == WebIntentState.Succeeded && executor.Calls == 1, "authoritative executor should run once");
        Check(replay.State == WebIntentState.Succeeded && replay.Replayed && executor.Calls == 1, "identical retry should return cached result");
        envelope.PayloadJson = "{\"changed\":true}";
        Check(gateway.Submit(request, envelope).Code == "idempotency-conflict" && executor.Calls == 1, "same key with another payload must fail");
    }

    private static void TestSecurityRefusals()
    {
        var host = Host(); var sessions = new WebSessionRegistry(); var executor = new FakeExecutor(); var gateway = new WebIntentGateway(host, sessions, executor);
        var noPermission = sessions.Create("player-a", "localhost:8080", Array.Empty<string>());
        Check(gateway.Submit(Request(noPermission), Intent("a", "c")).Code == "unauthorized", "permission must be checked host-side");
        var allowed = sessions.Create("player-b", "localhost:8080", new[] { "test.write" });
        var csrf = Request(allowed); csrf.CsrfToken += "x";
        Check(gateway.Submit(csrf, Intent("b", "d")).Code == "unauthorized", "CSRF mismatch must fail");
        var origin = Request(allowed); origin.OriginAuthority = "attacker.invalid";
        Check(gateway.Submit(origin, Intent("c", "e")).Code == "unauthorized" && executor.Calls == 0, "cross-origin request must not reach authority");
    }

    private static void TestPayloadAndRateLimits()
    {
        var host = Host(); var sessions = new WebSessionRegistry(); var executor = new FakeExecutor();
        var session = sessions.Create("player-a", "localhost:8080", new[] { "test.write" });
        var gateway = new WebIntentGateway(host, sessions, executor, new SlidingWindowRateLimiter(1));
        var oversized = Intent("large", "large-c"); oversized.PayloadJson = new string('x', WebIntentGateway.MaximumPayloadBytes + 1);
        Check(gateway.Submit(Request(session), oversized).Code == "payload-too-large", "oversized payload must fail before authority");
        var malformed = Intent("malformed", "malformed-c"); malformed.PayloadJson = "not-json";
        Check(new WebIntentGateway(host, sessions, executor).Submit(Request(session), malformed).Code == "invalid-payload", "malformed JSON object must fail before authority");
        var second = new WebIntentGateway(host, sessions, executor, new SlidingWindowRateLimiter(1));
        Check(second.Submit(Request(session), Intent("one", "one-c")).State == WebIntentState.Succeeded, "first bounded request should pass");
        Check(second.Submit(Request(session), Intent("two", "two-c")).Code == "rate-limited", "rate limit should refuse burst");
    }

    private static void TestShellAndModuleIsolation()
    {
        var host = Host();
        Check(host.Load(new BadModule()).State == WebModuleLoadState.Failed && host.Modules.Count == 1, "failed module must not disable loaded modules");
        var shell = new WebShellService(host).Snapshot(WebConnectionState.Online, "shell-c");
        Check(shell.SchemaVersion == 1 && shell.Modules.Single().Id == "BDVM.Test", "shell should expose versioned loaded module state");
        Check(shell.Navigation.Single().OwnerModuleId == "BDVM.Test", "navigation should retain module ownership");
    }

    private static void TestNamespaceAndCapabilityCollisions()
    {
        var host = Host();
        var collision = host.Load(new CollisionModule());
        Check(collision.State == WebModuleLoadState.Refused && collision.Code == "capability-collision", "capabilities must have one owner");
        var traversal = new TraversalModule();
        Check(new WebModuleHost().Load(traversal).Code == "route-outside-namespace", "route traversal must fail before publication");
    }

    private static WebModuleHost Host() { var host = new WebModuleHost(); Check(host.Load(new TestModule()).State == WebModuleLoadState.Loaded, "test module should load"); return host; }
    private static WebSessionRequest Request(WebSession session) => new WebSessionRequest { SessionId = session.SessionId, CsrfToken = session.CsrfToken, OriginAuthority = session.OriginAuthority };
    private static WebIntentEnvelope Intent(string key, string correlation) => new WebIntentEnvelope { ModuleId = "BDVM.Test", IntentType = "bdvm.test.write.v1", IdempotencyKey = key, CorrelationId = correlation, ExpectedVersion = 2, PayloadJson = "{}" };
    private static void Run(Action test) { try { test(); } catch (Exception error) { failures++; Console.Error.WriteLine(error); } }
    private static void Check(bool condition, string message) { checks++; if (!condition) { failures++; Console.Error.WriteLine("FAIL: " + message); } }

    private sealed class FakeExecutor : IAuthoritativeWebIntentExecutor
    {
        public int Calls { get; private set; }
        public WebIntentResult Execute(string authenticatedPrincipal, WebIntentEnvelope envelope) { Calls++; return new WebIntentResult { State = WebIntentState.Succeeded, Code = "accepted", CorrelationId = envelope.CorrelationId, ResultJson = "{\"version\":3}" }; }
    }
    private sealed class TestModule : IBdvmWebModule
    {
        public BdvmWebModuleManifest Manifest { get; } = new BdvmWebModuleManifest { Id = "BDVM.Test", DisplayName = "Test", ModuleVersion = "1.0.0", RequiredWebApi = new BdvmApiRange(new BdvmApiVersion(1, 0), new BdvmApiVersion(1, 0)), RouteNamespace = "/api/modules/bdvm.test", AssetNamespace = "modules/bdvm.test", Permissions = new[] { "test.write" }, Capabilities = new[] { "bdvm.test.v1" } };
        public void Register(IBdvmWebRegistrar registrar) { registrar.AddNavigation(new BdvmWebNavigationItem { Id = "test", Label = "Test", Path = "/test" }); registrar.AddRoute(new BdvmWebRoute { Method = "POST", Path = "/api/modules/bdvm.test/write", Permission = "test.write", IntentType = "bdvm.test.write.v1" }); }
    }
    private sealed class BadModule : IBdvmWebModule
    {
        public BdvmWebModuleManifest Manifest { get; } = new BdvmWebModuleManifest { Id = "BDVM.Bad", DisplayName = "Bad", ModuleVersion = "1.0.0", RequiredWebApi = new BdvmApiRange(new BdvmApiVersion(1, 0), new BdvmApiVersion(1, 0)), RouteNamespace = "/api/modules/bdvm.bad", AssetNamespace = "modules/bdvm.bad" };
        public void Register(IBdvmWebRegistrar registrar) { throw new InvalidOperationException("expected"); }
    }
    private sealed class CollisionModule : IBdvmWebModule
    {
        public BdvmWebModuleManifest Manifest { get; } = new BdvmWebModuleManifest { Id = "BDVM.Collision", DisplayName = "Collision", ModuleVersion = "1.0.0", RequiredWebApi = new BdvmApiRange(new BdvmApiVersion(1, 0), new BdvmApiVersion(1, 0)), RouteNamespace = "/api/modules/bdvm.collision", AssetNamespace = "modules/bdvm.collision", Capabilities = new[] { "bdvm.test.v1" } };
        public void Register(IBdvmWebRegistrar registrar) { }
    }
    private sealed class TraversalModule : IBdvmWebModule
    {
        public BdvmWebModuleManifest Manifest { get; } = new BdvmWebModuleManifest { Id = "BDVM.Traversal", DisplayName = "Traversal", ModuleVersion = "1.0.0", RequiredWebApi = new BdvmApiRange(new BdvmApiVersion(1, 0), new BdvmApiVersion(1, 0)), RouteNamespace = "/api/modules/bdvm.traversal", AssetNamespace = "modules/bdvm.traversal" };
        public void Register(IBdvmWebRegistrar registrar) => registrar.AddRoute(new BdvmWebRoute { Method = "GET", Path = "/api/modules/bdvm.traversal/../secret" });
    }
}
