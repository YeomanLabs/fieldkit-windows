namespace FieldKit.Core.Auth;

public static class AuthConfigProblemExtensions
{
    public static string Message(this AuthConfigProblem problem) =>
        problem switch
        {
            AuthConfigProblem.ClientIdInvalid =>
                "The Application (client) ID should be a GUID like 1a2b3c4d-…, from the app registration's Overview page.",
            AuthConfigProblem.TenantIdInvalid =>
                "The Directory (tenant) ID should be a GUID from the Overview page, or your tenant domain like contoso.onmicrosoft.com.",
            AuthConfigProblem.MultiTenantAuthority =>
                "Use your own tenant ID. FieldKit is single tenant, so \"common\" and \"organizations\" won't work.",
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };
}
