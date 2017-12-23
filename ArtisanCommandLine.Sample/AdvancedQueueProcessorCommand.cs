using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArtisanCommandLine.Sample
{
    public class AdvancedQueueProcessorCommand : BaseConsoleCommand
    {
        public bool ShouldRunContinuously { get; set; }

        [ConventionIsRequired]
        public string QueueName { get; set; }

        [ConventionIgnore]
        public string SomeRandomProperty { get; set; }

        public override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddLogging(logging => logging.AddConsole());
        }

        public async Task<int> Handle(List<string> jobs, [ConventionOption] int userId, CancellationToken token, ILogger<AdvancedQueueProcessorCommand> logger)
        {
            if (jobs != null) logger.LogInformation(String.Join(", ", jobs));
            logger.LogInformation($"user id: {userId}");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
            catch
            {
                logger.LogWarning("process cancelled abruptly");
            }
            logger.LogInformation("closing");
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
}
