namespace FieldKit.Core.Graph;

/// <summary><c>{ "returnCode": 3010, "type": "softReboot" }</c>.</summary>
internal sealed class Win32LobAppReturnCodeDto
{
    public int? ReturnCode { get; init; }

    public string? Type { get; init; }
}
