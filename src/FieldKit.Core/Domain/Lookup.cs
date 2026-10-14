namespace FieldKit.Core.Domain;

/// <summary>The outcome of one Graph lookup, as seen by flag evaluation.</summary>
public readonly record struct Lookup<T>
{
    private Lookup(LookupState state, T? value)
    {
        State = state;
        Value = value;
    }

    public LookupState State { get; }

    /// <summary>The value when <see cref="State"/> is <see cref="LookupState.Found"/>.</summary>
    public T? Value { get; }

    public static Lookup<T> Found(T value) => new(LookupState.Found, value);

    public static Lookup<T> NotFound => new(LookupState.NotFound, default);

    public static Lookup<T> Skipped => new(LookupState.Skipped, default);

    public static Lookup<T> Unavailable => new(LookupState.Unavailable, default);
}
