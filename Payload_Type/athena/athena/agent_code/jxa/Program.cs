﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jxa
{
    internal class Program
    {
        public void runjxa()
        {
            string jscode = @"return {""user_output"": JSON.stringify(ObjC.deepUnwrap($.NSHost.currentHost.addresses), null, 2), ""completed"": true};";
        }
    }
}
