using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library;

internal static class LibraryChargeGuardFixture
{
	public static (LibraryChargeAppService Service, IRepository<LibraryCharge, Guid> Repository) Create(LibraryCharge charge)
	{
		var tenantId = charge.TenantId ?? throw new InvalidOperationException("Charge guard tests require an actual school identifier.");
		var repository = Substitute.For<IRepository<LibraryCharge, Guid>>();
		repository.FindAsync(charge.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(charge);
		repository.GetQueryableAsync().Returns(new[] { charge }.AsQueryable());
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		var access = Substitute.For<ILibraryAccessService>();
		access.GetContextAsync(Arg.Any<Guid?>()).Returns(new LibraryAcademicContext(
			new UserScopeContext { TenantId = tenantId, UserId = Guid.NewGuid() },
			charge.AcademicYearId.HasValue ? [charge.AcademicYearId.Value] : []));
		// These unit tests pin mutation guards AFTER an authorized, school-bound
		// single-source read. Actual identity/grant/year/recorded-source boundaries
		// are independently exercised by LibraryStaffRecordedScope integration tests.
		var sourceScope = Substitute.For<IFinanceExternalSourceScope>();
		sourceScope.GetReadableSourcesAsync(FinanceExternalSourceKind.LibraryAssessment, Arg.Any<CancellationToken>())
			.Returns(new[] { new FinanceExternalReadableSource { SourceId = charge.Id, AcademicYearId = charge.AcademicYearId } }.AsQueryable());
		var provider = new ServiceCollection()
			.AddSingleton(sourceScope)
			.AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter([]))
			.BuildServiceProvider();
		var service = new LibraryChargeAppService(repository, null!, null!, null!, null!, null!, null!, null!, null!,
			access, Substitute.For<ILibraryInventoryLock>(), tenant)
		{
			LazyServiceProvider = new AbpLazyServiceProvider(provider)
		};
		return (service, repository);
	}
}
