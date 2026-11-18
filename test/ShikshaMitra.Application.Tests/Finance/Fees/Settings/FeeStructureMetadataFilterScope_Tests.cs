using ShikshaMitra.Domain.Finance.Fees.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Metadata_header_filters_translate_division_only_scope_without_granting_class_access(bool displayFilter)
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, standard, _, _) = await SeedStandardAsync();
		var scope = new UserScopeContext {
			UserId = TestUserId, TenantId = TestTenantId,
			AcademicYearIds = [year], DivisionIds = [Guid.NewGuid()]
		};
		var access = Substitute.For<IUserScopeAccessService>();
		access.GetCurrentUserScopeAsync().Returns(Task.FromResult(scope));
		var service = new FeeStructureMetadataAppService(
			_metadataRepo, GetRequiredService<IRepository<FeeStructure, Guid>>(),
			GetRequiredService<IAcademicYearRepository>(), GetRequiredService<IEducationBoardRepository>(),
			GetRequiredService<IStandardRepository>(), GetRequiredService<IGuidGenerator>(), access)
		{
			LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
		};
		var filters = displayFilter
			? """[{"column":"name","type":"equals","value":"Standard I fees"},{"column":"totalClassValue","type":"equals","value":"0/1"}]"""
			: """[{"column":"name","type":"equals","value":"Standard I fees"}]""";
		await WithUnitOfWorkAsync(async () => {
			var denied = await service.GetListAsync(year, filters: filters);
			denied.TotalCount.ShouldBe(0);
			denied.Items.ShouldBeEmpty();
			// A class grant is the positive control; a division grant alone cannot reveal its parent.
			scope.StandardIds.Add(standard);
			var allowed = await service.GetListAsync(year, filters: filters);
			allowed.TotalCount.ShouldBe(1);
			allowed.Items.Single().Classes.Single().Id.ShouldBe(standard);
		});
	}
}
