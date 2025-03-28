using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ZeroGdk.Server.Factories;
using ZeroGdk.Server.Routing;

namespace ZeroGdk.Server
{
	internal static class FactoryExtensions
	{
		public static void AddFactories<T>(this IServiceCollection services, Assembly? gameAssembly = null)
		{
			services.AddSingleton<RouteResolver<T>>();
			services.AddSingleton<FactoryExecuter<T>>();

			// get entry assembly (implementation)
			var assembly = gameAssembly ?? Assembly.GetEntryAssembly();
			if (assembly == null)
			{
				return;
			}

			// get factory types
			var types = assembly.GetTypes().WhereFactoryTypes<T>();
			foreach (var type in types)
			{
				services.AddScoped(type);
			}
		}

		public static Dictionary<string, Type> GetFactoryDataTypeMap<T>(this Assembly? assembly)
		{
			ArgumentNullException.ThrowIfNull(assembly);

			var map = new Dictionary<string, Type>();

			// get factory types
			var baseType = typeof(Factory<,>);
			var types = assembly.GetTypes().WhereFactoryTypes<T>();
			foreach (var type in types)
			{
				if (!IsSubclassOfGeneric(baseType, type, out var builtGeneric))
				{
					continue;
				}

				var dataType = builtGeneric.GenericTypeArguments[1];
				var routes = type.GetCustomAttributes<RouteAttribute>();
				foreach (var route in routes)
				{
					if (route == null)
					{
						continue;
					}
					
					map[route.Path] = dataType;
				}
			}

			return map;
		}

		public static void MapFactories<T>(this WebApplication app, Assembly? gameAssembly = null)
		{
			var resolver = app.Services.GetRequiredService<RouteResolver<T>>();

			// get entry assembly (implementation)
			var assembly = gameAssembly ?? Assembly.GetEntryAssembly();
			if (assembly == null)
			{
				return;
			}

			// get factory types
			var types = assembly.GetTypes().WhereFactoryTypes<T>();
			foreach (var type in types)
			{
				var routes = type.GetCustomAttributes<RouteAttribute>();
				foreach (var route in routes)
				{
					if (route == null)
					{
						continue;
					}

					resolver.MapRoute(route.Path, type);
				}
			}
		}

		private static bool IsSubclassOfGeneric(Type generic, Type? toCheck, [MaybeNullWhen(false)] out Type builtGenericType)
		{
			while (toCheck != null && toCheck != typeof(object))
			{
				if (toCheck.IsGenericType)
				{
					if (toCheck.GetGenericTypeDefinition() == generic)
					{
						builtGenericType = toCheck;
						return true;
					}
				}
				toCheck = toCheck.BaseType;
			}
			builtGenericType = null;
			return false;
		}

		private static IEnumerable<Type> WhereFactoryTypes<T>(this IEnumerable<Type> types)
		{
			return types.Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i == typeof(IFactory<T>)));
		}
	}
}
