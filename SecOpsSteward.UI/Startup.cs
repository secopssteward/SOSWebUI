using System;
using System.Collections.Generic;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using SecOpsSteward.Data;
using SecOpsSteward.Integrations.Azure;
using SecOpsSteward.Plugins.Azure;
using SecOpsSteward.Shared;
using SecOpsSteward.Shared.NonceTracking;
using SecOpsSteward.Shared.Roles;

namespace SecOpsSteward.UI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Instance = this;
            Configuration = configuration;
        }

        public static Startup Instance { get; private set; }
        public IConfiguration Configuration { get; }

        // ---

        /// <summary>
        ///     If this is the first time the app is being run
        /// </summary>
        public static bool FirstRun { get; set; } = false;

        /// <summary>
        ///     If the App is running in Demo mode. This is shorthand for disabling all of the below.
        /// </summary>
        public static bool RunDemoMode => Instance.Configuration.GetValue("RunDemoMode", false);

        /// <summary>
        ///     If authentication is configured; if not, no AuthX will be integrated. The app will not have user awareness for any
        ///     features.
        /// </summary>
        public static bool HasAuthConfiguration =>
            !RunDemoMode && Instance.Configuration.GetSection("AzureAd").Exists();

        /// <summary>
        ///     If Service Integrations are to be used, or if dummy/test Integrations are used instead
        /// </summary>
        public static bool UseDummyServices => RunDemoMode || Instance.Configuration.GetValue<bool>("UseDummyServices");

        /// <summary>
        ///     If Auto-Discovery/Resource Explorers are enabled
        /// </summary>
        public static bool LockDiscovery => RunDemoMode || Instance.Configuration.GetValue("DisableDiscovery", false);

        /// <summary>
        ///     If individual Package-based permissions for Users and Agents are enabled
        /// </summary>
        public static bool IgnoreUserPermissionRestrictions => Instance.Configuration.GetValue("IgnoreUserPermissionRestrictions", false);

        // ---

        private void RegisterAuthXServices(IServiceCollection services)
        {
            // Register authentication/authorization services and consent handler
            if (HasAuthConfiguration)
            {
                services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"))
                    .EnableTokenAcquisitionToCallDownstreamApi()
                    .AddDownstreamWebApi("AzureRM", co =>
                    {
                        co.BaseUrl = "https://management.azure.com/";
                        co.Scopes = "https://management.azure.com//user_impersonation";
                    })
                    .AddDownstreamWebApi("Graph", co =>
                    {
                        co.BaseUrl = "https://graph.windows.net/";
                        co.Scopes = "https://graph.windows.net//user.read";
                    })
                    .AddDownstreamWebApi("KeyVault", co =>
                    {
                        co.BaseUrl = "https://vault.azure.net/";
                        co.Scopes = "https://vault.azure.net//user_impersonation";
                    })
                    .AddDownstreamWebApi("ServiceBus", co =>
                    {
                        co.BaseUrl = "https://servicebus.azure.net/";
                        co.Scopes = "https://servicebus.azure.net//user_impersonation";
                    })
                    .AddInMemoryTokenCaches();
                services.AddControllersWithViews()
                    .AddMicrosoftIdentityUI();

                services.AddAuthorization(options =>
                {
                    // By default, all incoming requests will be authorized according to the default policy
                    options.FallbackPolicy = options.DefaultPolicy;
                });

                services.AddServerSideBlazor()
                    .AddMicrosoftIdentityConsentHandler();
            }
            else
            {
                // Skip all AuthX, no user integrations
                services.AddServerSideBlazor();
            }

            // Register TokenOwner for user information, avatar, etc
            services.AddScoped(s => TokenOwner.Create(
                s.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync().Result,
                HasAuthConfiguration));
        }

        private ChimeraServiceConfigurator RegisterChimeraServices(IServiceCollection services)
        {
            // Map config based on Chimera section (all values translate)
            var config = new ChimeraServiceConfigurator(new Dictionary<string, string>());
            foreach (var a in Configuration.GetSection("Chimera").GetChildren()) config[a.Key] = a.Value;

            // Add service integrations (storage, messaging, vault, etc)
            if (!UseDummyServices)
                services.AddAzurePlatformIntegrations();
            else
                services.AddChimeraDummyIntegrations();


            // If running locally, this factory is registered to use managed identity (provided by the environment)
            services.RegisterCurrentCredentialFactory(Configuration.GetSection("AzureAd")["TenantId"],
                config["SubscriptionId"], false, UseDummyServices);

            // Chimera core ( + public packages)
            services.AddChimeraWithPublicPackageRepository(config, Configuration.GetConnectionString("PublicPackages"));

            // Bindings to Chimera API and data repo
            services.AddTransient<DataBoundApi>();

            // Message processing (as user)
            services.AddScoped<WorkflowMessageProcessorService>();

            // This is for executions local to the _web server_ ... don't bother with this (for now)
            services.AddScoped<INonceTrackingService, NoNonceTrackingService>();

            return config;
        }

        private void RegisterDatabaseServices(IServiceCollection services)
        {
            // Adds the DbContext
            services.AddDbContextFactory<SecOpsStewardDbContext>(options =>
            {
                // TODO: Disable in production
                options.EnableDetailedErrors();

                // If using dummy integrations, fall back to SQLite
                if (UseDummyServices)
                    options.UseSqlite("Data Source=sos.db")
                        .EnableSensitiveDataLogging(); // TODO: Disable

                // Try SQL Server
                else if (Configuration.GetConnectionString("Database") != null)
                    options.UseSqlServer(Configuration.GetConnectionString("Database"));
                else if (Environment.GetEnvironmentVariable("SQLAZURECONNSTR_Database") != null)
                    options.UseSqlServer(Environment.GetEnvironmentVariable("SQLAZURECONNSTR_Database"));

                else throw new Exception("No database configuration specified!");
            });

            // Add convenience method for creating transient DB connections
            services.AddTransient(p =>
                p.GetRequiredService<IDbContextFactory<SecOpsStewardDbContext>>().CreateDbContext());
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Register logger
            services.AddLogging(c => { c.AddConsole().AddAnsiConsoleFormatter(); });

            // Register AuthX services
            RegisterAuthXServices(services);

            // Database
            RegisterDatabaseServices(services);

            // Register Chimera services
            var chimeraConfig = RegisterChimeraServices(services);

            // Set up Razor components
            services.AddRazorPages();
            services.AddMudServices();
            services.AddBlazoredSessionStorage();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            if (HasAuthConfiguration) // other place which adds AuthX
            {
                app.UseAuthentication();
                app.UseAuthorization();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}