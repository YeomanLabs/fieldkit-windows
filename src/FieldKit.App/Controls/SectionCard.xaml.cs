using System.Windows.Input;
using FieldKit.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace FieldKit.App.Controls;

/// <summary>
/// A card for one independently loading section: loading, not found, skipped and failed (with
/// Retry) are shown here; the content appears only once the section has loaded.
/// </summary>
[ContentProperty(Name = nameof(CardContent))]
public sealed partial class SectionCard : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SectionCard), new PropertyMetadata(""));

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(ISectionState), typeof(SectionCard), new PropertyMetadata(null, (d, _) => ((SectionCard)d).Update()));

    public static readonly DependencyProperty NotFoundTextProperty = DependencyProperty.Register(
        nameof(NotFoundText), typeof(string), typeof(SectionCard), new PropertyMetadata("", (d, _) => ((SectionCard)d).Update()));

    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
        nameof(RetryCommand), typeof(ICommand), typeof(SectionCard), new PropertyMetadata(null));

    public static readonly DependencyProperty CardContentProperty = DependencyProperty.Register(
        nameof(CardContent), typeof(object), typeof(SectionCard), new PropertyMetadata(null));

    public SectionCard()
    {
        InitializeComponent();
        Update();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public ISectionState? State
    {
        get => (ISectionState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>What to say when the lookup found nothing, e.g. "Not registered in Autopilot."</summary>
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

    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    private void Update()
    {
        var kind = State?.Kind ?? SectionKind.Loading;
        LoadingPanel.Visibility = Show(kind == SectionKind.Loading);
        ContentHost.Visibility = Show(kind == SectionKind.Loaded);
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
        AutomationProperties.SetName(RetryButton, $"Retry {Header}");
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (RetryCommand?.CanExecute(null) == true) RetryCommand.Execute(null);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
