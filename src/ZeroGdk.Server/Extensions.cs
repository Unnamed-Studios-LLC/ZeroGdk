using Arch.Core.External;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.InteropServices;
using ZeroGdk.Client;
using ZeroGdk.Server.HostedServices;
using ZeroGdk.Server.Managers;
using ZeroGdk.Server.Network;
using ZeroGdk.Server.Options;
using ZeroGdk.Server.Queues;
using ZeroGdk.Server.Services;
using ZeroGdk.Server.Timing;

namespace ZeroGdk.Server
{
	public static class Extensions
	{
		private const string LogHeaderV3 =
@"
   ███████╗ ███████╗ ██████╗   ██████╗ 
   ╚════██║ ██╔════╝ ██╔══██╗ ██╔═══██╗
     ███╔═╝ █████╗   ██████╔╝ ██║   ██║
   ██╔══╝   ██╔══╝   ██╔══██╗ ██║   ██║
   ███████╗ ███████╗ ██║  ██║ ╚██████╔╝
   ╚══════╝ ╚══════╝ ╚═╝  ╚═╝  ╚═════╝  v3
";

		/// <summary>
		/// Registers ZeroGDK services and configuration into the application’s dependency injection container.
		/// </summary>
		/// <param name="builder">The <see cref="WebApplicationBuilder"/> to add services to.</param>
		/// <param name="dataBuilderAction">Optional callback to configure and register data types via a <see cref="DataBuilder"/>.</param>
		public static void AddZeroGdk(this WebApplicationBuilder builder, Action<DataBuilder>? dataBuilderAction = null, Assembly? gameAssembly = null)
		{
			//== Register

			// add options
			builder.Services.Configure<ClientOptions>(builder.Configuration.GetSection("Client"));
			builder.Services.Configure<ClientFactoryOptions>(builder.Configuration.GetSection("ClientFactory"));
			builder.Services.Configure<EntitiesOptions>(builder.Configuration.GetSection("Entities"));
			builder.Services.Configure<NetworkOptions>(builder.Configuration.GetSection("Network"));
			builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Server"));
			builder.Services.Configure<TimingOptions>(builder.Configuration.GetSection("Timing"));
			builder.Services.Configure<WorldFactoryOptions>(builder.Configuration.GetSection("WorldFactory"));
			builder.ConfigureStartupOptions();
			builder.Services.AddSingleton<ExternalOptions>();

			// add timing
			builder.Services.AddTransient<ITicker, DefaultTicker>();

			// add managers
			builder.Services.AddSingleton<ClientManager>();
			builder.Services.AddSingleton<NetworkManager>();
			builder.Services.AddSingleton<WorldManager>();
			builder.Services.AddSingleton<ViewManager>();

			// add factories
			builder.Services.AddFactories<Client>(gameAssembly);
			builder.Services.AddFactories<World>(gameAssembly);

			// add data
			var dataBuilder = new DataBuilder();
			dataBuilderAction?.Invoke(dataBuilder);
			builder.Services.AddSingleton(dataBuilder.Build());

			// add network
			builder.Services.AddSingleton<NetworkKeyStore>();
			builder.Services.AddSingleton<TcpNetworkValidator>();

			// add expire
			builder.Services.AddSingleton<IExpire>(x => x.GetRequiredService<NetworkKeyStore>());
			builder.Services.AddSingleton<IExpire>(x => x.GetRequiredService<TcpNetworkValidator>());

			// add queues
			builder.Services.AddSingleton<ClientQueue>();
			builder.Services.AddSingleton<WorldQueue>();
			builder.Services.AddSingleton<FirewallQueue>();

			// add hosted services
			if (builder.Environment.IsProduction() &&
				RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				builder.Services.AddSingleton<LinuxFirewallProcessor>();
			}
			else
			{
				builder.Services.AddSingleton<DummyFirewallProcessor>();
			}
			builder.Services.AddHostedService<ClientFactoryProcessor>();
			builder.Services.AddHostedService<WorldFactoryProcessor>();
			builder.Services.AddHostedService<ExpirationProcessor>();
			builder.Services.AddHostedService<GameServer>();
			builder.Services.AddHostedService<TcpNetworkListener>();
			builder.Services.AddHostedService<StartupService>();

			// add console formatting
			builder.Logging.ClearProviders();
			builder.Logging.SetMinimumLevel(builder.Environment.IsProduction() ? LogLevel.Information : LogLevel.Trace);
			builder.Logging.AddConsole(options => options.FormatterName = nameof(ZeroConsoleFormatter));
			builder.Logging.AddConsoleFormatter<ZeroConsoleFormatter, ConsoleFormatterOptions>();

			// gRPC
			builder.Services.AddGrpc();
		}

		/// <summary>
		/// Configures and maps ZeroGDK middleware, services, and endpoints into the application pipeline.
		/// </summary>
		/// <param name="app">The <see cref="WebApplication"/> to configure.</param>
		public static void UseZeroGdk(this WebApplication app, Assembly? gameAssembly = null)
		{
			// map gRPC
			app.MapGrpcService<WorkerClientService>();
			app.MapGrpcService<WorkerWorldService>();

			// map factories
			app.MapFactories<Client>(gameAssembly);
			app.MapFactories<World>(gameAssembly);

			var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>();
			if (serverOptions.Value.LogHeader)
			{
				Console.WriteLine(LogHeaderV3);
			}
		}

		public static void AddStartupWorld(this WebApplicationBuilder builder, CreateWorldRequest request)
		{
			builder.Services.Configure<StartupOptions>(options => options.Worlds.Add(request));
		}
	}
}
