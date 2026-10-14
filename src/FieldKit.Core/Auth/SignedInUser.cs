namespace FieldKit.Core.Auth;

/// <param name="TenantName">Filled in from Graph after sign-in; null until then or if the lookup fails.</param>
public sealed record SignedInUser(string DisplayName, string Username, string TenantId, string? TenantName = null);
