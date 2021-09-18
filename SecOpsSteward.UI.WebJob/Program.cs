using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecOpsSteward.Data;
using SecOpsSteward.Integrations.Azure;
using SecOpsSteward.Plugins.Azure;
using SecOpsSteward.Shared;

namespace SecOpsSteward.UI.WebJob
{
    internal class Program
    {
        private static IConfiguration Configuration { get; set; }

        private static async Task Main(string[] args)
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Development.json", true, true)
                .AddEnvironmentVariables()
                .Build();

            //setup our DI
            var sp = new ServiceCollection()
                .AddLogging(l => l.AddConsole());

            sp.AddDbContextFactory<SecOpsStewardDbContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("Database"));
            });

            sp.AddScoped(p => p.GetRequiredService<IDbContextFactory<SecOpsStewardDbContext>>().CreateDbContext());

            var config = new ChimeraServiceConfigurator(new Dictionary<string, string>
            {
                {"SubscriptionId", Configuration.GetSection("Chimera")["SubscriptionId"]},
                {"ResourceGroup", Configuration.GetSection("Chimera")["ResourceGroup"]},
                {"VaultName", Configuration.GetSection("Chimera")["VaultName"]},
                {"PackageRepoAccount", Configuration.GetSection("Chimera")["PackageRepoAccount"]},
                {"PackageRepoContainer", Configuration.GetSection("Chimera")["PackageRepoContainer"]},
                {"ServiceBusNamespace", Configuration.GetSection("Chimera")["ServiceBusNamespace"]},
                {"SignDecryptRole", Configuration.GetSection("Chimera")["SignDecryptRole"]},
                {"VerifyEncryptRole", Configuration.GetSection("Chimera")["VerifyEncryptRole"]}
            });
            sp.AddSingleton(config);

            sp.RegisterCurrentCredentialFactory(Configuration.GetSection("AzureAd")["TenantId"],
                config["SubscriptionId"]);

            sp.AddAzurePlatformIntegrations();

            sp.AddSingleton<RuntimePeriodicActionService>();

            var provider = sp.BuildServiceProvider();

            var rpas = provider.GetService<RuntimePeriodicActionService>();
            await rpas.PerformPeriodicActions();
        }
    }
}