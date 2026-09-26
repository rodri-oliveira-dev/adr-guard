using AdrGuard.Generation;
using System.Text;

namespace AdrGuard.Review;

internal static class AdrReviewExplicitContextValidator
{
    internal const long MaximumContextFileBytes = 150000;

    internal const long MaximumAggregateContextFileBytes = 300000;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md",
            ".txt",
        };

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    internal static async Task ValidateAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        long aggregateBytes = 0;

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            var fullPath = Path.GetFullPath(filePath);
            var extension = Path.GetExtension(fullPath);

            if (!SupportedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    $"Unsupported context file extension '{extension}' for '{fullPath}'. "
                    + "Only .md and .txt files are supported.");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Context file does not exist: '{fullPath}'.",
                    fullPath);
            }

            var bytes = await File.ReadAllBytesAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);

            ValidateByteLimits(
                fullPath,
                bytes.LongLength,
                ref aggregateBytes);

            ValidateUtf8(
                fullPath,
                bytes);
        }
    }

    private static void ValidateByteLimits(
        string fullPath,
        long byteCount,
        ref long aggregateBytes)
    {
        if (byteCount > MaximumContextFileBytes)
        {
            throw new InvalidOperationException(
                $"Context file '{fullPath}' exceeds the "
                + $"{MaximumContextFileBytes}-byte per-file limit.");
        }

        aggregateBytes += byteCount;

        if (aggregateBytes > MaximumAggregateContextFileBytes)
        {
            throw new InvalidOperationException(
                "Explicit context files exceed the "
                + $"{MaximumAggregateContextFileBytes}-byte aggregate limit.");
        }
    }

    private static void ValidateUtf8(
        string fullPath,
        byte[] bytes)
    {
        var content = bytes.AsSpan();

        if (HasUtf16OrUtf32Bom(content))
        {
            throw InvalidEncoding(fullPath);
        }

        if (content.StartsWith(
                [0xEF, 0xBB, 0xBF]))
        {
            content = content[3..];
        }

        try
        {
            var decoded = StrictUtf8.GetString(content);

            if (decoded.Contains(
                    '\0',
                    StringComparison.Ordinal))
            {
                throw InvalidEncoding(fullPath);
            }
        }
        catch (DecoderFallbackException)
        {
            throw InvalidEncoding(fullPath);
        }
    }

    private static bool HasUtf16OrUtf32Bom(
        ReadOnlySpan<byte> content) =>
        content.StartsWith([0xFF, 0xFE])
        || content.StartsWith([0xFE, 0xFF])
        || content.StartsWith([0x00, 0x00, 0xFE, 0xFF])
        || content.StartsWith([0xFF, 0xFE, 0x00, 0x00]);

    private static InvalidDataException InvalidEncoding(
        string fullPath) =>
        new(
            $"Context file '{fullPath}' must contain valid UTF-8 text. "
            + "UTF-16, UTF-32, invalid UTF-8, and binary/NUL content are not supported.");
}
