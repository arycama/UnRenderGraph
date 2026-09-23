using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct RtHandle : IEquatable<RtHandle>
{
	public readonly int index;
	public readonly int propertyId;
	public readonly bool isPersistent;

	public RtHandle(int index, int propertyId, bool isPersistent)
	{
		this.index = index;
		this.propertyId = propertyId;
		this.isPersistent = isPersistent;
	}

	public override bool Equals(object obj)
	{
		return obj is RtHandle handle && Equals(handle);
	}

	public bool Equals(RtHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(RtHandle left, RtHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(RtHandle left, RtHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(RtHandle handle) => handle.index;

	public static implicit operator ResourceHandle(RtHandle handle) => new(handle.index, ResourceHandleType.RenderTarget, handle.propertyId, handle.isPersistent);
}

