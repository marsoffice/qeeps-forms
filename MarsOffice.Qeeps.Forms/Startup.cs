using System;
using System.IO;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(MarsOffice.Qeeps.Forms.Startup))]
namespace MarsOffice.Qeeps.Forms
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            FunctionsHostBuilderContext context = builder.GetContext();
            var env = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";
            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{env}.json"), optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = builder.GetContext().Configuration;
            builder.Services.AddHttpClient("access", (svc, hc) =>
            {
                hc.BaseAddress = new Uri(config["access_url"]);
                hc.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", new [] {$"Bearer {GetToken(config)}"});
            });
        }

        private string GetToken(IConfiguration config)
        {
            TokenCredential tokenCredential;
            var envVar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var isDevelopmentEnvironment = string.IsNullOrEmpty(envVar) || envVar.ToLower() == "development";

            if (isDevelopmentEnvironment)
            {
                tokenCredential = new AzureCliCredential();
            }
            else
            {
                tokenCredential = new DefaultAzureCredential();
            }

            return tokenCredential.GetToken(
                new TokenRequestContext(scopes: new string[] { config["scope"] }),
                cancellationToken: System.Threading.CancellationToken.None
            ).Token;
        }
    }
}