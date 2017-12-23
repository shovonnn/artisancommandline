using System;
using Microsoft.Extensions.DependencyInjection;
namespace ArtisanCommandLine
{
    public abstract class BaseConsoleCommand
    {
        private IServiceProvider serviceProvider;

        public virtual void ConfigureServices(IServiceCollection services)
        {
        }

        public void Initialize()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public object ResolveType(Type type)
        {
            return serviceProvider.GetService(type);
        }
    }
}
