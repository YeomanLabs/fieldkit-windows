namespace FieldKit.Core.Auth;

/// <summary>Why typed tenant setup values can't be used. <see cref="AuthConfigProblemExtensions.Message"/> explains each.</summary>
public enum AuthConfigProblem
{
    ClientIdInvalid,
    TenantIdInvalid,
    MultiTenantAuthority,
}
