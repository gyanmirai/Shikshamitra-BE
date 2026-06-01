using System;
using System.Collections.Generic;

namespace ShikshaMitra.Seeders;

public static class ShikshaMitraSeederProfile
{
	public const string FullDemo = "FullDemo";
	public const string FastDemo = "FastDemo";
	public const string Minimal = "Minimal";
	public const string ContextPropertyName = "ShikshaMitra:Seeders:Profile";
	public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		FullDemo,
		FastDemo,
		Minimal
	};

	public static readonly IReadOnlySet<string> Demo = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		FullDemo,
		FastDemo
	};

	public static string Normalize(string? profile)
	{
		if (string.Equals(profile, FastDemo, System.StringComparison.OrdinalIgnoreCase))
		{
			return FastDemo;
		}

		if (string.Equals(profile, Minimal, System.StringComparison.OrdinalIgnoreCase))
		{
			return Minimal;
		}

		return FullDemo;
	}
}
