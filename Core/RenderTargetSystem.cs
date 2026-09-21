using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class RenderTargetSystem : IDisposable
{
	private readonly List<RenderTargetIdentifier> renderTargets = new();
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


	public int ExportTarget(RenderTargetIdentifier id)
	{
		var index = renderTargets.Count;
		renderTargets.Add(id);
		resourceIndices.Add(-1);
		return index;
	}

	public int AllocateTarget(RenderTargetDescriptor descriptor, ViewInfo viewInfo, int samples, bool isUav)
	{
		var rtDescriptor = descriptor.GetRenderTextureDescriptor(viewInfo, samples, isUav);

		var resourceIndex = -1;
		RenderTexture resource = null;
		for (var i = 0; i < availableResources.Count; i++)
		{
			var targetIndex = availableResources[i];
			var target = resources[targetIndex];

			if (target.graphicsFormat != rtDescriptor.graphicsFormat || target.depthStencilFormat != rtDescriptor.depthStencilFormat || target.stencilFormat != rtDescriptor.stencilFormat)
				continue;

			if (target.dimension != rtDescriptor.dimension)
				continue;

			if (target.width != rtDescriptor.width || target.height != rtDescriptor.height || target.volumeDepth != rtDescriptor.volumeDepth)
				continue;

			if (target.enableRandomWrite != rtDescriptor.enableRandomWrite || target.antiAliasing != rtDescriptor.msaaSamples || target.bindTextureMS != rtDescriptor.bindMS)
				continue;

			resource = target;
			resourceIndex = targetIndex;
			availableResources.RemoveAt(i);
			break;
		}

		if (resource == null)
		{
			resource = new RenderTexture(rtDescriptor) { hideFlags = HideFlags.HideAndDontSave, name = $"{resources.Count} {rtDescriptor.width}x{rtDescriptor.height}x{rtDescriptor.volumeDepth} {descriptor.format} {descriptor.dimension} aa:{rtDescriptor.msaaSamples}" };
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
		resourceIndices.Clear();
	}
}
