using System.Collections.Generic;

namespace RTX_Encode_Toolkit.Models;

public sealed class EncodePlan
{
    public required ProcessCommand MainCommand { get; init; }

    public ProcessCommand? PreprocessCommand { get; init; }

    public required string OutputPath { get; init; }

    public string? TemporaryInputPath { get; init; }

    public string? ResolvedVsrResolution { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string ToCommandPreview()
    {
        if (PreprocessCommand is null)
        {
            return MainCommand.ToCommandLine();
        }

        return PreprocessCommand.ToCommandLine() + "\n\n" + MainCommand.ToCommandLine();
    }
}
