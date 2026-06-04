using System;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Wrapper for Guid to make it a reference type for caching.
/// </summary>
[Serializable]
public class GuidCacheItem
{
	public Guid Value { get; set; }

	public GuidCacheItem() { }

	public GuidCacheItem(Guid value) => Value = value;
}
