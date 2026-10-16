namespace FieldKit.Core.Domain;

/// <summary>The groups a device and its primary user are in (including nested groups), ID to display name.</summary>
/// <param name="HasUser">The device has a primary user, so "All users" assignments reach it.</param>
public sealed record DeviceMembership(IReadOnlyDictionary<string, string> DeviceGroups, IReadOnlyDictionary<string, string> UserGroups, bool HasUser);
