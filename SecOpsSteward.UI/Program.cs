using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace SecOpsSteward.UI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // NOTE THIS BLOWS AWAY SQLITE EVERY TIME
            if (File.Exists("sos.db")) File.Delete("sos.db");

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
        }
    }
}