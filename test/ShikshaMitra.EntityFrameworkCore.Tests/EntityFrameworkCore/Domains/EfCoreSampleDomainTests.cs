using ShikshaMitra.Samples;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Domains;

[Collection(ShikshaMitraTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<ShikshaMitraEntityFrameworkCoreTestModule>
{

}
