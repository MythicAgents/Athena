using Agent.Interfaces;
using Agent.Models;
using Agent.Profiles;
using Agent.Tests.TestClasses;
using Octokit;
using Octokit.Internal;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using GitHubProfile = Agent.Profiles.GitHub;
using WebsocketProfile = Agent.Profiles.Websocket.Websocket;
using WebsocketMessage = Agent.Profiles.Websocket.WebSocketMessage;

namespace ProfileReliability.Tests;

[TestClass]
public class ProfileSmokeTests
{
    private const string SuccessfulCheckin = "{\"action\":\"checkin\",\"status\":\"success\",\"id\":\"agent-1\"}";
    private const string PayloadUuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba";

    [TestMethod]
    [DoNotParallelize]
    public async Task GitHubProfileCompletesSuccessfulCheckinThroughApi()
    {
        var transport = new GitHubApiTransport(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(PayloadUuid + SuccessfulCheckin)));
        GitHubClient client = (GitHubClient)typeof(GitHubProfile)
            .GetField("client", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        FieldInfo httpClientField = client.Connection.GetType()
            .GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object originalTransport = httpClientField.GetValue(client.Connection)!;
        Credentials originalCredentials = client.Credentials;
        var crypto = new GitHubCheckinCrypto(transport.ResponseBody);
        Action restoreConfiguration = ConfigureGitHubProfile();

        try
        {
            httpClientField.SetValue(client.Connection, transport);
            var config = new TestAgentConfig { uuid = PayloadUuid };
            var profile = new GitHubProfile(config, crypto, new TestLogger(), new TestMessageManager());
            var checkin = new Checkin
            {
                action = "checkin", uuid = PayloadUuid, host = "test-host", user = "test-user",
                os = "linux", architecture = "x64", domain = "test", ips = new List<string> { "127.0.0.1" }
            };

            CheckinResponse response = await profile.Checkin(checkin);

            Assert.AreEqual("agent-1", response.id);
            Assert.AreEqual("success", response.status);
            StringAssert.Contains(crypto.EncryptedPlaintext!, "\"host\":\"test-host\"");
            CollectionAssert.AreEqual(
                new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete },
                transport.Requests.Select(request => request.Method).ToArray());
            StringAssert.EndsWith(transport.Requests[0].Endpoint, "repos/operator/repo/issues/2/comments");
            StringAssert.EndsWith(transport.Requests[1].Endpoint.Split('?')[0], "repos/operator/repo/issues/1/comments");
            StringAssert.EndsWith(transport.Requests[2].Endpoint, "repos/operator/repo/issues/comments/42");
            StringAssert.Contains(transport.Requests[0].Body, "encrypted-checkin");
        }
        finally
        {
            restoreConfiguration();
            httpClientField.SetValue(client.Connection, originalTransport);
            client.Credentials = originalCredentials;
        }
    }

    private static Action ConfigureGitHubProfile()
    {
        Type configType = typeof(GitHubProfile).Assembly.GetType("Agent.Profiles.ChannelConfig")!;
        FieldInfo dataField = configType.GetField("_d", BindingFlags.Static | BindingFlags.NonPublic)!;
        FieldInfo keyField = configType.GetField("_k", BindingFlags.Static | BindingFlags.NonPublic)!;
        byte[] encoded = (byte[])dataField.GetValue(null)!;
        byte[] original = encoded.ToArray();
        byte key = (byte)keyField.GetValue(null)!;
        string json = JsonSerializer.Serialize(new
        {
            personal_access_token = "fake",
            github_username = "operator",
            github_repo = "repo",
            server_issue_number = 1,
            client_issue_number = 2
        });
        byte[] plaintext = Encoding.UTF8.GetBytes(json.PadRight(encoded.Length));
        Assert.IsTrue(plaintext.Length <= encoded.Length, "Fake GitHub configuration must fit generated storage.");
        for (int i = 0; i < encoded.Length; i++)
        {
            encoded[i] = (byte)(plaintext[i] ^ key);
        }
        return () => Array.Copy(original, encoded, original.Length);
    }

    [TestMethod]
    public async Task HttpProfileCompletesSuccessfulCheckin()
    {
        var profile = new HttpProfile(new TestAgentConfig(), new IdentityCrypto(), new TestLogger(), new TestMessageManager());
        SetMember(profile, "_client", new HttpClient(new StaticResponseHandler(SuccessfulCheckin)));
        SetMember(profile, "getURL", "https://example.test/?q=");

        CheckinResponse response = await profile.Checkin(new Checkin { action = "checkin" });

        Assert.AreEqual("agent-1", response.id);
        Assert.AreEqual("success", response.status);
    }

    [TestMethod]
    public void DiscordProfileReceivesSuccessfulCheckin()
    {
        var profile = Uninitialized<DiscordProfile>();
        ManualResetEventSlim signal = ConfigureInbound(profile, "checkedin");
        SetMember(profile, "_uuid", "test-client");
        string envelope = Newtonsoft.Json.JsonConvert.SerializeObject(new MessageWrapper
        {
            to_server = false,
            client_id = "test-client",
            message = SuccessfulCheckin
        });

        InvokeInbound(profile, envelope);

        Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public void WebsocketProfileReceivesSuccessfulCheckin()
    {
        var profile = Uninitialized<WebsocketProfile>();
        ManualResetEventSlim signal = ConfigureInbound(profile, "checkedIn");
        string envelope = JsonSerializer.Serialize(new WebsocketMessage { client = false, data = SuccessfulCheckin });

        InvokeInbound(profile, envelope);

        Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public async Task SmbProfileReceivesSuccessfulCheckin()
    {
        var profile = Uninitialized<SmbProfile>();
        SetMember(profile, "crypt", new IdentityCrypto());
        var signal = new ManualResetEventSlim(false);
        SetMember(profile, "checkinAvailable", signal);
        MethodInfo complete = typeof(SmbProfile).GetMethod("OnMessageReceiveComplete", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)complete.Invoke(profile, new object[] { SuccessfulCheckin })!;

        Assert.IsTrue(signal.Wait(TimeSpan.FromSeconds(1)));
        Assert.AreEqual("agent-1", ((CheckinResponse)GetMember(profile, "cir")!).id);
    }

    [TestMethod]
    public async Task ZoomProfileReceivesSuccessfulCheckin()
    {
        var profile = new ZoomProfile(new TestAgentConfig(), new IdentityCrypto(), new TestLogger(), new TestMessageManager());
        string correlation = (string)GetMember(profile, "_correlationId")!;
        string envelope = JsonSerializer.Serialize(new { t = "I", c = correlation, j = "checkin", s = 0, n = 1, d = SuccessfulCheckin });
        SetMember(profile, "_client", new HttpClient(new ZoomInboundHandler(envelope)));
        SetMember(profile, "_token", "access-token");
        SetMember(profile, "_tokenExpiry", DateTime.UtcNow.AddHours(1));
        MethodInfo process = typeof(ZoomProfile).GetMethod("ProcessInbound", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!;

        await (Task)process.Invoke(profile, null)!;

        Assert.IsTrue((bool)GetMember(profile, "_checkedIn")!);
    }

    private static T Uninitialized<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static ManualResetEventSlim ConfigureInbound(object profile, string checkedInMember)
    {
        var signal = new ManualResetEventSlim(false);
        SetMember(profile, "crypt", new IdentityCrypto());
        SetMember(profile, "checkinAvailable", signal);
        SetMember(profile, checkedInMember, false);
        return signal;
    }

    private static void InvokeInbound(object profile, string content)
    {
        MethodInfo method = profile.GetType().GetMethod("HandleInboundMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(profile, new object[] { content });
    }

    private static object? GetMember(object target, string name)
    {
        Type type = target.GetType();
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    private static void SetMember(object target, string name, object value)
    {
        Type type = target.GetType();
        PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (property is not null)
        {
            property.SetValue(target, value);
            return;
        }
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(target, value);
    }

    private sealed class IdentityCrypto : ICryptoManager
    {
        public string Encrypt(string data) => data;
        public string Decrypt(string data) => data;
    }

    private sealed class GitHubCheckinCrypto(string responseBody) : ICryptoManager
    {
        public string? EncryptedPlaintext { get; private set; }

        public string Encrypt(string data)
        {
            EncryptedPlaintext = data;
            return "encrypted-checkin";
        }

        public string Decrypt(string data) => data == responseBody
            ? SuccessfulCheckin
            : throw new InvalidOperationException("Unexpected ciphertext");
    }

    private sealed class GitHubApiTransport(string responseBody) : IHttpClient
    {
        public string ResponseBody { get; } = responseBody;
        public List<CapturedGitHubRequest> Requests { get; } = new();

        public Task<IResponse> Send(IRequest request, CancellationToken cancellationToken, Func<object, object>? preprocessResponseBody)
        {
            Requests.Add(new CapturedGitHubRequest(request.Method, request.Endpoint.ToString(), request.Body?.ToString() ?? ""));
            object body = request.Method == HttpMethod.Get
                ? JsonSerializer.Serialize(new[] { new { id = 42, body = ResponseBody } })
                : JsonSerializer.Serialize(new { id = 41, body = "created" });
            HttpStatusCode status = request.Method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK;
            Type responseType = typeof(GitHubClient).Assembly.GetType("Octokit.Internal.Response")!;
            var response = (IResponse)Activator.CreateInstance(
                responseType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { status, body, new Dictionary<string, string>(), "application/json" },
                null)!;
            return Task.FromResult(response);
        }

        public void SetRequestTimeout(TimeSpan timeout) { }
        public void Dispose() { }
    }

    private sealed record CapturedGitHubRequest(HttpMethod Method, string Endpoint, string Body);

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private sealed class ZoomInboundHandler(string message) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Method == HttpMethod.Get
                ? JsonSerializer.Serialize(new { messages = new[] { new { id = "message-1", message } } })
                : "{}";
            return Task.FromResult(new HttpResponseMessage(
                request.Method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
