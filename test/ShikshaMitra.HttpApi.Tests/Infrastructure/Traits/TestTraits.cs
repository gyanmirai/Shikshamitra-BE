namespace ShikshaMitra.Infrastructure.Traits;

/// <summary>
/// Canonical trait names used across all API test suites.
/// Filter with: dotnet test --filter "Tier=Critical" or "Module=HR"
/// </summary>
public static class TestTraits
{
	public const string Tier = nameof(Tier);
	public const string Module = nameof(Module);
	public const string AuthMode = nameof(AuthMode);
	public const string Destructive = nameof(Destructive);

	public static class Tiers
	{
		/// <summary>Must-pass for every PR/merge. Fast, deterministic, business-critical paths.</summary>
		public const string Critical = nameof(Critical);

		/// <summary>High-value non-critical; runs on main and optionally PR for touched modules.</summary>
		public const string Important = nameof(Important);

		/// <summary>Broad matrix, data-heavy, or destructive scenarios; scheduled/nightly/manual.</summary>
		public const string Extended = nameof(Extended);

		/// <summary>Hosted smoke tests against a running server with real auth tokens.</summary>
		public const string Smoke = nameof(Smoke);
	}

	public static class Modules
	{
		public const string HR = nameof(HR);
		public const string Students = nameof(Students);
		public const string Academic = nameof(Academic);
		public const string Finance = nameof(Finance);
		public const string Communication = nameof(Communication);
		public const string Library = nameof(Library);
		public const string Transport = nameof(Transport);
		public const string Dashboard = nameof(Dashboard);
		public const string TenantSettings = nameof(TenantSettings);
		public const string Security = nameof(Security);
	}

	public static class AuthModes
	{
		public const string UserPassword = nameof(UserPassword);
		public const string ClientCredentials = nameof(ClientCredentials);
		public const string Anonymous = nameof(Anonymous);
	}
}
