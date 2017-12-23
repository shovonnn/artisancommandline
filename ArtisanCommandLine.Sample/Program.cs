using McMaster.Extensions.CommandLineUtils;

namespace ArtisanCommandLine.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "HelloWorld";
            app.HelpOption();
            app.BindConvention<QueueProcessorCommand>("queue");
            app.BindConvention<AdvancedQueueProcessorCommand>("advanced-queue");
            app.Execute(args);
        }
    }
}
