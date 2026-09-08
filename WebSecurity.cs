using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BDVM.Web;

public interface IWebClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemWebClock : IWebClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class WebSession
{
    public string SessionId { get; set; } = "";
    public string Principal { get; set; } = "";
    public string CsrfToken { get; set; } = "";
    public string OriginAuthority { get; set; } = "";
    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class WebSessionRequest
{
    public string SessionId { get; set; } = "";
    public string CsrfToken { get; set; } = "";
    public string OriginAuthority { get; set; } = "";
}

public sealed class WebSessionRegistry
{
    private const int MaximumIdentityLength = 96;
    private readonly Dictionary<string, WebSession> sessions = new Dictionary<string, WebSession>(StringComparer.Ordinal);
    private readonly IWebClock clock;
    private readonly TimeSpan lifetime;

    public WebSessionRegistry(IWebClock? clock = null, TimeSpan? lifetime = null)
    {
        this.clock = clock ?? new SystemWebClock();
        this.lifetime = lifetime ?? TimeSpan.FromMinutes(30);
    }

    public WebSession Create(string authenticatedPrincipal, string originAuthority, IEnumerable<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(authenticatedPrincipal) || authenticatedPrincipal.Length > MaximumIdentityLength)
            throw new UnauthorizedAccessException("A bounded authenticated transport identity is required.");
        if (!ValidAuthority(originAuthority)) throw new UnauthorizedAccessException("A valid same-origin authority is required.");
        var normalizedPermissions = (permissions ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var session = new WebSession
        {
            SessionId = RandomToken(),
            CsrfToken = RandomToken(),
            Principal = authenticatedPrincipal.Trim(),
            OriginAuthority = originAuthority.Trim().ToLowerInvariant(),
            Permissions = normalizedPermissions,
            ExpiresAt = clock.UtcNow.Add(lifetime)
        };
        sessions[session.SessionId] = session;
        return session;
    }

    public WebSession Validate(WebSessionRequest request, string permission)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SessionId) || !sessions.TryGetValue(request.SessionId, out var session))
            throw new UnauthorizedAccessException("Unknown web session.");
        if (session.ExpiresAt <= clock.UtcNow) { sessions.Remove(session.SessionId); throw new UnauthorizedAccessException("Web session expired."); }
        if (!FixedEquals(session.CsrfToken, request.CsrfToken)) throw new UnauthorizedAccessException("CSRF token mismatch.");
        if (!string.Equals(session.OriginAuthority, (request.OriginAuthority ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Cross-origin request refused.");
        if (!string.IsNullOrWhiteSpace(permission) && !session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Permission refused: " + permission + ".");
        return session;
    }

    public bool Revoke(string sessionId) => !string.IsNullOrEmpty(sessionId) && sessions.Remove(sessionId);

    private static bool ValidAuthority(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255 || value.Contains("/") || value.Contains("\\")) return false;
        return Uri.CheckHostName(value.Split(':')[0]) != UriHostNameType.Unknown || value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string RandomToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool FixedEquals(string expected, string actual)
    {
        var left = Encoding.UTF8.GetBytes(expected ?? "");
        var right = Encoding.UTF8.GetBytes(actual ?? "");
        var difference = left.Length ^ right.Length;
        var length = Math.Max(left.Length, right.Length);
        for (var i = 0; i < length; i++) difference |= (i < left.Length ? left[i] : 0) ^ (i < right.Length ? right[i] : 0);
        return difference == 0;
    }
}

public sealed class SlidingWindowRateLimiter
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> events = new Dictionary<string, Queue<DateTimeOffset>>(StringComparer.Ordinal);
    private readonly IWebClock clock;
    private readonly int maximumRequests;
    private readonly TimeSpan window;

    public SlidingWindowRateLimiter(int maximumRequests = 30, TimeSpan? window = null, IWebClock? clock = null)
    {
        if (maximumRequests < 1 || maximumRequests > 1000) throw new ArgumentOutOfRangeException(nameof(maximumRequests));
        this.maximumRequests = maximumRequests;
        this.window = window ?? TimeSpan.FromSeconds(10);
        this.clock = clock ?? new SystemWebClock();
    }

    public bool TryConsume(string key)
    {
        lock (events)
        {
            if (!events.TryGetValue(key, out var queue)) events[key] = queue = new Queue<DateTimeOffset>();
            var threshold = clock.UtcNow.Subtract(window);
            while (queue.Count != 0 && queue.Peek() <= threshold) queue.Dequeue();
            if (queue.Count >= maximumRequests) return false;
            queue.Enqueue(clock.UtcNow);
            return true;
        }
    }
}
