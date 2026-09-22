using System;
using System.Diagnostics;

[DebuggerDisplay("id({propertyId}), resource({resourceIndex}), writes({firstWriteIndex}:{lastWriteIndex}), lastRead({lastReadIndex})")]
public struct ResourceInfo
{
	public readonly int descriptorIndex;
	public bool isExternal;
	public int resourceIndex;
	public readonly Range firstWriteIndexRange;
	public int lastWriteIndex;
	public int lastReadIndex;
	public int propertyId;
	public readonly ResourceHandleType type;
	public bool isPersistent;

	public ResourceInfo(int descriptorIndex, int propertyId, Range firstWriteIndexRange, ResourceHandleType type, bool isPersistent)
	{
		this.descriptorIndex = descriptorIndex;
		this.propertyId = propertyId;
		this.type = type;
		this.firstWriteIndexRange = firstWriteIndexRange;
		this.isPersistent = isPersistent;
		isExternal = false;
		resourceIndex = -1;
		lastWriteIndex = -1;
		lastReadIndex = -1;
	}
}
