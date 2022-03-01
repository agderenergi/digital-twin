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
            var basicTwin = await tester.GetTwin("Test_6740cde7-961a-4e19-b914-be6cdd9450ea");
            var node = await tester.GetNode<TestPerson>("Test_6740cde7-961a-4e19-b914-be6cdd9450ea");
            
            var basicTwin2 = await tester.GetTwin("Test_693e3e41-d2db-45b4-91ad-2c4cbebe81bf");
            var node2 = await tester.GetNode<TestPerson>("Test_693e3e41-d2db-45b4-91ad-2c4cbebe81bf");
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