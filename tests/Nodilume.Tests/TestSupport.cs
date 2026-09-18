using Nodilume.Core;

internal static class Check
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
    }

    public static T Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T exception) { return exception; }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{message} Wrong exception: {exception.GetType().Name}.", exception);
        }
        throw new InvalidOperationException($"{message} No exception was thrown.");
    }

    public static async Task<T> ThrowsAsync<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); }
        catch (T exception) { return exception; }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{message} Wrong exception: {exception.GetType().Name}.", exception);
        }
        throw new InvalidOperationException($"{message} No exception was thrown.");
    }
}

internal sealed class TempDatabase : IAsyncDisposable
{
    public TempDatabase(string name)
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NodilumeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Path = System.IO.Path.Combine(Root, $"{name}.sqlite");
    }

    public string Root { get; }
    public string Path { get; }

    public ValueTask DisposeAsync()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return ValueTask.CompletedTask;
    }
}

internal static class TestIds
{
    public static Guid G(int value) => Guid.Parse($"10000000-0000-0000-0000-{value:D12}");
    public static MapId Map(int value) => new(G(value));
    public static IdeaId Idea(int value) => new(G(value));
    public static PlacementId Placement(int value) => new(G(value));
    public static RelationId Relation(int value) => new(G(value));
}