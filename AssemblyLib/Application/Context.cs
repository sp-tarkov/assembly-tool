using System.Collections.Concurrent;

namespace AssemblyLib.Application;

internal sealed class Context
{
	private readonly ConcurrentDictionary<Type, object> _contexts = [];
	
	public static Context Instance
	{
		get
		{
			return _instance ??= new Context();
		}
	}
	private static Context? _instance;
	
	public bool RegisterComponent<T>(object instance) where T : IComponent
	{
		return _contexts.TryAdd(typeof(T), instance);
	}
	
	public T? Get<T>() where T : IComponent
	{
		if (_contexts.TryGetValue(typeof(T), out var instance))
		{
			return (T)instance;
		}
		
		return default;
	}
}