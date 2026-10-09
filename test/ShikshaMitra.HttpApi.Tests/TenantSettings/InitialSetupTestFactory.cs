using Xunit;

namespace ShikshaMitra.TenantSettings;

// Setup must work for a clean school, with no dependency on demo academics or seeded identities.
public class InitialSetupTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
}

[CollectionDefinition(Name)]
public class InitialSetupTestCollection : ICollectionFixture<InitialSetupTestFactory>
{
	public const string Name = "Initial setup clean-school API";
}
