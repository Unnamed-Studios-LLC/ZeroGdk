using System.Diagnostics.CodeAnalysis;

namespace ZeroGdk.Server.Routing
{
	internal sealed class RouteResolver<T>
	{
		private readonly Dictionary<string, Type> _mappedTypes = [];

		public void MapRoute(string route, Type type)
		{
			if (_mappedTypes.TryGetValue(route, out var existingType))
			{
				throw new RouteConflictException(route, existingType, type);
			}

			_mappedTypes.Add(route, type);
		}

		public bool TryResolve(string route, [MaybeNullWhen(false)] out Type factoryType)
		{
			return _mappedTypes.TryGetValue(route, out factoryType);
		}
	}
}
