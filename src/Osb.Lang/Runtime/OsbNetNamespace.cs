using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 OSB.NET standard library implementation.
/// </summary>
public static class OsbNetNamespace
{
    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        return upper switch
        {
            "PING" => Ping(args, location),
            "DOWN" => Down(args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown OSB.NET method '{methodName}'."),
        };
    }

    private static OslangValue Ping(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "NET.PING() expects exactly 1 argument (host).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "NET.PING() expects a STRING argument for host.");
        }

        var host = sv.Value;
        var success = false;
        var time = new NumberValue(-1);

        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host);
            success = reply.Status == IPStatus.Success;
            time = new NumberValue(reply.RoundtripTime);
        }
        catch
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, 80);
                var completed = task.Wait(TimeSpan.FromSeconds(5));
                sw.Stop();
                if (completed)
                {
                    success = true;
                    time = new NumberValue(sw.Elapsed.TotalMilliseconds);
                }
            }
            catch
            {
                success = false;
            }
        }

        var result = new Dictionary<string, OslangValue>
        {
            ["success"] = BooleanValue.Of(success),
            ["host"] = new StringValue(host),
            ["time"] = time
        };
        return new JsonObjectValue(result);
    }

    private static OslangValue Down(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            throw new OslangRuntimeException(location, "NET.DOWN() expects 1 or 2 arguments (url, options?).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "NET.DOWN() expects a STRING argument for URL.");
        }

        var url = sv.Value;
        var timeoutMs = 30000.0;
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (args.Count >= 2)
        {
            if (args[1] is not JsonObjectValue options)
            {
                throw new OslangRuntimeException(location, "NET.DOWN() options must be a JSON object.");
            }
            if (options.Data.TryGetValue("timeout", out var timeoutVal) && timeoutVal is NumberValue tn)
            {
                timeoutMs = tn.Value;
            }
            if (options.Data.TryGetValue("headers", out var headersVal) && headersVal is JsonObjectValue hv)
            {
                foreach (var kvp in hv.Data)
                {
                    if (kvp.Value is StringValue svh)
                    {
                        requestHeaders[kvp.Key] = svh.Value;
                    }
                }
            }
        }

        try
        {
            using var client = new HttpClient();
            if (timeoutMs > 0)
            {
                client.Timeout = TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs));
            }

            foreach (var h in requestHeaders)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);
            }

            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var respHeaders = new Dictionary<string, OslangValue>();
            foreach (var h in response.Headers)
            {
                respHeaders[h.Key] = new StringValue(string.Join(", ", h.Value));
            }

            var result = new Dictionary<string, OslangValue>
            {
                ["status"] = new NumberValue((int)response.StatusCode),
                ["headers"] = new JsonObjectValue(respHeaders),
                ["body"] = new StringValue(body)
            };
            return new JsonObjectValue(result);
        }
        catch (Exception ex)
        {
            var result = new Dictionary<string, OslangValue>
            {
                ["status"] = new NumberValue(0),
                ["headers"] = new JsonObjectValue(new Dictionary<string, OslangValue>()),
                ["body"] = new StringValue($"Error: {ex.Message}")
            };
            return new JsonObjectValue(result);
        }
    }
}
