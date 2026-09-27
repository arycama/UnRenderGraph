using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class PersistentRtHandleCache
{
	private readonly Dictionary<Camera, RtHandle> textureCache = new();
	private readonly RenderGraph renderGraph;
	private readonly GraphicsFormat format;
	private readonly int propertyId;
	private readonly bool hasMips;
	private readonly bool clear;
	private readonly Color clearColor;
	private readonly float clearDepth;
	private readonly uint clearStencil;
	private readonly TextureDimension dimension;
	private readonly bool autoGenerateMips;

	public PersistentRtHandleCache(RenderGraph renderGraph, GraphicsFormat format, int propertyId, bool clear = false, Color clearColor = default, float clearDepth = 1f, uint clearStencil = default, TextureDimension dimension = TextureDimension.Tex2D, bool hasMips = false, bool autoGenerateMips = false)
	{
		this.renderGraph = renderGraph;
		this.format = format;
		this.propertyId = propertyId;
		this.hasMips = hasMips;
		this.clear = clear;
		this.clearColor = clearColor;
		this.clearDepth = clearDepth;
		this.clearStencil = clearStencil;
		this.dimension = dimension;
		this.autoGenerateMips = autoGenerateMips;
	}

	public (RtHandle current, RtHandle history, bool hasHistory) GetTextures(ViewHandle viewHandle, Camera camera, PassBuilder pass)
	{
		var hasHistory = textureCache.TryGetValue(camera, out var history);
		if (hasHistory)
			pass.ReleasePersistentResource(history);

		var current = renderGraph.GetTexture(new(viewHandle, format, clear, clearColor, clearDepth, clearStencil, dimension, hasMips, autoGenerateMips), propertyId, true);
		textureCache[camera] = current;

		return (current, history, hasHistory);
	}
}
