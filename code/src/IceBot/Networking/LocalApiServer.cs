using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using IceBot.Config;

namespace IceBot.Networking
{
    /// <summary>
    /// Local HTTP API for Cloudflare Tunnel ingress (BE pushes orders to this host).
    /// </summary>
    internal sealed class LocalApiServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Thread _thread = null!;

        public LocalApiServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(AppConfig.ApiListenPrefix);
        }

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _listener.Start();
            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "IceBot-LocalApi"
            };
            _thread.Start();
            Console.WriteLine($"[API] Listening on {AppConfig.ApiListenPrefix}");
        }

        private void ListenLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[API] Listener error: {ex.Message}");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

            try
            {
                if (!Authorize(request))
                {
                    WriteJson(response, 401, "{\"error\":\"unauthorized\"}");
                    return;
                }

                if (request.HttpMethod == "GET" && path == "/health")
                {
                    WriteJson(response, 200, "{\"status\":\"ok\",\"service\":\"IceBot\"}");
                    return;
                }

                if (request.HttpMethod == "POST" && path == "/api/orders")
                {
                    var body = ReadBody(request);
                    Console.WriteLine($"[API] Order received ({body.Length} bytes):");
                    Console.WriteLine(body);
                    // TODO: map order → workflow queue → run on robot
                    WriteJson(response, 202, "{\"status\":\"accepted\"}");
                    return;
                }

                if (request.HttpMethod == "POST" && path == "/api/provision")
                {
                    var body = ReadBody(request);
                    Console.WriteLine($"[API] Provision request ({body.Length} bytes):");
                    Console.WriteLine(body);
                    // TODO: call cloud BE with machine id, save .lua files to workflow/
                    WriteJson(response, 202, "{\"status\":\"accepted\"}");
                    return;
                }

                WriteJson(response, 404, "{\"error\":\"not_found\"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] Request error: {ex.Message}");
                WriteJson(response, 500, "{\"error\":\"internal_error\"}");
            }
        }

        private static bool Authorize(HttpListenerRequest request)
        {
            var expectedKey = AppConfig.ApiKey;
            if (string.IsNullOrEmpty(expectedKey))
            {
                return true;
            }

            var header = request.Headers["X-Api-Key"];
            return string.Equals(header, expectedKey, StringComparison.Ordinal);
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
        {
            var buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
        }
    }
}
