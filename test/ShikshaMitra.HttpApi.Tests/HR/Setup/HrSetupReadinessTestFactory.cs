using Xunit;

namespace ShikshaMitra.HR.Setup;

public class HrSetupReadinessTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
}

[CollectionDefinition(Name)]
public class HrSetupReadinessTestCollection : ICollectionFixture<HrSetupReadinessTestFactory>
{
	public const string Name = "HR setup readiness clean-school API";
}
