```c#
public class QueueProcessorCommand
{
    public bool ShouldRunContinuously { get; set; }

    [ConventionIsRequired]
    public string QueueName { get; set; }

    [ConventionIgnore]
    public string SomeRandomProperty { get; set; }

    public async Task<int> Handle(List<string> jobs, [ConventionOption] int userId, CancellationToken token)
    {
        if (jobs != null) Console.WriteLine(String.Join(", ", jobs));
        Console.WriteLine($"user id: {userId}");
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
        }
        catch
        {
            Console.WriteLine("process cancelled abruptly");
        }
        Console.WriteLine("closing");
        return 0;
    }

    [ConventionDescription("sub command to list all jobs")]
    public int HandleList()
    {
        Console.WriteLine("showing list..");
        return 0;
    }

    [ConventionDescription("sub command to delete a job")]
    public int HandleDeleteJob([ConventionOption, ConventionIsRequired] int jobId)
    {
        Console.WriteLine("deleted job");
        return 0;
    }
}

internal class Program
{
    public static void Main(string[] args)
    {
        var app = new CommandLineApplication();
        app.Name = "HelloWorld";
        app.HelpOption();
        app.BindConvention<QueueProcessorCommand>("queue");
        app.Execute(args);
    }
}
```
