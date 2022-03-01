using System.Threading.Tasks;
using DigitalTwinsService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwinsService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var services = BuildServiceProvider();
            var tester = new ServiceTester(services.GetRequiredService<DigitalTwinsService>());

            await tester.UploadADTModels();
            await tester.CreateTestTwins();

            var testPersons = await tester.GetAllTestPersons();
        }
        
        private static ServiceProvider BuildServiceProvider() =>
            new ServiceCollection()
                .AddSingleton<IConfiguration>(BuildConfigurationRoot())
                .AddSingleton(x => new DigitalTwinsService(x.GetRequiredService<IConfiguration>()))
                .BuildServiceProvider();
        
        private static IConfigurationRoot BuildConfigurationRoot() =>
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();
    }
}