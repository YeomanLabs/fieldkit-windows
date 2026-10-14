using CommunityToolkit.Mvvm.ComponentModel;
using FieldKit.Core.Domain;
using FieldKit.Core.Graph;
using FieldKit.Core.Persistence;

namespace FieldKit.Core.ViewModels;

/// <summary>Phase 2: change one Autopilot record's group tag and assigned user (ports <c>AutopilotEditModel</c>).</summary>
public sealed partial class AutopilotEditViewModel : ObservableObject
{
    private readonly AutopilotUpdates _updates;
    private readonly SettingsStore _settings;
    private readonly Action _onUnauthorized;

    public AutopilotEditViewModel(AutopilotDevice record, AutopilotUpdates updates, SettingsStore settings, Action onUnauthorized)
    {
        Record = record;
        GroupTagText = record.GroupTag;
        UserChange = AutopilotUserChange.Keep;
        _updates = updates;
        _settings = settings;
        _onUnauthorized = onUnauthorized;
    }

    public AutopilotDevice Record { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(CanSave), nameof(SuggestedTags), nameof(HasSuggestedTags))]
    public partial string GroupTagText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(CanSave), nameof(AssignedUserText), nameof(ShowsRemoveToggle), nameof(RemoveToggleText))]
    public partial AutopilotUserChange UserChange { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    public string? NormalizedTag => GroupTag.Normalized(GroupTagText);

    /// <summary>An invalid tag counts as a change, so Save can explain what's wrong with it.</summary>
    public bool HasChanges =>
        (NormalizedTag is not { } tag || tag != Record.GroupTag) || UserChange != AutopilotUserChange.Keep;

    public bool CanSave => HasChanges && !IsSaving;

    /// <summary>Recent tags other than the one typed, as one-click choices.</summary>
    public IReadOnlyList<string> SuggestedTags => _settings.RecentGroupTags.Where(tag => tag != GroupTagText).ToList();

    public bool HasSuggestedTags => SuggestedTags.Count > 0;

    public string AssignedUserText => UserChange switch
    {
        AutopilotUserChange.Assign assign => assign.User.UserPrincipalName,
        AutopilotUserChange.RemoveUser => "None (will be removed)",
        _ => Record.AssignedUser.Length == 0 ? "None" : Record.AssignedUser,
    };

    public string ChangeUserText => Record.AssignedUser.Length == 0 ? "Assign a user" : "Change user";

    public bool ShowsRemoveToggle => Record.AssignedUser.Length > 0 || UserChange != AutopilotUserChange.Keep;

    public string RemoveToggleText => UserChange == AutopilotUserChange.Remove ? "Keep current user" : "Remove assigned user";

    public string SerialNumber => Record.SerialNumber;

    public string ModelText => DeviceText.Join(" ", Record.Manufacturer, Record.Model);

    public void ToggleRemove() =>
        UserChange = UserChange == AutopilotUserChange.Remove ? AutopilotUserChange.Keep : AutopilotUserChange.Remove;

    public void AssignUser(DirectoryUser user) => UserChange = new AutopilotUserChange.Assign(user);

    /// <summary>Applies the changes. Returns true when Graph accepted all of them.</summary>
    public async Task<bool> SaveAsync()
    {
        if (NormalizedTag is not { } tag)
        {
            ErrorMessage = $"Group tags can be up to {GroupTag.MaxLength} characters, without line breaks.";
            return false;
        }
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            if (tag != Record.GroupTag)
            {
                await _updates.SetGroupTagAsync(tag, Record.Id);
                _settings.RememberGroupTag(tag);
            }
            switch (UserChange)
            {
                case AutopilotUserChange.Assign assign:
                    await _updates.AssignUserAsync(assign.User.UserPrincipalName, assign.User.DisplayName, Record.Id);
                    break;
                case AutopilotUserChange.RemoveUser:
                    await _updates.UnassignUserAsync(Record.Id);
                    break;
            }
            return true;
        }
        catch (Exception error)
        {
            if (FeatureErrors.Message(error, _onUnauthorized, GraphFeature.AutopilotEdit) is { } message) ErrorMessage = message;
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
