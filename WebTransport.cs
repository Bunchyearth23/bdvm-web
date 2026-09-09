using System;

namespace BDVM.Web;

public sealed class WebTransportRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string BodyJson { get; set; } = "";
    public WebSessionRequest Session { get; set; } = new WebSessionRequest();
}

public sealed class WebTransportResponse
{
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string Body { get; set; } = "";
}

public interface IBdvmWebTransport
{
    void Start(Func<WebTransportRequest, WebTransportResponse> handler);
    void Stop();
}

// The transport owns HTTP/WebSocket mechanics only. It must never execute domain rules.
public interface IWebSnapshotProvider
{
    WebTransportResponse Read(string authenticatedPrincipal, string path);
}
