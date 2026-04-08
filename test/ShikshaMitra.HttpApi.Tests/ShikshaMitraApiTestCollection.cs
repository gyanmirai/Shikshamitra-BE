using Xunit;

namespace ShikshaMitra;

/// <summary>
/// xUnit collection definition that shares a single <see cref="ShikshaMitraApiTestFactory"/>
/// instance across all in-process API test classes.
/// Apply [Collection(ApiTestCollection.Name)] to each test class.
/// </summary>
[CollectionDefinition(Name)]
public class ApiTestCollection : ICollectionFixture<ShikshaMitraApiTestFactory>
{
	public const string Name = "ShikshaMitra API";
}
