namespace FieldKit.Core.Domain;

/// <summary>Why a sign-in ended the way it did, and what to look at next.</summary>
public sealed record SignInDiagnosis(SignInVerdict Verdict, string Title, string Explanation, IReadOnlyList<string> WhatToCheck)
{
    /// <summary>"Blocked", "Failed", "Interrupted" or "Signed in", for the list.</summary>
    public string Label => Verdict switch
    {
        SignInVerdict.Blocked => "Blocked",
        SignInVerdict.Failed => "Failed",
        SignInVerdict.Interrupted => "Interrupted",
        _ => "Signed in",
    };

    public bool IsProblem => Verdict != SignInVerdict.Success;
}
