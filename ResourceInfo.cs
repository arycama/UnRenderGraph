using System;
using System.Diagnostics;

[DebuggerDisplay("id({propertyId}), resource({resourceIndex}), writes({firstWriteIndex}:{lastWriteIndex}), lastRead({lastReadIndex})")]
public struct ResourceInfo
{
	public int descriptorIndex;
	public bool isExternal;
	public int resourceIndex;
	public Range firstWriteIndexRange;
	public int lastWriteIndex;
	public int lastReadIndex;
	public int propertyId;
	public ResourceHandleType type;

	public ResourceInfo(int descriptorIndex, int propertyId, ResourceHandleType type)
	{
		this.descriptorIndex = descriptorIndex;
		this.propertyId = propertyId;
		this.type = type;
		isExternal = false;
		resourceIndex = -1;
		firstWriteIndexRange = default;
		lastWriteIndex = -1;
		lastReadIndex = -1;
	}
}
