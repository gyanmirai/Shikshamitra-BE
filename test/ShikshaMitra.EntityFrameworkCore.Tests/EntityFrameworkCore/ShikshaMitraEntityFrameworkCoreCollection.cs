using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

[CollectionDefinition(ShikshaMitraTestConsts.CollectionDefinitionName)]
public class ShikshaMitraEntityFrameworkCoreCollection : ICollectionFixture<ShikshaMitraEntityFrameworkCoreFixture>
{

}
