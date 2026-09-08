using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using BDVM.Common;

namespace BDVM.Web;

public sealed class WebIntentEnvelope
{
    public int SchemaVersion { get; set; } = 1;
    public string ModuleId { get; set; } = "";
    public string IntentType { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public long ExpectedVersion { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public enum WebIntentState { Succeeded, Pending, Refused, Conflict, Reconcile }

public sealed class WebIntentResult
{
    public WebIntentState State { get; set; }
    public string Code { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
    public bool Replayed { get; set; }
}

public interface IAuthoritativeWebIntentExecutor
{
    WebIntentResult Execute(string authenticatedPrincipal, WebIntentEnvelope envelope);
}

public sealed class WebIntentGateway
{
    public const int MaximumPayloadBytes = 4096;
    private const int MaximumTokenLength = 128;
    private readonly WebModuleHost modules;
    private readonly WebSessionRegistry sessions;
    private readonly SlidingWindowRateLimiter limiter;
    private readonly IAuthoritativeWebIntentExecutor executor;
    private readonly Dictionary<string, ReplayEntry> completed = new Dictionary<string, ReplayEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, ExecutionSlot> executing = new Dictionary<string, ExecutionSlot>(StringComparer.Ordinal);

    public WebIntentGateway(WebModuleHost modules, WebSessionRegistry sessions, IAuthoritativeWebIntentExecutor executor, SlidingWindowRateLimiter? limiter = null)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.limiter = limiter ?? new SlidingWindowRateLimiter();
    }

    public WebIntentResult Submit(WebSessionRequest request, WebIntentEnvelope envelope)
    {
        if (envelope == null) return Refused("invalid-envelope", "");
        var route = modules.FindIntent(envelope.ModuleId, envelope.IntentType);
        if (route == null) return Refused("unknown-intent", envelope.CorrelationId);
        WebSession session;
        try { session = sessions.Validate(request, route.Permission); }
        catch (UnauthorizedAccessException) { return Refused("unauthorized", envelope.CorrelationId); }
        if (!limiter.TryConsume(session.SessionId)) return Refused("rate-limited", envelope.CorrelationId);
        var validation = Validate(envelope);
        if (validation != null) return validation;
        var replayKey = session.Principal + "|" + envelope.ModuleId + "|" + envelope.IdempotencyKey;
        var fingerprint = Fingerprint(envelope);
        ExecutionSlot slot;
        var ownsExecution = false;
        lock (completed)
        {
            if (completed.TryGetValue(replayKey, out var known))
            {
                if (!string.Equals(known.Fingerprint, fingerprint, StringComparison.Ordinal)) return Refused("idempotency-conflict", envelope.CorrelationId);
                return Copy(known.Result, true);
            }
            if (executing.TryGetValue(replayKey, out slot!))
            {
                if (!string.Equals(slot.Fingerprint, fingerprint, StringComparison.Ordinal)) return Refused("idempotency-conflict", envelope.CorrelationId);
            }
            else
            {
                slot = new ExecutionSlot(fingerprint);
                executing.Add(replayKey, slot);
                ownsExecution = true;
            }
        }
        if (!ownsExecution)
        {
            slot.Completed.Wait();
            return Copy(slot.Result ?? Refused("authoritative-executor-failed", envelope.CorrelationId), true);
        }
        WebIntentResult result;
        try { result = executor.Execute(session.Principal, envelope) ?? Refused("empty-authoritative-result", envelope.CorrelationId); }
        catch (Exception) { result = new WebIntentResult { State = WebIntentState.Reconcile, Code = "authoritative-executor-failed", CorrelationId = envelope.CorrelationId }; }
        lock (completed)
        {
            executing.Remove(replayKey);
            slot.Result = Copy(result, false);
            if (result.State != WebIntentState.Pending) completed[replayKey] = new ReplayEntry(fingerprint, Copy(result, false));
            slot.Completed.Set();
        }
        return result;
    }

    private static WebIntentResult? Validate(WebIntentEnvelope envelope)
    {
        if (envelope.SchemaVersion != 1) return Refused("unsupported-envelope-version", envelope.CorrelationId);
        if (!BoundedToken(envelope.ModuleId) || !BoundedToken(envelope.IntentType) || !BoundedToken(envelope.CorrelationId) || !BoundedToken(envelope.IdempotencyKey))
            return Refused("invalid-envelope", envelope.CorrelationId);
        if (envelope.ExpectedVersion < 0) return Refused("invalid-expected-version", envelope.CorrelationId);
        if (envelope.PayloadJson == null || Encoding.UTF8.GetByteCount(envelope.PayloadJson) > MaximumPayloadBytes)
            return Refused("payload-too-large", envelope.CorrelationId);
        try
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumPayloadBytes, RecursionLimit = 16 };
            if (!(serializer.DeserializeObject(envelope.PayloadJson) is IDictionary<string, object>)) return Refused("invalid-payload", envelope.CorrelationId);
        }
        catch (ArgumentException) { return Refused("invalid-payload", envelope.CorrelationId); }
        catch (InvalidOperationException) { return Refused("invalid-payload", envelope.CorrelationId); }
        return null;
    }

    private static bool BoundedToken(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumTokenLength && value.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == ':' || c == '-');
    private static string Fingerprint(WebIntentEnvelope envelope)
    {
        var canonical = envelope.SchemaVersion + "|" + envelope.ModuleId + "|" + envelope.IntentType + "|" + envelope.ExpectedVersion + "|" + envelope.PayloadJson;
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }
    private static WebIntentResult Refused(string code, string correlation) => new WebIntentResult { State = WebIntentState.Refused, Code = code, CorrelationId = correlation ?? "" };
    private static WebIntentResult Copy(WebIntentResult source, bool replayed) => new WebIntentResult { State = source.State, Code = source.Code, CorrelationId = source.CorrelationId, ResultJson = source.ResultJson, Replayed = replayed };
    private sealed class ReplayEntry { public ReplayEntry(string fingerprint, WebIntentResult result) { Fingerprint = fingerprint; Result = result; } public string Fingerprint { get; } public WebIntentResult Result { get; } }
    private sealed class ExecutionSlot
    {
        public ExecutionSlot(string fingerprint) { Fingerprint = fingerprint; }
        public string Fingerprint { get; }
        public System.Threading.ManualResetEventSlim Completed { get; } = new System.Threading.ManualResetEventSlim(false);
        public WebIntentResult? Result { get; set; }
    }
}
