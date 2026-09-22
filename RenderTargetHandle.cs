using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct RenderTargetHandle : IEquatable<RenderTargetHandle>
{
	public readonly int index;
	public readonly int propertyId;
	public readonly bool isPersistent;

	public RenderTargetHandle(int index, int propertyId, bool isPersistent)
	{
		this.index = index;
		this.propertyId = propertyId;
		this.isPersistent = isPersistent;
	}

	public override bool Equals(object obj)
	{
		return obj is RenderTargetHandle handle && Equals(handle);
	}

	public bool Equals(RenderTargetHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(RenderTargetHandle left, RenderTargetHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(RenderTargetHandle left, RenderTargetHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(RenderTargetHandle handle) => handle.index;

	public static implicit operator ResourceHandle(RenderTargetHandle handle) => new(handle.index, ResourceHandleType.RenderTarget, handle.propertyId, handle.isPersistent);
}

