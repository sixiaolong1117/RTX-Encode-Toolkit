using System;
using System.ComponentModel;
using System.Diagnostics;
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

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output?.Report(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output?.Report(args.Data);
            }
        };

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

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
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
