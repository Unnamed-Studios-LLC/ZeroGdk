using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ZeroGdk.Server.Options;

namespace ZeroGdk.Server
{
	internal static class OptionsExtensions
	{
		public static void ConfigureStartupOptions(this WebApplicationBuilder builder)
		{
			builder.Services.Configure<StartupOptions>(options =>
			{
				var startup = builder.Configuration.GetSection("Startup");
				startup.Bind(options);

				var dataTypeMap = Assembly.GetEntryAssembly().GetFactoryDataTypeMap<World>();
				var worldsSection = startup.GetSection("Worlds");
				int i = 0;
				foreach (var world in worldsSection.GetChildren())
				{
					var createWorldRequest = options.Worlds[i];
					var route = world.GetSection("Route");
					var data = world.GetSection("Data");

					// get data type
					if (string.IsNullOrEmpty(route.Value) ||
						!dataTypeMap.TryGetValue(route.Value, out var dataType))
					{
						continue;
					}

					// create data instance
					if (Activator.CreateInstance(dataType) is not IMessage instance)
					{
						continue;
					}

					// map data values
					data.Bind(instance);
					createWorldRequest.Data = Any.Pack(instance);
				}
			});
		}
	}
}
