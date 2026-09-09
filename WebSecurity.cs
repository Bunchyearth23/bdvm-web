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
    public string DisplayName { get; set; } = "";
    public string CsrfToken { get; set; } = "";
    public string OriginAuthority { get; set; } = "";
    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class WebIdentity
{
    public string PrincipalId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string MultiplayerDisplayName { get; set; } = "";
}

public sealed class WebLoginRequest
{
    public string AccountName { get; set; } = "";
    public string Password { get; set; } = "";
    public string OriginAuthority { get; set; } = "";
    public string RemoteAddress { get; set; } = "";
}

public sealed class WebCredentialRecord
{
    public string AccountName { get; set; } = "";
    public string PrincipalId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int Iterations { get; set; }
    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
}

public interface IWebCredentialStore
{
    WebCredentialRecord? Find(string normalizedAccountName);
    void Save(WebCredentialRecord record);
}

public sealed class InMemoryWebCredentialStore : IWebCredentialStore
{
    private readonly Dictionary<string, WebCredentialRecord> records = new Dictionary<string, WebCredentialRecord>(StringComparer.OrdinalIgnoreCase);
    public WebCredentialRecord? Find(string normalizedAccountName) { lock (records) return records.TryGetValue(normalizedAccountName, out var value) ? value : null; }
    public void Save(WebCredentialRecord record) { if (record == null) throw new ArgumentNullException(nameof(record)); lock (records) records[record.AccountName] = record; }
}

public sealed class WebAuthenticationService
{
    public const int PasswordIterations = 120000;
    private readonly IWebCredentialStore credentials;
    private readonly WebSessionRegistry sessions;
    private readonly SlidingWindowRateLimiter loginLimiter;

    public WebAuthenticationService(IWebCredentialStore credentials, WebSessionRegistry sessions, SlidingWindowRateLimiter? loginLimiter = null)
    {
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.loginLimiter = loginLimiter ?? new SlidingWindowRateLimiter(5, TimeSpan.FromMinutes(1));
    }

    public WebCredentialRecord Register(string accountName, string password, WebIdentity identity, IEnumerable<string> permissions)
    {
        var account = NormalizeAccount(accountName);
        ValidatePassword(password);
        if (identity == null || !Bounded(identity.PrincipalId, 96)) throw new ArgumentException("A stable technical principal is required.", nameof(identity));
        if (credentials.Find(account) != null) throw new InvalidOperationException("Account already exists.");
        var salt = RandomBytes(32);
        var record = new WebCredentialRecord
        {
            AccountName = account,
            PrincipalId = identity.PrincipalId.Trim(),
            DisplayName = SelectDisplayName(identity, account),
            Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(Derive(password, salt, PasswordIterations)),
            Iterations = PasswordIterations,
            Permissions = (permissions ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
        credentials.Save(record);
        return record;
    }

    public WebSession Login(WebLoginRequest request)
    {
        if (request == null) throw new UnauthorizedAccessException("Invalid credentials.");
        var limiterKey = (request.RemoteAddress ?? "unknown").Trim().ToLowerInvariant();
        if (!loginLimiter.TryConsume(limiterKey)) throw new UnauthorizedAccessException("Login rate limit exceeded.");
        string account;
        try { account = NormalizeAccount(request.AccountName); } catch (ArgumentException) { throw new UnauthorizedAccessException("Invalid credentials."); }
        var record = credentials.Find(account);
        if (record == null || !Verify(request.Password, record)) throw new UnauthorizedAccessException("Invalid credentials.");
        return sessions.Create(record.PrincipalId, request.OriginAuthority, record.Permissions, record.DisplayName);
    }

    public static string SelectDisplayName(WebIdentity identity, string fallback)
    {
        var proposed = string.IsNullOrWhiteSpace(identity.MultiplayerDisplayName) ? identity.DisplayName : identity.MultiplayerDisplayName;
        proposed = string.IsNullOrWhiteSpace(proposed) ? fallback : proposed.Trim();
        if (!Bounded(proposed, 48) || proposed.Any(char.IsControl)) throw new ArgumentException("Display name must contain 1 to 48 safe characters.", nameof(identity));
        return proposed;
    }

    private static bool Verify(string password, WebCredentialRecord record)
    {
        try
        {
            var salt = Convert.FromBase64String(record.Salt);
            var expected = Convert.FromBase64String(record.PasswordHash);
            return FixedEquals(expected, Derive(password ?? "", salt, record.Iterations));
        }
        catch (FormatException) { return false; }
    }
    private static byte[] Derive(string password, byte[] salt, int iterations) { using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)) return kdf.GetBytes(32); }
    private static byte[] RandomBytes(int length) { var value = new byte[length]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(value); return value; }
    private static string NormalizeAccount(string value) { if (!Bounded(value, 64) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-'))) throw new ArgumentException("Invalid account name."); return value.Trim().ToLowerInvariant(); }
    private static void ValidatePassword(string value) { if (value == null || value.Length < 10 || value.Length > 256) throw new ArgumentException("Password must contain 10 to 256 characters."); }
    private static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;
    private static bool FixedEquals(byte[] left, byte[] right) { var difference = left.Length ^ right.Length; for (var i = 0; i < Math.Max(left.Length, right.Length); i++) difference |= (i < left.Length ? left[i] : 0) ^ (i < right.Length ? right[i] : 0); return difference == 0; }
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

    public WebSession Create(string authenticatedPrincipal, string originAuthority, IEnumerable<string> permissions, string? displayName = null)
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
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? authenticatedPrincipal.Trim() : displayName!.Trim(),
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
