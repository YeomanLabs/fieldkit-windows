namespace FieldKit.App.Views;

/// <summary>Opens the device page.</summary>
/// <param name="IntuneId">Picks a record when a serial has several (e.g. opened from a person's devices).</param>
internal sealed record DeviceRoute(string Serial, string? IntuneId = null)
{
    // The frame hands the same route back on Back, so the page keeps where the user was.

    /// <summary>The record picked on the page since, if any.</summary>
    public string? PickedIntuneId { get; set; }

    /// <summary>The tab showing when the user left: 0 Overview, 1 groups and assignments, 2 hardware, storage and apps, 3 install status.</summary>
    public int Tab { get; set; }
}
