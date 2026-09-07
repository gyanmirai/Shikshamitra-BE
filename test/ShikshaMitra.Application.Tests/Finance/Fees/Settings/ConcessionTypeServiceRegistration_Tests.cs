using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class ConcessionTypeServiceRegistration_Tests :
	ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public void Service_Can_Be_Resolved_With_Audit_History_Dependencies()
	{
		var service = GetRequiredService<IConcessionTypeAppService>();

		Assert.NotNull(service);
	}
}
