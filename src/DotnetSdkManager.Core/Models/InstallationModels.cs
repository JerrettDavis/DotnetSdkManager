namespace DotnetSdkManager.Models;

public sealed record InstalledSdkManifest(
    string Version,
    string Channel,
    string TargetRid,
    string ArtifactRid,
    string Source,
    string Sha512,
    DateTimeOffset InstalledAtUtc,
    bool IsEol,
    string? SupportPhase,
    string? ReleaseType);

public sealed record ManagedSdk(
    string Version,
    string Channel,
    string TargetRid,
    string ArtifactRid,
    string Root,
    DateTimeOffset InstalledAtUtc,
    bool IsEol,
    bool IsActive,
    string Sha512);

public sealed record SystemSdk(string Version, string Root);

public sealed record InstallResult(ManagedSdk Sdk, bool AlreadyInstalled);

public sealed record ActiveSdk(
    string Version,
    string TargetRid,
    string Root,
    DateTimeOffset ActivatedAtUtc);

public sealed record DoctorCheck(string Name, bool Success, string Message);
