/* The MIT License (MIT)
* 
* Copyright (c) 2015 Marc Clifton
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

// From: http://stackoverflow.com/questions/34478513/c-sharp-full-duplex-asynchronous-named-pipes-net
// See Eric Frazer's Q and self answer

using System;
using System.IO.Pipes;

namespace Athena.Models.Athena.Pipes
{
    public class ClientPipe : BasicPipe
    {
        protected NamedPipeClientStream clientPipeStream;

        public ClientPipe(string serverName, string pipeName, Action<BasicPipe> asyncReaderStart)
        {
            this.asyncReaderStart = asyncReaderStart;
            clientPipeStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipeStream = clientPipeStream;
        }

        public void Connect()
        {
            clientPipeStream.Connect();
            asyncReaderStart(this);
        }
    }
}
