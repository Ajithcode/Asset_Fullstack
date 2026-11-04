namespace WorkerNetworkService.Settings;

public class HttpSettings
{
    /// <summary>Address the HTTP listener binds to. 0.0.0.0 accepts requests from other machines.</summary>
    public string ListenUrl { get; set; } = "http://0.0.0.0:5002";

    /// <summary>Redis stream each received request is added to, so the stream mappings store it in Mongo.</summary>
    public string TargetStream { get; set; } = "networkserver_stream";

    /// <summary>Stream entry field that holds the request payload; must match the mapping's PayloadField.</summary>
    public string PayloadField { get; set; } = "msg";

    public List<HttpEndpoint> Methods { get; set; } = [];
}

public class HttpEndpoint
{
    public string Name { get; set; } = "";

    /// <summary>POST/PUT: the request body is the payload. GET: the query string is the payload.</summary>
    public string Method { get; set; } = "POST";

    public string Url { get; set; } = "";
}
