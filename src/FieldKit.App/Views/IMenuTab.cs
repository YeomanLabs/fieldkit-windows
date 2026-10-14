namespace FieldKit.App.Views;

/// <summary>
/// A tab that starts over when its menu item is chosen (the user's request on 2026-10-14): the
/// search box empties and the list reloads from Graph, as on a first visit.
/// </summary>
internal interface IMenuTab
{
    void StartOver();
}
