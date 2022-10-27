using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace GlanceReddit
{
    public class Program
	{
        public static void Main(string[] args)
		{
			var host = CreateHostBuilder(args).Build();

			host.Run();
		}

        public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.ConfigureKestrel(serverOptions =>
					{
						serverOptions.AddServerHeader = false;
					});
					
					webBuilder.UseStartup<Startup>();
				});
	}
}
