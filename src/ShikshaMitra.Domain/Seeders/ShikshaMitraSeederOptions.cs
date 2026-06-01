using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.Seeders;

public sealed class ShikshaMitraSeederOptions
{
	private const string EnabledKey = "ShikshaMitra:Seeders:Enabled";
	private const string ProfileKey = "ShikshaMitra:Seeders:Profile";
	private const string ForceRerunKey = "ShikshaMitra:Seeders:ForceRerun";
	private const string UseMarkersKey = "ShikshaMitra:Seeders:UseCompletionMarkers";

	public bool Enabled { get; init; }
	public bool ForceRerun { get; init; }
	public bool UseCompletionMarkers { get; init; }
	public string Profile { get; init; } = ShikshaMitraSeederProfile.FullDemo;

	public static ShikshaMitraSeederOptions FromConfiguration(IConfiguration configuration) =>
		new()
		{
			Enabled = configuration.GetValue(EnabledKey, true),
			ForceRerun = configuration.GetValue(ForceRerunKey, false),
			UseCompletionMarkers = configuration.GetValue(UseMarkersKey, true),
			Profile = ShikshaMitraSeederProfile.Normalize(configuration[ProfileKey])
		};
}
