using System.Diagnostics;
using FieldKit.Core.Graph;

namespace FieldKit.Core.ViewModels;

/// <summary>Turns an error from a feature's Graph calls into the text its section shows.</summary>
internal static class FeatureErrors
{
    /// <summary>
    /// User-facing text, or null when the section should be left alone: the work was cancelled,
    /// or the session expired and the app is returning to sign-in.
    /// </summary>
    public static string? Message(Exception error, Action onUnauthorized, GraphFeature? feature = null)
    {
        switch (error)
        {
            case OperationCanceledException:
                return null;
            case GraphException { Kind: GraphErrorKind.Unauthorized }:
                onUnauthorized();
                return null;
            case GraphException graph:
                return feature is { } known ? known.Message(graph) : graph.UserMessage;
            default:
                Debug.WriteLine($"Unexpected {error.GetType().Name} in a feature");
                return GraphException.Network().UserMessage;
        }
    }
}
