using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Security.Permissions;

public static class NavigationVisibilityPrerequisites
{
	private static readonly Dictionary<string, string[][]> Paths = LoadPaths();
	private static readonly string[] Prefixes = Paths.Keys.OrderByDescending(prefix => prefix.Length).ToArray();
	private static readonly HashSet<string> VisibilityNames = new(
		ShikshaMitraModulePermissions.All.Concat(ShikshaMitraSubModulePermissions.All)
			.Concat(Paths.Values.SelectMany(paths => paths).SelectMany(path => path)), StringComparer.Ordinal);

	public static string[][] GetPaths(string permissionName)
	{
		if (VisibilityNames.Contains(permissionName)) return [];
		var prefix = Prefixes.FirstOrDefault(candidate => permissionName == candidate ||
			permissionName.StartsWith(candidate + ".", StringComparison.Ordinal));
		return prefix == null ? [] : Paths[prefix];
	}

	private static Dictionary<string, string[][]> LoadPaths()
	{
		using var resource = typeof(NavigationVisibilityPrerequisites).Assembly.GetManifestResourceStream(
			"ShikshaMitra.Security.Permissions.NavigationVisibilityPrerequisites.json")
			?? throw new InvalidOperationException("Navigation visibility prerequisites are unavailable.");
		return JsonSerializer.Deserialize<Dictionary<string, string[][]>>(resource)
			?? throw new InvalidOperationException("Navigation visibility prerequisites are invalid.");
	}
}
