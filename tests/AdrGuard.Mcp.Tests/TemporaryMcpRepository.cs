using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AdrGuard.Mcp.Tests;

internal sealed class TemporaryMcpRepository : IDisposable
{
    private TemporaryMcpRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "adr-guard-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "docs", "adr"));
    }

    internal string Root { get; }

    internal static TemporaryMcpRepository Create() => new();

    internal void Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }

    internal void WriteBytes(string relativePath, byte[] content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    internal IReadOnlyDictionary<string, string> Snapshot() =>
        Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/'),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.Ordinal);

    internal void InitializeGit()
    {
        RunGit("init");
        RunGit("config", "user.email", "adr-guard-tests@example.invalid");
        RunGit("config", "user.name", "ADR Guard Tests");
        RunGit("add", ".");
        RunGit("commit", "-m", "fixture");
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root)) return;
        foreach (var path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }

    private void RunGit(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git fixture setup failed: {output}{error}");
        }
    }
}
