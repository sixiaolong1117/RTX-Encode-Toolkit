using System.Collections.Generic;

namespace RTX_Encode_Toolkit.Models;

public sealed class EncodePlan
{
    public required ProcessCommand MainCommand { get; init; }

    public ProcessCommand? PreprocessCommand { get; init; }

    public ProcessCommand? PostprocessCommand { get; init; }

    public required string OutputPath { get; init; }

    public string? TemporaryInputPath { get; init; }

    public string? TemporaryOutputPath { get; init; }

    public string? ResolvedVsrResolution { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string ToCommandPreview()
    {
        var result = MainCommand.ToCommandLine();
        if (PostprocessCommand is not null)
        {
            result += "\n\n" + PostprocessCommand.ToCommandLine();
        }

        if (PreprocessCommand is not null)
        {
            result = PreprocessCommand.ToCommandLine() + "\n\n" + result;
        }

        return result;
    }
}
