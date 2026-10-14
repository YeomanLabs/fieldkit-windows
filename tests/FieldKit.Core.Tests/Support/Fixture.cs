namespace FieldKit.Core.Tests.Support;

/// <summary>Graph response fixtures (this project's Fixtures folder, copied to the build output).</summary>
internal static class Fixture
{
    public static byte[] Data(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json"));
}
