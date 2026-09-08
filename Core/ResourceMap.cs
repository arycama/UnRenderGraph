using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceMap
{
	private readonly Dictionary<Type, IRenderResource> resources = new();

	public void SetResource<T>(T resource) where T : IRenderResource
	{
		resources[typeof(T)] = resource;
	}

	public bool TryGetResource(Type type, out IRenderResource resource)
	{
		return resources.TryGetValue(type, out resource);
	}

	public bool TryGetResource<T>(out T resource)
	{
		var resourceExists = TryGetResource(typeof(T), out var temp);
		resource = resourceExists ? (T)temp : default;
		return resourceExists;
	}

	public T GetResource<T>()
	{
		if (TryGetResource<T>(out var resource))
		{
			return resource;
		}
		else
		{
			Debug.LogError($"Resource of type {typeof(T)} has not been set");
			return default;
		}
	}

	public void Clear()
	{
		resources.Clear();
	}
}
