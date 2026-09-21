using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class RenderTargetSystem : IDisposable
{
	private readonly List<RenderTargetIdentifier> renderTargets = new();
	private readonly List<RenderTargetDescriptor> descriptors = new();
	private readonly List<RenderTexture> resources = new();
	private readonly List<int> availableResources = new();
	private readonly List<int> resourceIndices = new();

	public void Dispose()
	{
		foreach (var target in resources)
		{
			// can be null due to renderdoc loading..
			if (target == null)
				continue;

			if (target.IsCreated())
				target.Release();

			Object.DestroyImmediate(target);
		}
	}

	public RenderTargetIdentifier GetTexture(int index)
	{
		return renderTargets[index];
	}

	public RenderTargetDescriptor GetDescriptor(int index)
	{
		return descriptors[index];
	}

	public int ExportTarget(RenderTargetIdentifier id)
	{
		var index = renderTargets.Count;
		renderTargets.Add(id);
		resourceIndices.Add(-1);
		return index;
	}

	public int AddDescriptor(RenderTargetDescriptor descriptor)
	{
		var descriptorIndex = descriptors.Count;
		descriptors.Add(descriptor);
		return descriptorIndex;
	}

	public int AllocateTarget(int descriptorIndex, ViewInfo viewInfo, int samples, bool isUav)
	{
		var baseDescriptor = descriptors[descriptorIndex];
		var descriptor = baseDescriptor.GetRenderTextureDescriptor(viewInfo, samples, isUav);

		var resourceIndex = -1;
		RenderTexture resource = null;
		for (var i = 0; i < availableResources.Count; i++)
		{
			var targetIndex = availableResources[i];
			var target = resources[targetIndex];

			if (target.graphicsFormat != descriptor.graphicsFormat || target.depthStencilFormat != descriptor.depthStencilFormat || target.stencilFormat != descriptor.stencilFormat)
				continue;

			if (target.dimension != descriptor.dimension)
				continue;

			if (target.width != descriptor.width || target.height != descriptor.height || target.volumeDepth != descriptor.volumeDepth)
				continue;

			if (target.enableRandomWrite != descriptor.enableRandomWrite || target.antiAliasing != descriptor.msaaSamples || target.bindTextureMS != descriptor.bindMS)
				continue;

			resource = target;
			resourceIndex = targetIndex;
			availableResources.RemoveAt(i);
			break;
		}

		if (resource == null)
		{
			resource = new RenderTexture(descriptor) { hideFlags = HideFlags.HideAndDontSave, name = $"{resources.Count} {descriptor.width}x{descriptor.height}x{descriptor.volumeDepth} {baseDescriptor.format} {baseDescriptor.dimension} aa:{descriptor.msaaSamples}" };
			_ = resource.Create();
			resourceIndex = resources.Count;
			resources.Add(resource);
		}

		var index = renderTargets.Count;
		renderTargets.Add(resource);
		resourceIndices.Add(resourceIndex);
		return index;
	}

	public void ReleaseResource(int resourceIndex)
	{
		availableResources.Add(resourceIndices[resourceIndex]);
	}

	public void FreeUnreleasedResources()
	{
		renderTargets.Clear();
		descriptors.Clear();
		resourceIndices.Clear();
	}
}
