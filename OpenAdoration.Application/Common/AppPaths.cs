namespace OpenAdoration.Application.Common;

/// <summary>
/// Absolute paths the app stores user data at, supplied once by the composition root.
/// Lets file-level features (e.g. backup) locate the DB, media and settings without
/// re-deriving the convention.
/// </summary>
public sealed record AppPaths(string DbPath, string MediaDirectory, string SettingsPath);
