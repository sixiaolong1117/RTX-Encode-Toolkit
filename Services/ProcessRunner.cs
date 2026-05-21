using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RTX_Encode_Toolkit.Models;

namespace RTX_Encode_Toolkit.Services;

public sealed class ProcessRunner
{
    public async Task<int> RunAsync(ProcessCommand command, IProgress<string>? output, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(command);

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"无法启动进程：{command.FileName}");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"无法启动 {command.FileName}，请检查路径或 PATH 环境变量。", ex);
        }

        var outputTask = PumpOutputAsync(process.StandardOutput, output, cancellationToken);
        var errorTask = PumpOutputAsync(process.StandardError, output, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            try
            {
                await Task.WhenAll(outputTask, errorTask);
            }
            catch (OperationCanceledException)
            {
            }

            throw;
        }

        return process.ExitCode;
    }

    public async Task<ProcessCaptureResult> CaptureAsync(ProcessCommand command, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(command);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"无法启动进程：{command.FileName}");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"无法启动 {command.FileName}，请检查路径或 PATH 环境变量。", ex);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            stdout.Append(await outputTask);
            stderr.Append(await errorTask);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            throw;
        }

        return new ProcessCaptureResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static Process CreateProcess(ProcessCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    }

    private static async Task PumpOutputAsync(
        StreamReader reader,
        IProgress<string>? output,
        CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var line = new StringBuilder();
        var previousWasCarriageReturn = false;

        while (true)
        {
            var read = await reader.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                var ch = buffer[index];
                if (ch == '\r')
                {
                    FlushLine(line, output);
                    previousWasCarriageReturn = true;
                    continue;
                }

                if (ch == '\n')
                {
                    if (!previousWasCarriageReturn)
                    {
                        FlushLine(line, output);
                    }

                    previousWasCarriageReturn = false;
                    continue;
                }

                previousWasCarriageReturn = false;
                line.Append(ch);
            }
        }

        FlushLine(line, output);
    }

    private static void FlushLine(StringBuilder line, IProgress<string>? output)
    {
        if (line.Length == 0)
        {
            return;
        }

        output?.Report(line.ToString());
        line.Clear();
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}

public sealed record ProcessCaptureResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string CombinedOutput => StandardOutput + Environment.NewLine + StandardError;
}
