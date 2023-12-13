using System;
using System.Diagnostics;
using System.IO;

public class GptInteractiveProcess : IDisposable
{
    private Process process;
    private StreamWriter streamWriter;
    private TaskCompletionSource<bool> outputReadComplete;
    private TaskCompletionSource<bool> errorReadComplete;

    public event EventHandler<string> OutputReceived;
    public event EventHandler<string> ErrorReceived;
    public string task_id;

    public GptInteractiveProcess(string fileName, string task_id)
    {
        process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data == null)
                outputReadComplete.TrySetResult(true);
            else
                OutputReceived?.Invoke(this, e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null)
                errorReadComplete.TrySetResult(true);
            else
                ErrorReceived?.Invoke(this, e.Data);
        };

        process.Start();

        streamWriter = process.StandardInput;
        outputReadComplete = new TaskCompletionSource<bool>();
        errorReadComplete = new TaskCompletionSource<bool>();

        // Begin asynchronous reading of output and error
        Task.Run(() => process.BeginOutputReadLine());
        Task.Run(() => process.BeginErrorReadLine());
        this.task_id = task_id;
    }

    public async Task WriteLineAsync(string input)
    {
        await streamWriter.WriteLineAsync(input);
    }

    public async Task SendCtrlCAsync()
    {
        // Simulate Ctrl+C by writing the appropriate control character
        await streamWriter.WriteAsync("\x03");
    }

    public async Task<string> ReadOutputAsync()
    {
        await outputReadComplete.Task;
        return await process.StandardOutput.ReadToEndAsync();
    }

    public async Task<string> ReadErrorAsync()
    {
        await errorReadComplete.Task;
        return await process.StandardError.ReadToEndAsync();
    }

    public void Dispose()
    {
        if (!process.HasExited)
        {
            process.Kill();
        }

        process.Dispose();
        streamWriter.Close();
    }
}