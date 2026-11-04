using System.Globalization;
using AdrGuard.Configuration;
using AdrGuard.Git;
using AdrGuard.Validation;

namespace AdrGuard.Diagnostics;

internal static class DoctorDiagnosticService
{
    private const int GitOutputLimitPerStream = 2 * 1024;
    private const string RepositoryArea = "Repository";
    private const string ConfigurationArea = "Configuration";
    private const string AdrDirectoryArea = "ADR directory";
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] ConfigurationProperties =
    [
        "conventional-supersession",
        "template",
        "adr-format",
        "lifecycle-statuses",
        "filename-policy",
        "placeholder-policy",
        "metadata-policy",
        "validation-profile",
    ];

    internal static DoctorReport Run(
        string selectedDirectory,
        string cliVersion,
        Func<string, CancellationToken, string>? gitVersionProbe = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedDirectory);
        ArgumentNullException.ThrowIfNull(cliVersion);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<DoctorDiagnostic>(6)
        {
            CheckCli(cliVersion),
        };

        var repository = CheckRepository(selectedDirectory);
        diagnostics.Add(repository.Diagnostic);

        var configuration = CheckConfiguration(repository.RootDirectory);
        diagnostics.Add(configuration.Diagnostic);
        diagnostics.Add(CheckAdrDirectory(repository.RootDirectory, configuration));
        diagnostics.Add(CheckRuntime());
        diagnostics.Add(CheckGit(
            repository.RootDirectory ?? GetExistingWorkingDirectory(selectedDirectory),
            gitVersionProbe,
            cancellationToken));

        return new DoctorReport(diagnostics);
    }

    private static DoctorDiagnostic CheckCli(string version)
    {
        var safeVersion = SanitizeVersion(version);
        return safeVersion is null
            ? Error("CLI", "application identity could not be determined.")
            : Ok("CLI", safeVersion);
    }

    private static RepositoryCheck CheckRepository(string selectedDirectory)
    {
        try
        {
            var selectedRoot = Path.GetFullPath(selectedDirectory);
            if (!Directory.Exists(selectedRoot))
            {
                return new RepositoryCheck(
                    null,
                    Error(
                        RepositoryArea,
                        "the selected directory does not exist. Fix: run from an existing repository or pass its directory."));
            }

            ProveDirectoryReadable(selectedRoot);
            RepositoryPath.ResolveContained(selectedRoot, ".", allowMissingLeaf: false);

            if (!AdrRepositoryRoot.TryResolve(selectedRoot, out var repositoryRoot))
            {
                return new RepositoryCheck(
                    null,
                    Error(
                        RepositoryArea,
                        "no ADR Guard or Git repository was detected. Fix: run from an existing repository or pass its directory."));
            }

            var selectedRelativePath = Path.GetRelativePath(repositoryRoot, selectedRoot);
            RepositoryPath.ResolveContained(repositoryRoot, selectedRelativePath, allowMissingLeaf: false);
            ProveDirectoryReadable(repositoryRoot);

            var hasGitMarker = Directory.Exists(Path.Combine(repositoryRoot, ".git"))
                || File.Exists(Path.Combine(repositoryRoot, ".git"));
            return hasGitMarker
                ? new RepositoryCheck(
                    repositoryRoot,
                    Ok(RepositoryArea, "detected at the selected root (Git worktree)."))
                : new RepositoryCheck(
                    repositoryRoot,
                    Warning(
                        RepositoryArea,
                        "ADR Guard configuration was found without a Git marker; Git-based features are unavailable."));
        }
        catch (Exception exception) when (IsPathOrAccessException(exception))
        {
            return new RepositoryCheck(
                null,
                Error(
                    RepositoryArea,
                    "the selected directory is inaccessible or unsafe. Fix: run from an existing repository or pass its directory."));
        }
    }

    private static ConfigurationCheck CheckConfiguration(string? repositoryRoot)
    {
        if (repositoryRoot is null)
        {
            return new ConfigurationCheck(
                ConfigurationState.NotChecked,
                null,
                Warning(ConfigurationArea, "not checked because no repository root was identified."));
        }

        try
        {
            var configuration = AdrGuardConfigurationLoader.Load(repositoryRoot);
            return configuration is null
                ? new ConfigurationCheck(
                    ConfigurationState.Missing,
                    null,
                    Warning(
                        ConfigurationArea,
                        ".adrguard.yml was not found; legacy defaults remain active. Fix: review 'adr-guard init --dry-run' when shared configuration is desired."))
                : new ConfigurationCheck(
                    ConfigurationState.Valid,
                    configuration,
                    Ok(ConfigurationArea, ".adrguard.yml schema 1 loaded."));
        }
        catch (AdrGuardConfigurationException exception)
        {
            return new ConfigurationCheck(
                ConfigurationState.Invalid,
                null,
                Error(ConfigurationArea, DescribeConfigurationFailure(exception)));
        }
    }

    private static DoctorDiagnostic CheckAdrDirectory(
        string? repositoryRoot,
        ConfigurationCheck configuration)
    {
        if (repositoryRoot is null)
        {
            return Warning(AdrDirectoryArea, "not checked because no repository root was identified.");
        }

        if (configuration.State == ConfigurationState.Invalid)
        {
            return Warning(AdrDirectoryArea, "not checked because configuration is invalid.");
        }

        if (configuration.State == ConfigurationState.Missing)
        {
            return Warning(
                AdrDirectoryArea,
                "the legacy scope is the selected root; no dedicated ADR directory is configured.");
        }

        if (configuration.State == ConfigurationState.NotChecked
            || configuration.Configuration is null)
        {
            return Warning(AdrDirectoryArea, "not checked because configuration is unavailable.");
        }

        try
        {
            var path = RepositoryPath.ResolveContained(
                repositoryRoot,
                configuration.Configuration.AdrDirectory,
                allowMissingLeaf: true);
            if (!Directory.Exists(path))
            {
                return Error(
                    AdrDirectoryArea,
                    $"{DisplayRelativePath(configuration.Configuration.AdrDirectory)} does not exist. Fix: create or correct the directory inside the repository, or adjust 'adr-directory'.");
            }

            RepositoryPath.ResolveContained(
                repositoryRoot,
                configuration.Configuration.AdrDirectory,
                allowMissingLeaf: false);
            ProveDirectoryReadable(path);
            return Ok(
                AdrDirectoryArea,
                $"{DisplayRelativePath(configuration.Configuration.AdrDirectory)} exists and is readable.");
        }
        catch (Exception exception) when (IsPathOrAccessException(exception))
        {
            return Error(
                AdrDirectoryArea,
                "the configured directory is inaccessible or unsafe. Fix: create or correct the directory inside the repository, or adjust 'adr-directory'.");
        }
    }

    private static DoctorDiagnostic CheckRuntime()
    {
        var version = SanitizeVersion(Environment.Version.ToString());
        return version is null
            ? Error(".NET runtime", "runtime identity could not be determined.")
            : Ok(".NET runtime", version);
    }

    private static DoctorDiagnostic CheckGit(
        string workingDirectory,
        Func<string, CancellationToken, string>? gitVersionProbe,
        CancellationToken cancellationToken)
    {
        try
        {
            var output = gitVersionProbe is null
                ? GitCommandRunner.RunBounded(
                    workingDirectory,
                    ["--version"],
                    GitTimeout,
                    GitOutputLimitPerStream,
                    cancellationToken)
                : gitVersionProbe(workingDirectory, cancellationToken);
            var version = ParseGitVersion(output);
            return version is null
                ? GitWarning("returned an unrecognized version")
                : Ok("Git", $"{version} available.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is GitOperationException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return GitWarning("executable not available or did not respond within local safety limits");
        }
    }

    private static DoctorDiagnostic GitWarning(string reason) =>
        Warning(
            "Git",
            $"{reason}. Fix: install or correct Git only for '--changed', 'impact', 'drift', or other Git-based comparisons.");

    private static string DescribeConfigurationFailure(AdrGuardConfigurationException exception)
    {
        var message = exception.Message;
        if (message.Contains("symbolic link or reparse point", StringComparison.Ordinal))
        {
            return ".adrguard.yml is a symbolic link or reparse point. Fix: replace it with a regular file inside the repository.";
        }

        if (message.Contains("exceeds the", StringComparison.Ordinal))
        {
            return ".adrguard.yml exceeds the 65536-byte limit. Fix: reduce it to the supported schema 1 scalar configuration.";
        }

        if (TryDescribeInvalidLine(message, out var invalidLine))
        {
            return invalidLine;
        }

        if (message.Contains("missing required property 'schema-version'", StringComparison.Ordinal))
        {
            return ".adrguard.yml is missing required property 'schema-version'. Fix: set 'schema-version: 1'.";
        }

        if (message.Contains("unsupported schema-version", StringComparison.Ordinal))
        {
            return ".adrguard.yml uses an unsupported schema-version. Fix: set 'schema-version: 1'.";
        }

        if (message.Contains("contains an unsafe path", StringComparison.Ordinal))
        {
            return ".adrguard.yml contains an unsafe path. Fix: use relative paths that stay inside the repository and do not traverse symbolic links.";
        }

        if (message.Contains("cannot define both 'template' and 'template-file'", StringComparison.Ordinal))
        {
            return ".adrguard.yml defines both 'template' and 'template-file'. Fix: keep only one template source.";
        }

        var property = ConfigurationProperties.FirstOrDefault(
            candidate => message.Contains($"property '{candidate}'", StringComparison.Ordinal));
        if (property is not null)
        {
            return $".adrguard.yml has an invalid value for '{property}'. Fix: use a supported schema 1 value.";
        }

        return ".adrguard.yml could not be read or parsed. Fix: correct the file as a readable schema 1 scalar configuration.";
    }

    private static bool TryDescribeInvalidLine(string message, out string description)
    {
        const string Marker = " at line ";
        var markerIndex = message.IndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            description = string.Empty;
            return false;
        }

        var numberStart = markerIndex + Marker.Length;
        var separatorIndex = message.IndexOf(": ", numberStart, StringComparison.Ordinal);
        if (separatorIndex < 0
            || !int.TryParse(
                message.AsSpan(numberStart, separatorIndex - numberStart),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var lineNumber))
        {
            description = string.Empty;
            return false;
        }

        var reason = message[(separatorIndex + 2)..].TrimEnd('.');
        var safeReason = DescribeParseReason(reason);
        description = $".adrguard.yml line {lineNumber} {safeReason}. Fix: use supported schema 1 top-level scalar properties.";
        return true;
    }

    private static string DescribeParseReason(string reason)
    {
        const string UnknownProperty = "unknown property '";
        if (reason.StartsWith(UnknownProperty, StringComparison.Ordinal)
            && reason.EndsWith('\''))
        {
            var property = reason[UnknownProperty.Length..^1];
            if (property.Length is > 0 and <= 32
                && property.All(IsSafePropertyCharacter))
            {
                return $"has unknown property '{property}'";
            }

            return "contains an unknown property";
        }

        if (reason.StartsWith("duplicate property '", StringComparison.Ordinal))
        {
            return "contains a duplicate property";
        }

        if (reason.Contains("top-level scalar", StringComparison.Ordinal))
        {
            return "contains unsupported nested or non-scalar content";
        }

        if (reason.Contains("expected 'property: value'", StringComparison.Ordinal))
        {
            return "does not use the expected 'property: value' syntax";
        }

        if (reason.Contains("property value cannot be empty", StringComparison.Ordinal))
        {
            return "contains an empty property value";
        }

        if (reason.Contains("unterminated", StringComparison.Ordinal)
            || reason.Contains("escape", StringComparison.Ordinal))
        {
            return "contains an invalid quoted scalar";
        }

        if (reason.Contains("collections, tags, anchors", StringComparison.Ordinal))
        {
            return "contains unsupported YAML structures";
        }

        return "is malformed";
    }

    private static bool IsSafePropertyCharacter(char character) =>
        character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';

    private static string? ParseGitVersion(string output)
    {
        const string Prefix = "git version ";
        var line = output.ReplaceLineEndings("\n").Split('\n', 2)[0].Trim();
        if (!line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versionText = line[Prefix.Length..];
        var separator = versionText.IndexOf(' ');
        return SanitizeVersion(separator < 0 ? versionText : versionText[..separator]);
    }

    private static string? SanitizeVersion(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is > 0 and <= 80
            && char.IsAsciiDigit(trimmed[0])
            && trimmed.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '.' or '-' or '+' or '_')
                ? trimmed
                : null;
    }

    private static string DisplayRelativePath(string path)
    {
        var normalized = path == "." ? "." : path.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.Length <= 160
            && !normalized.Contains("::", StringComparison.Ordinal)
            && normalized.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '/' or '.' or '_' or '-' or ' ')
                ? normalized
                : "configured ADR directory";
    }

    private static string GetExistingWorkingDirectory(string selectedDirectory)
    {
        try
        {
            var fullPath = Path.GetFullPath(selectedDirectory);
            return Directory.Exists(fullPath) ? fullPath : Directory.GetCurrentDirectory();
        }
        catch (Exception exception) when (IsPathOrAccessException(exception))
        {
            return Directory.GetCurrentDirectory();
        }
    }

    private static void ProveDirectoryReadable(string path)
    {
        using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        _ = enumerator.MoveNext();
    }

    private static bool IsPathOrAccessException(Exception exception) =>
        exception is ArgumentException
        or IOException
        or UnauthorizedAccessException
        or NotSupportedException
        or System.Security.SecurityException;

    private static DoctorDiagnostic Ok(string check, string message) =>
        new(DoctorDiagnosticSeverity.Ok, check, message);

    private static DoctorDiagnostic Warning(string check, string message) =>
        new(DoctorDiagnosticSeverity.Warning, check, message);

    private static DoctorDiagnostic Error(string check, string message) =>
        new(DoctorDiagnosticSeverity.Error, check, message);

    private sealed record RepositoryCheck(string? RootDirectory, DoctorDiagnostic Diagnostic);

    private sealed record ConfigurationCheck(
        ConfigurationState State,
        AdrGuardConfiguration? Configuration,
        DoctorDiagnostic Diagnostic);

    private enum ConfigurationState
    {
        NotChecked,
        Missing,
        Valid,
        Invalid,
    }
}

internal enum DoctorDiagnosticSeverity
{
    Ok,
    Warning,
    Error,
}

internal sealed record DoctorDiagnostic(
    DoctorDiagnosticSeverity Severity,
    string Check,
    string Message);

internal sealed record DoctorReport(IReadOnlyList<DoctorDiagnostic> Diagnostics)
{
    internal bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == DoctorDiagnosticSeverity.Error);
}
