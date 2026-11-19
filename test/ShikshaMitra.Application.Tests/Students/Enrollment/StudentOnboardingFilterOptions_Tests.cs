using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_filter_options_uses_scoped_scalar_academic_lookups() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", accountsEnabled: false, emailEnabled: false);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var context = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			context.ChangeTracker.Clear();
			var options = await service.GetFilterOptionsAsync(fixture.Case.AcademicYearId);
			options.AcademicYears.Select(item => item.Id).ShouldContain(fixture.Case.AcademicYearId);
			options.Boards.Select(item => item.Id).ShouldContain(fixture.Offering.BoardId);
			options.Mediums.Single(item => item.Id == fixture.Offering.MediumId).ParentId.ShouldBe(fixture.Offering.BoardId);
			options.Standards.Single(item => item.Id == fixture.Offering.StandardId).ParentId.ShouldBe(fixture.Offering.MediumId);
			options.Divisions.Single(item => item.Id == fixture.Case.DivisionId).ParentId.ShouldBe(fixture.Offering.StandardId);
			context.ChangeTracker.Entries<EducationBoard>().ShouldBeEmpty();
			context.ChangeTracker.Entries<Standard>().ShouldBeEmpty();
		});
		var absent = await service.GetFilterOptionsAsync(Guid.NewGuid());
		absent.AcademicYears.ShouldBeEmpty(); absent.Boards.ShouldBeEmpty(); absent.Mediums.ShouldBeEmpty();
		absent.Standards.ShouldBeEmpty(); absent.Divisions.ShouldBeEmpty();
	});
}
