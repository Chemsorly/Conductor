using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor_Server.Server
{
    public class SignalRStartup
    {

        public static Task Run(String pEndpoint)
        {
            return Task.Run(() =>
            {
                var host = new Microsoft.AspNetCore.Hosting.WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        //options.Limits.MaxRequestBodySize = null;
                        //options.Limits.MaxRequestBufferSize = null;
                    })
                    .UseIISIntegration()
                    .UseUrls(pEndpoint)
                    .UseStartup<SignalRStartup>()
                    .Build();

                host.Run();
            });
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR()
                .AddHubOptions<CommHub>(options =>
                {
                    options.EnableDetailedErrors = true;
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSignalR(routes =>
            {
                routes.MapHub<CommHub>("/signalr", options =>
                {
                    //1024 * 1024 * 1024 * 1024 = 1GB buffer size
                    //workaround for https://github.com/aspnet/SignalR/issues/2266
                    options.ApplicationMaxBufferSize = 1099511627776;
                });
            });

            //GlobalHost.Configuration.MaxIncomingWebSocketMessageSize = null;
            //app.Map("/signalr", map =>
            //{
            //    map.UseCors(CorsOptions.AllowAll);
            //    var hubConfiguration = new HubConfiguration
            //    {
            //        EnableDetailedErrors = true,
            //        EnableJSONP = true
            //    };
            //    GlobalHost.DependencyResolver = hubConfiguration.Resolver;
            //    GlobalHost.Configuration.MaxIncomingWebSocketMessageSize = null;
            //    GlobalHost.Configuration.DefaultMessageBufferSize = 1000;
            //    map.RunSignalR(hubConfiguration);
            //});
        }
    }
}
