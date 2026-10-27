using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees;

// Only this contract test uses this fixture. The shared HTTP fixture gives every DbContext
// the same SQLite connection, which cannot run the invitation's nested requiresNew UOW.
// Match the Application test harness; PostgreSQL transaction acceptance remains separate.
public class EmployeeWorkAssignmentApiTestFactory : ShikshaMitraApiTestFactory
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services => services.AddAlwaysDisableUnitOfWorkTransaction());
	}
}

public class EmployeeWorkAssignmentApi_Tests(EmployeeWorkAssignmentApiTestFactory factory)
	: IClassFixture<EmployeeWorkAssignmentApiTestFactory>
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Invite_preserves_multiple_organization_ids_and_server_labels_through_public_http_read()
	{
		await new AcademicOrganizationOptionsApi_Tests(factory).AssertWorkAssignmentRoundTripAsync();
	}
}
