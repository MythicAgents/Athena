// Copyright (c) 2018-2020, Yves Goergen, https://unclassified.software
//
// Copying and distribution of this file, with or without modification, are permitted provided the
// copyright notice and this notice are preserved. This file is offered as-is, without any warranty.


using Agent.Models;
using System.Net;
using System.Net.Sockets;

namespace Agent
{
    #region License
    //https://github.com/ygoe/AsyncTcpClient
    /*
     * Permissive license

    Copyright (c) 2018, Yves Goergen, https://unclassified.software

    Copying and distribution of this file, with or without modification, are permitted provided the copyright notice and this notice are preserved. This file is offered as-is, without any warranty.
    */
    #endregion
    public class AsyncTcpClient : IDisposable
    {
        #region Private data

        private TcpClient tcpClient;
        private NetworkStream stream;
        private TaskCompletionSource<bool> closedTcs = new TaskCompletionSource<bool>();

        #endregion Private data

        #region Constructors

        /// <summary>
        /// Initialises a new instance of the <see cref="AsyncTcpClient"/> class.
        /// </summary>
        public AsyncTcpClient()
        {
            closedTcs.SetResult(true);
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="AsyncTcpClient"/> class.
        /// </summary>
        public AsyncTcpClient(ConnectionOptions co)
        {
            switch ((AddressType)co.addressType)
            {
                case AddressType.IPv4:
                    this.IPAddress = co.ip;
                    break;
                case AddressType.IPv6:
                    this.IPAddress = co.ip;
                    break;
                case AddressType.DomainName:
                    this.HostName = co.host;
                    break;
                default:
                    break;
            }

            this.Port = co.port;

            closedTcs.SetResult(true);
        }

        #endregion Constructors

        #region Events

        /// <summary>
        /// Occurs when a trace message is available.
        /// </summary>
        public event EventHandler<AsyncTcpEventArgs> Message;

        #endregion Events

        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="TcpClient"/> to use. Only for client connections that were
        /// accepted by an <see cref="AsyncTcpListener"/>.
        /// </summary>
        public TcpClient ServerTcpClient { get; set; }

        /// <summary>
        /// Gets or sets the remote endpoint of the socket. Only for client connections that were
        /// accepted by an <see cref="AsyncTcpListener"/>.
        /// </summary>
        public EndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the amount of time an <see cref="AsyncTcpClient"/> will wait to connect
        /// once a connection operation is initiated.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the maximum amount of time an <see cref="AsyncTcpClient"/> will wait to
        /// connect once a repeated connection operation is initiated. The actual connection
        /// timeout is increased with every try and reset when a connection is established.
        /// </summary>
        public TimeSpan MaxConnectTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets a value indicating whether the client should try to reconnect after the
        /// connection was closed.
        /// </summary>
        public bool AutoReconnect { get; set; }

        /// <summary>
        /// Gets or sets the name of the host to connect to.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Gets or sets the IP address of the host to connect to.
        /// Only regarded if <see cref="HostName"/> is null or empty.
        /// </summary>
        public IPAddress IPAddress { get; set; }

        /// <summary>
        /// Gets or sets the port number of the remote host.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets a value indicating whether the client is currently connected.
        /// </summary>
        public bool IsConnected => tcpClient.Client is null ? false : tcpClient.Client.Connected;

        /// <summary>
        /// Gets the buffer of data that was received from the remote host.
        /// </summary>
        public ByteBuffer ByteBuffer { get; private set; } = new ByteBuffer();

        /// <summary>
        /// A <see cref="Task"/> that can be awaited to close the connection. This task will
        /// complete when the connection was closed remotely.
        /// </summary>
        public Task ClosedTask => closedTcs.Task;

        /// <summary>
        /// Gets a value indicating whether the <see cref="ClosedTask"/> has completed.
        /// </summary>
        public bool IsClosing => ClosedTask.IsCompleted;

        /// <summary>
        /// Called when the client has connected to the remote host. This method can implement the
        /// communication logic to execute when the connection was established. The connection will
        /// not be closed before this method completes.
        /// </summary>
        /// <remarks>
        /// This callback method may not be called when the <see cref="OnConnectedAsync"/> method
        /// is overridden by a derived class.
        /// </remarks>
        public Func<AsyncTcpClient, bool, Task> ConnectedCallback { get; set; }

        /// <summary>
        /// Called when the connection was closed. The parameter specifies whether the connection
        /// was closed by the remote host.
        /// </summary>
        /// <remarks>
        /// This callback method may not be called when the <see cref="OnClosed"/> method is
        /// overridden by a derived class.
        /// </remarks>
        public Action<AsyncTcpClient, bool> ClosedCallback { get; set; }

        /// <summary>
        /// Called when data was received from the remote host. The parameter specifies the number
        /// of bytes that were received. This method can implement the communication logic to
        /// execute every time data was received. New data will not be received before this method
        /// completes.
        /// </summary>
        /// <remarks>
        /// This callback method may not be called when the <see cref="OnReceivedAsync"/> method
        /// is overridden by a derived class.
        /// </remarks>
        public Func<AsyncTcpClient, int, Task> ReceivedCallback { get; set; }


        public int ConnectionId { get; set; }

        #endregion Properties

        #region Public methods

        /// <summary>
        /// Runs the client connection asynchronously.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task RunAsync()
        {
            bool isReconnected = false;
            int reconnectTry = -1;
            do
            {
                reconnectTry++;
                ByteBuffer = new ByteBuffer();
                if (ServerTcpClient != null)
                {
                    // Take accepted connection from listener
                    tcpClient = ServerTcpClient;
                }
                else
                {
                    // Try to connect to remote host
                    var connectTimeout = TimeSpan.FromTicks(ConnectTimeout.Ticks + (MaxConnectTimeout.Ticks - ConnectTimeout.Ticks) / 20 * Math.Min(reconnectTry, 20));
                    tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
                    tcpClient.Client.DualMode = true;
                    Message?.Invoke(this, new AsyncTcpEventArgs("Connecting to server"));
                    Task connectTask;
                    if (!string.IsNullOrWhiteSpace(HostName))
                    {
                        connectTask = tcpClient.ConnectAsync(HostName, Port);
                    }
                    else
                    {
                        connectTask = tcpClient.ConnectAsync(IPAddress, Port);
                    }
                    var timeoutTask = Task.Delay(connectTimeout);
                    if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                    {
                        Message?.Invoke(this, new AsyncTcpEventArgs("Connection timeout"));
                        continue;
                    }
                    try
                    {
                        await connectTask;
                    }
                    catch (Exception ex)
                    {
                        Message?.Invoke(this, new AsyncTcpEventArgs("Error connecting to remote host", ex));
                        await timeoutTask;
                        continue;
                    }
                }
                reconnectTry = -1;
                stream = tcpClient.GetStream();

                // Read until the connection is closed.
                // A closed connection can only be detected while reading, so we need to read
                // permanently, not only when we might use received data.
                var networkReadTask = Task.Run(async () =>
                {
                    // 10 KiB should be enough for every Ethernet packet
                    byte[] buffer = new byte[10240];
                    while (true)
                    {
                        int readLength = -1;
                        try
                        {
                            if (stream != null)
                            {
                                readLength = await stream.ReadAsync(buffer, 0, buffer.Length);
                            }
                        }
                        catch (IOException ex) when ((ex.InnerException as SocketException)?.ErrorCode == (int)SocketError.OperationAborted ||
                            (ex.InnerException as SocketException)?.ErrorCode == 125 /* Operation canceled (Linux) */)
                        {
                            // Warning: This error code number (995) may change.
                            // See https://docs.microsoft.com/en-us/windows/desktop/winsock/windows-sockets-error-codes-2
                            // Note: NativeErrorCode and ErrorCode 125 observed on Linux.
                            Message?.Invoke(this, new AsyncTcpEventArgs("Connection closed locally", ex));
                            readLength = -1;
                        }
                        catch (IOException ex) when ((ex.InnerException as SocketException)?.ErrorCode == (int)SocketError.ConnectionAborted)
                        {
                            Message?.Invoke(this, new AsyncTcpEventArgs("Connection aborted", ex));
                            readLength = -1;
                        }
                        catch (IOException ex) when ((ex.InnerException as SocketException)?.ErrorCode == (int)SocketError.ConnectionReset)
                        {
                            Message?.Invoke(this, new AsyncTcpEventArgs("Connection reset remotely", ex));
                            readLength = -2;
                        }
                        if (readLength <= 0)
                        {
                            if (readLength == 0)
                            {
                                Message?.Invoke(this, new AsyncTcpEventArgs("Connection closed remotely"));
                            }
                            closedTcs.TrySetResult(true);
                            OnClosed(readLength != -1);
                            return;
                        }
                        var segment = new ArraySegment<byte>(buffer, 0, readLength);
                        ByteBuffer.Enqueue(segment);
                        await OnReceivedAsync(readLength);
                    }
                });

                closedTcs = new TaskCompletionSource<bool>();
                await OnConnectedAsync(isReconnected);

                // Wait for closed connection
                await networkReadTask;
                tcpClient.Close();

                isReconnected = true;
            }
            while (AutoReconnect && ServerTcpClient == null);
        }

        /// <summary>
        /// Closes the socket connection normally. This does not release the resources used by the
        /// <see cref="AsyncTcpClient"/>.
        /// </summary>
        public void Disconnect()
        {
            tcpClient.Client.Disconnect(false);
        }

        /// <summary>
        /// Releases the managed and unmanaged resources used by the <see cref="AsyncTcpClient"/>.
        /// Closes the connection to the remote host and disables automatic reconnecting.
        /// </summary>
        public void Dispose()
        {
            AutoReconnect = false;
            tcpClient?.Dispose();
            stream = null;
        }

        /// <summary>
        /// Waits asynchronously until received data is available in the buffer.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token used to propagate notification that this operation should be canceled.</param>
        /// <returns>true, if data is available; false, if the connection is closing.</returns>
        /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was canceled.</exception>
        public async Task<bool> WaitAsync(CancellationToken cancellationToken = default)
        {
            return await Task.WhenAny(ByteBuffer.WaitAsync(cancellationToken), closedTcs.Task) != closedTcs.Task;
        }

        /// <summary>
        /// Sends data to the remote host.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">A cancellation token used to propagate notification that this operation should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task Send(ArraySegment<byte> data, CancellationToken cancellationToken = default)
        {
            if (tcpClient.Client is null || !tcpClient.Client.Connected)
                throw new InvalidOperationException("Not connected.");

            await stream.WriteAsync(data.Array, data.Offset, data.Count, cancellationToken);
        }

        #endregion Public methods

        #region Protected virtual methods

        /// <summary>
        /// Called when the client has connected to the remote host. This method can implement the
        /// communication logic to execute when the connection was established. The connection will
        /// not be closed before this method completes.
        /// </summary>
        /// <param name="isReconnected">true, if the connection was closed and automatically reopened;
        ///   false, if this is the first established connection for this client instance.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected virtual Task OnConnectedAsync(bool isReconnected)
        {
            if (ConnectedCallback != null && this != null)
            {
                return ConnectedCallback(this, isReconnected);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the connection was closed.
        /// </summary>
        /// <param name="remote">true, if the connection was closed by the remote host; false, if
        ///   the connection was closed locally.</param>
        protected virtual void OnClosed(bool remote)
        {
            if (this != null)
            {
                ClosedCallback?.Invoke(this, remote);
            }

        }

        /// <summary>
        /// Called when data was received from the remote host. This method can implement the
        /// communication logic to execute every time data was received. New data will not be
        /// received before this method completes.
        /// </summary>
        /// <param name="count">The number of bytes that were received. The actual data is available
        ///   through the <see cref="ByteBuffer"/>.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected virtual Task OnReceivedAsync(int count)
        {
            if (ReceivedCallback != null && this != null)
            {
                return ReceivedCallback(this, count);
            }
            return Task.CompletedTask;
        }

        #endregion Protected virtual methods
    }

    /// <summary>
    /// Provides data for the <see cref="AsyncTcpClient.Message"/> event.
    /// </summary>
    public class AsyncTcpEventArgs : EventArgs
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="AsyncTcpEventArgs"/> class.
        /// </summary>
        /// <param name="message">The trace message.</param>
        /// <param name="exception">The exception that was thrown, if any.</param>
        public AsyncTcpEventArgs(string message, Exception exception = null)
        {
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// Gets the trace message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exception that was thrown, if any.
        /// </summary>
        public Exception Exception { get; }
    }
    public class ByteBuffer
    {
        #region Private data

        private const int DefaultCapacity = 1024;

        private readonly object syncObj = new object();

        /// <summary>
        /// The internal buffer.
        /// </summary>
        private byte[] buffer = new byte[DefaultCapacity];

        /// <summary>
        /// The buffer index of the first byte to dequeue.
        /// </summary>
        private int head;

        /// <summary>
        /// The buffer index of the last byte to dequeue.
        /// </summary>
        private int tail = -1;

        /// <summary>
        /// Indicates whether the buffer is empty. The empty state cannot be distinguished from the
        /// full state with <see cref="head"/> and <see cref="tail"/> alone.
        /// </summary>
        private bool isEmpty = true;

        /// <summary>
        /// Used to signal the waiting <see cref="DequeueAsync(int, CancellationToken)"/> method.
        /// Set when new data becomes available. Only reset there.
        /// </summary>
        private TaskCompletionSource<bool> dequeueManualTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Used to signal the waiting <see cref="WaitAsync"/> method.
        /// Set when new data becomes availalble. Reset when the queue is empty.
        /// </summary>
        private TaskCompletionSource<bool> availableTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion Private data

        #region Constructors

        /// <summary>
        /// Initialises a new instance of the <see cref="ByteBuffer"/> class that is empty and has
        /// the default initial capacity.
        /// </summary>
        public ByteBuffer()
        {
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="ByteBuffer"/> class that contains bytes
        /// copied from the specified collection and has sufficient capacity to accommodate the
        /// number of bytes copied.
        /// </summary>
        /// <param name="bytes">The collection whose bytes are copied to the new <see cref="ByteBuffer"/>.</param>
        public ByteBuffer(byte[] bytes)
        {
            Enqueue(bytes);
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="ByteBuffer"/> class that is empty and has
        /// the specified initial capacity.
        /// </summary>
        /// <param name="capacity">The initial number of bytes that the <see cref="ByteBuffer"/> can contain.</param>
        public ByteBuffer(int capacity)
        {
            AutoTrimMinCapacity = capacity;
            SetCapacity(capacity);
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// Gets the number of bytes contained in the buffer.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncObj)
                {
                    if (isEmpty)
                    {
                        return 0;
                    }
                    if (tail >= head)
                    {
                        return tail - head + 1;
                    }
                    return Capacity - head + tail + 1;
                }
            }
        }

        /// <summary>
        /// Gets the current buffer contents.
        /// </summary>
        public byte[] Buffer
        {
            get
            {
                lock (syncObj)
                {
                    byte[] bytes = new byte[Count];
                    if (!isEmpty)
                    {
                        if (tail >= head)
                        {
                            Array.Copy(buffer, head, bytes, 0, tail - head + 1);
                        }
                        else
                        {
                            Array.Copy(buffer, head, bytes, 0, Capacity - head);
                            Array.Copy(buffer, 0, bytes, Capacity - head, tail + 1);
                        }
                    }
                    return bytes;
                }
            }
        }

        /// <summary>
        /// Gets the capacity of the buffer.
        /// </summary>
        public int Capacity => buffer.Length;

        /// <summary>
        /// Gets or sets a value indicating whether the buffer is automatically trimmed on dequeue
        /// if the <see cref="Count"/> becomes significantly smaller than the <see cref="Capacity"/>.
        /// Default is true.
        /// </summary>
        /// <remarks>
        /// This property is not thread-safe and should only be set if no other operation is ongoing.
        /// </remarks>
        public bool AutoTrim { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum capacity to maintain when automatically trimming on dequeue.
        /// See <see cref="AutoTrim"/>. Default is the initial capacity as set in the constructor.
        /// </summary>
        /// <remarks>
        /// This property is not thread-safe and must only be set if no other operation is ongoing.
        /// </remarks>
        public int AutoTrimMinCapacity { get; set; } = DefaultCapacity;

        #endregion Properties

        #region Public methods

        /// <summary>
        /// Removes all bytes from the buffer.
        /// </summary>
        public void Clear()
        {
            lock (syncObj)
            {
                head = 0;
                tail = -1;
                isEmpty = true;
                Reset(ref availableTcs);
            }
        }

        /// <summary>
        /// Sets the buffer capacity. Existing bytes are kept in the buffer.
        /// </summary>
        /// <param name="capacity">The new buffer capacity.</param>
        public void SetCapacity(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must not be negative.");

            lock (syncObj)
            {
                int count = Count;
                if (capacity < count)
                    throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity is too small to hold the current buffer content.");

                if (capacity != buffer.Length)
                {
                    byte[] newBuffer = new byte[capacity];
                    Array.Copy(Buffer, newBuffer, count);
                    buffer = newBuffer;
                    head = 0;
                    tail = count - 1;
                }
            }
        }

        /// <summary>
        /// Sets the capacity to the actual number of bytes in the buffer, if that number is less
        /// than 90 percent of current capacity.
        /// </summary>
        public void TrimExcess()
        {
            lock (syncObj)
            {
                if (Count < Capacity * 0.9)
                {
                    SetCapacity(Count);
                }
            }
        }

        /// <summary>
        /// Adds bytes to the end of the buffer.
        /// </summary>
        /// <param name="bytes">The bytes to add to the buffer.</param>
        public void Enqueue(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            Enqueue(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Adds bytes to the end of the buffer.
        /// </summary>
        /// <param name="segment">The bytes to add to the buffer.</param>
        public void Enqueue(ArraySegment<byte> segment)
        {
            Enqueue(segment.Array, segment.Offset, segment.Count);
        }

        /// <summary>
        /// Adds bytes to the end of the buffer.
        /// </summary>
        /// <param name="bytes">The bytes to add to the buffer.</param>
        /// <param name="offset">The index in <paramref name="bytes"/> of the first byte to add.</param>
        /// <param name="count">The number of bytes to add.</param>
        public void Enqueue(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset + count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return;   // Nothing to do

            lock (syncObj)
            {
                if (Count + count > Capacity)
                {
                    SetCapacity(Math.Max(Capacity * 2, Count + count));
                }

                int tailCount;
                int wrapCount;
                if (tail >= head || isEmpty)
                {
                    tailCount = Math.Min(Capacity - 1 - tail, count);
                    wrapCount = count - tailCount;
                }
                else
                {
                    tailCount = Math.Min(head - 1 - tail, count);
                    wrapCount = 0;
                }

                if (tailCount > 0)
                {
                    Array.Copy(bytes, offset, buffer, tail + 1, tailCount);
                }
                if (wrapCount > 0)
                {
                    Array.Copy(bytes, offset + tailCount, buffer, 0, wrapCount);
                }
                tail = (tail + count) % Capacity;
                isEmpty = false;
                Set(dequeueManualTcs);
                Set(availableTcs);
            }
        }

        /// <summary>
        /// Removes and returns bytes at the beginning of the buffer.
        /// </summary>
        /// <param name="maxCount">The maximum number of bytes to dequeue.</param>
        /// <returns>The dequeued bytes. This can be fewer than requested if no more bytes are available.</returns>
        public byte[] Dequeue(int maxCount)
        {
            return DequeueInternal(maxCount, peek: false);
        }

        /// <summary>
        /// Removes and returns bytes at the beginning of the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write the data to.</param>
        /// <param name="offset">The offset in the <paramref name="buffer"/> to write to.</param>
        /// <param name="maxCount">The maximum number of bytes to dequeue.</param>
        /// <returns>The number of dequeued bytes. This can be less than requested if no more bytes
        ///   are available.</returns>
        public int Dequeue(byte[] buffer, int offset, int maxCount)
        {
            return DequeueInternal(buffer, offset, maxCount, peek: false);
        }

        /// <summary>
        /// Returns bytes at the beginning of the buffer without removing them.
        /// </summary>
        /// <param name="maxCount">The maximum number of bytes to peek.</param>
        /// <returns>The bytes at the beginning of the buffer. This can be fewer than requested if
        ///   no more bytes are available.</returns>
        public byte[] Peek(int maxCount)
        {
            return DequeueInternal(maxCount, peek: true);
        }

        /// <summary>
        /// Removes and returns bytes at the beginning of the buffer. Waits asynchronously until
        /// <paramref name="count"/> bytes are available.
        /// </summary>
        /// <param name="count">The number of bytes to dequeue.</param>
        /// <param name="cancellationToken">A cancellation token used to propagate notification that
        ///	  this operation should be canceled.</param>
        /// <returns>The bytes at the beginning of the buffer.</returns>
        public async Task<byte[]> DequeueAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "The count must not be negative.");

            while (true)
            {
                TaskCompletionSource<bool> myDequeueManualTcs;
                lock (syncObj)
                {
                    if (count <= Count)
                    {
                        return Dequeue(count);
                    }
                    myDequeueManualTcs = Reset(ref dequeueManualTcs);
                }
                await AwaitAsync(myDequeueManualTcs, cancellationToken);
            }
        }

        /// <summary>
        /// Removes and returns bytes at the beginning of the buffer. Waits asynchronously until
        /// <paramref name="count"/> bytes are available.
        /// </summary>
        /// <param name="buffer">The buffer to write the data to.</param>
        /// <param name="offset">The offset in the <paramref name="buffer"/> to write to.</param>
        /// <param name="count">The number of bytes to dequeue.</param>
        /// <param name="cancellationToken">A cancellation token used to propagate notification that
        ///	  this operation should be canceled.</param>
        /// <returns>The bytes at the beginning of the buffer.</returns>
        public async Task DequeueAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "The count must not be negative.");
            if (buffer.Length < offset + count)
                throw new ArgumentException("The buffer is too small for the requested data.", nameof(buffer));

            while (true)
            {
                TaskCompletionSource<bool> myDequeueManualTcs;
                lock (syncObj)
                {
                    if (count <= Count)
                    {
                        Dequeue(buffer, offset, count);
                    }
                    myDequeueManualTcs = Reset(ref dequeueManualTcs);
                }
                await AwaitAsync(myDequeueManualTcs, cancellationToken);
            }
        }

        /// <summary>
        /// Waits asynchronously until bytes are available.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token used to propagate notification that
        ///   this operation should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> myAvailableTcs;
            lock (syncObj)
            {
                if (Count > 0)
                {
                    return;
                }
                myAvailableTcs = Reset(ref availableTcs);
            }
            await AwaitAsync(myAvailableTcs, cancellationToken);
        }

        #endregion Public methods

        #region Private methods

        private byte[] DequeueInternal(int count, bool peek)
        {
            if (count > Count)
                count = Count;
            byte[] bytes = new byte[count];
            DequeueInternal(bytes, 0, count, peek);
            return bytes;
        }

        private int DequeueInternal(byte[] bytes, int offset, int count, bool peek)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "The count must not be negative.");
            if (count == 0)
                return count;   // Easy
            if (bytes.Length < offset + count)
                throw new ArgumentException("The buffer is too small for the requested data.", nameof(bytes));

            lock (syncObj)
            {
                if (count > Count)
                    count = Count;

                if (tail >= head)
                {
                    Array.Copy(buffer, head, bytes, offset, count);
                }
                else
                {
                    if (count <= Capacity - head)
                    {
                        Array.Copy(buffer, head, bytes, offset, count);
                    }
                    else
                    {
                        int headCount = Capacity - head;
                        Array.Copy(buffer, head, bytes, offset, headCount);
                        int wrapCount = count - headCount;
                        Array.Copy(buffer, 0, bytes, offset + headCount, wrapCount);
                    }
                }
                if (!peek)
                {
                    if (count == Count)
                    {
                        isEmpty = true;
                        head = 0;
                        tail = -1;
                        Reset(ref availableTcs);
                    }
                    else
                    {
                        head = (head + count) % Capacity;
                    }

                    if (AutoTrim && Capacity > AutoTrimMinCapacity && Count <= Capacity / 2)
                    {
                        int newCapacity = Count;
                        if (newCapacity < AutoTrimMinCapacity)
                        {
                            newCapacity = AutoTrimMinCapacity;
                        }
                        if (newCapacity < Capacity)
                        {
                            SetCapacity(newCapacity);
                        }
                    }
                }
                return count;
            }
        }

        // Must be called within the lock
        private TaskCompletionSource<bool> Reset(ref TaskCompletionSource<bool> tcs)
        {
            if (tcs.Task.IsCompleted)
            {
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return tcs;
        }

        // Must be called within the lock
        private void Set(TaskCompletionSource<bool> tcs)
        {
            tcs.TrySetResult(true);
        }

        // Must NOT be called within the lock
        private async Task AwaitAsync(TaskCompletionSource<bool> tcs, CancellationToken cancellationToken)
        {
            if (await Task.WhenAny(tcs.Task, Task.Delay(-1, cancellationToken)) == tcs.Task)
            {
                await tcs.Task;   // Already completed
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        #endregion Private methods
    }
}
