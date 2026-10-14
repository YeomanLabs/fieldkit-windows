using System.Windows.Input;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>
/// What a tab's list says when it has no rows to show: loading, nothing found, or failed with
/// Retry. Hidden once the list has loaded (the rows speak for themselves).
/// </summary>
public sealed partial class ListStatus : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(ISectionState), typeof(ListStatus), new PropertyMetadata(null, (d, _) => ((ListStatus)d).Update()));

    public static readonly DependencyProperty NotFoundTextProperty = DependencyProperty.Register(
        nameof(NotFoundText), typeof(string), typeof(ListStatus), new PropertyMetadata("", (d, _) => ((ListStatus)d).Update()));

    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
        nameof(RetryCommand), typeof(ICommand), typeof(ListStatus), new PropertyMetadata(null));

    public static readonly DependencyProperty SubjectProperty = DependencyProperty.Register(
        nameof(Subject), typeof(string), typeof(ListStatus), new PropertyMetadata("", (d, _) => ((ListStatus)d).Update()));

    public ListStatus()
    {
        InitializeComponent();
        Update();
    }

    public ISectionState? State
    {
        get => (ISectionState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string NotFoundText
    {
        get => (string)GetValue(NotFoundTextProperty);
        set => SetValue(NotFoundTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    /// <summary>What's listed, for the Retry button's Narrator name ("Retry devices").</summary>
    public string Subject
    {
        get => (string)GetValue(SubjectProperty);
        set => SetValue(SubjectProperty, value);
    }

    private void Update()
    {
        var kind = State?.Kind ?? SectionKind.Loading;
        LoadingPanel.Visibility = Show(kind == SectionKind.Loading);
        FailedPanel.Visibility = Show(kind == SectionKind.Failed);
        var message = kind switch
        {
            SectionKind.NotFound => NotFoundText,
            SectionKind.Skipped => State?.Message ?? "",
            _ => "",
        };
        MessageText.Text = message;
        MessageText.Visibility = Show(message.Length > 0);
        FailedText.Text = kind == SectionKind.Failed ? State?.Message ?? "" : "";
        AutomationProperties.SetName(RetryButton, $"Retry {Subject}".Trim());
        Visibility = Show(kind != SectionKind.Loaded);

        // Narrator hears "nothing found" and failures without moving focus.
        var announcement = kind == SectionKind.Failed ? FailedText.Text : message;
        if (announcement.Length > 0 && FrameworkElementAutomationPeer.CreatePeerForElement(this) is { } peer)
        {
            peer.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, announcement, "ListStatus");
        }
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (RetryCommand?.CanExecute(null) == true) RetryCommand.Execute(null);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
