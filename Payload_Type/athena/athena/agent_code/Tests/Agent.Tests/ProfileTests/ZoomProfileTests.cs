using Agent.Profiles;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Agent.Tests.ProfileTests
{
    [TestClass]
    public class ZoomProfileTests
    {
        private sealed record CapturedRequest(HttpMethod Method, string Url, string? Scheme, string? Parameter, string Body);

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public List<CapturedRequest> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(new CapturedRequest(
                    request.Method,
                    request.RequestUri!.ToString(),
                    request.Headers.Authorization?.Scheme,
                    request.Headers.Authorization?.Parameter,
                    request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
                return Requests.Count == 1
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"access-token\",\"expires_in\":3600}") }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"message-id\"}") };
            }
        }

        [TestMethod]
        public void ZoomProfileLoadsEveryConfiguredValue()
        {
            var profile = new ZoomProfile(null!, null!, null!, null!);
            Assert.AreEqual("", GetField(profile, "accountId"));
            Assert.AreEqual("", GetField(profile, "clientId"));
            Assert.AreEqual("", GetField(profile, "clientSecret"));
            Assert.AreEqual("me", GetField(profile, "userId"));
            Assert.AreEqual("", GetField(profile, "channelId"));
            Assert.AreEqual("https://api.zoom.us/v2", GetField(profile, "apiBase"));
            Assert.AreEqual("https://zoom.us/oauth", GetField(profile, "oauthBase"));
        }

        [TestMethod]
        public async Task ZoomRequestsUseConfiguredOAuthAndChatValues()
        {
            var profile = new ZoomProfile(null!, null!, null!, null!);
            SetField(profile, "accountId", "account");
            SetField(profile, "clientId", "client");
            SetField(profile, "clientSecret", "secret");
            SetField(profile, "userId", "user");
            SetField(profile, "channelId", "channel");
            SetField(profile, "apiBase", "https://api.test");
            SetField(profile, "oauthBase", "https://oauth.test");
            var handler = new RecordingHandler();
            typeof(ZoomProfile).GetProperty("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(profile, new HttpClient(handler));

            var send = typeof(ZoomProfile).GetMethod("SendChatMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var messageId = await (Task<string?>)send.Invoke(profile, new object[] { "hello" })!;

            Assert.AreEqual("message-id", messageId);
            Assert.AreEqual(2, handler.Requests.Count);
            var oauth = handler.Requests[0];
            Assert.AreEqual(HttpMethod.Post, oauth.Method);
            Assert.AreEqual("https://oauth.test/token", oauth.Url);
            Assert.AreEqual("Basic", oauth.Scheme);
            Assert.AreEqual(Convert.ToBase64String(Encoding.ASCII.GetBytes("client:secret")), oauth.Parameter);
            StringAssert.Contains(oauth.Body, "grant_type=account_credentials");
            StringAssert.Contains(oauth.Body, "account_id=account");

            var chat = handler.Requests[1];
            Assert.AreEqual(HttpMethod.Post, chat.Method);
            Assert.AreEqual("https://api.test/chat/users/user/messages", chat.Url);
            Assert.AreEqual("Bearer", chat.Scheme);
            Assert.AreEqual("access-token", chat.Parameter);
            using var payload = JsonDocument.Parse(chat.Body);
            Assert.AreEqual("hello", payload.RootElement.GetProperty("message").GetString());
            Assert.AreEqual("channel", payload.RootElement.GetProperty("to_channel").GetString());
        }

        private static string GetField(ZoomProfile profile, string name)
        {
            return (string)typeof(ZoomProfile).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(profile)!;
        }

        private static void SetField(ZoomProfile profile, string name, string value)
        {
            typeof(ZoomProfile).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(profile, value);
        }
    }
}
