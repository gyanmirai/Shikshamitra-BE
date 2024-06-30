using ShikshaMitra.Samples;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Applications;

[Collection(ShikshaMitraTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<ShikshaMitraEntityFrameworkCoreTestModule>
{

}
