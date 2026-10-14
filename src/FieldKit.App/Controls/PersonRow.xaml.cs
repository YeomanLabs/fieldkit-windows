using FieldKit.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FieldKit.App.Controls;

/// <summary>A person in search results (ports <c>PersonRow.swift</c>).</summary>
public sealed partial class PersonRow : UserControl
{
    public static readonly DependencyProperty PersonProperty = DependencyProperty.Register(
        nameof(Person), typeof(DirectoryUser), typeof(PersonRow), new PropertyMetadata(null, (d, _) => ((PersonRow)d).Update()));

    public PersonRow()
    {
        InitializeComponent();
    }

    public DirectoryUser? Person
    {
        get => (DirectoryUser?)GetValue(PersonProperty);
        set => SetValue(PersonProperty, value);
    }

    private void Update()
    {
        if (Person is not { } person) return;
        Picture.DisplayName = person.DisplayName;
        NameText.Text = person.DisplayName;
        UpnText.Text = person.UserPrincipalName;
        SubtitleText.Text = person.Subtitle;
        SubtitleText.Visibility = person.Subtitle.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(this, string.Join(", ", new[] { person.DisplayName, person.UserPrincipalName, person.Subtitle }.Where(part => part.Length > 0)));
    }
}
