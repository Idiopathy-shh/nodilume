internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("Domain invariants", DomainTests.RunAsync),
            ("SQLite integration", SqliteTests.RunAsync),
            ("Semantic projection", SemanticProjectionTests.RunAsync),
            ("Local edits and view state", LocalEditTests.RunAsync)
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(exception);
            }
        }

        Console.WriteLine($"{tests.Length - failed}/{tests.Length} groups PASS");
        return failed == 0 ? 0 : 1;
    }
}