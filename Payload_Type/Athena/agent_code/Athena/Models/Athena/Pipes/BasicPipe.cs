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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Athena.Pipes
{
	public abstract class BasicPipe
	{
		public event EventHandler<PipeEventArgs> DataReceived;
		public event EventHandler<EventArgs> PipeClosed;

		protected PipeStream pipeStream;
		protected Action<BasicPipe> asyncReaderStart;

		public BasicPipe()
		{
		}

		public void Close()
		{
			pipeStream.WaitForPipeDrain();
			pipeStream.Close();
			pipeStream.Dispose();
			pipeStream = null;
		}

		/// <summary>
		/// Reads an array of bytes, where the first [n] bytes (based on the server's intsize) indicates the number of bytes to read 
		/// to complete the packet.
		/// </summary>
		public void StartByteReaderAsync()
		{
			StartByteReaderAsync((b) => DataReceived?.Invoke(this, new PipeEventArgs(b, b.Length)));
		}

		/// <summary>
		/// Reads an array of bytes, where the first [n] bytes (based on the server's intsize) indicates the number of bytes to read 
		/// to complete the packet, and invokes the DataReceived event with a string converted from UTF8 of the byte array.
		/// </summary>
		public void StartStringReaderAsync()
		{
			StartByteReaderAsync((b) =>
			{
				string str = Encoding.UTF8.GetString(b).TrimEnd('\0');
				DataReceived?.Invoke(this, new PipeEventArgs(str));
			});
		}

		public void Flush()
		{
			pipeStream.Flush();
		}

		public Task WriteString(string str)
		{
			return WriteBytes(Encoding.UTF8.GetBytes(str));
		}

		public Task WriteBytes(byte[] bytes)
		{
			var blength = BitConverter.GetBytes(bytes.Length);
			var bfull = blength.Concat(bytes).ToArray();
			return pipeStream.WriteAsync(bfull, 0, bfull.Length);
		}

		protected void StartByteReaderAsync(Action<byte[]> packetReceived)
		{
			int intSize = sizeof(int);
			byte[] bDataLength = new byte[intSize];

			pipeStream.ReadAsync(bDataLength, 0, intSize).ContinueWith(t =>
			{
				int len = t.Result;

				if (len == 0)
				{
					PipeClosed?.Invoke(this, EventArgs.Empty);
				}
				else
				{
					int dataLength = BitConverter.ToInt32(bDataLength, 0);
					byte[] data = new byte[dataLength];

					pipeStream.ReadAsync(data, 0, dataLength).ContinueWith(t2 =>
					{
						len = t2.Result;

						if (len == 0)
						{
							PipeClosed?.Invoke(this, EventArgs.Empty);
						}
						else
						{
							packetReceived(data);
							StartByteReaderAsync(packetReceived);
						}
					});
				}
			});
		}
	}
}