//using Athena.Commands.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Amazon.S3;
using Amazon;

namespace Plugin
{
    public static class s3
    {
        public static void Execute(Dictionary<string, object> args)
        {
            try
            {
                var credential = new AwsCredential("key", "secret");
                var client = new S3Client(AwsRegion.USEast1, credential);


            }
            catch (Exception e)
            {

                
            }
            return;
        }
    }
}
