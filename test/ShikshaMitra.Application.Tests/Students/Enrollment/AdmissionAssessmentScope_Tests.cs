using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentScope_Tests
{
	[Fact]
	public async Task Narrow_standard_grant_does_not_expand_to_ancestor_board_or_other_academic_year()
	{
		var year = Guid.NewGuid();
		var board = Guid.NewGuid();
		var medium = Guid.NewGuid();
		var stage = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var cycle = new AdmissionCycle(Guid.NewGuid(), null, "Current", "CURRENT", year, DateTime.Today, DateTime.Today.AddDays(60));
		var otherCycle = new AdmissionCycle(Guid.NewGuid(), null, "Other", "OTHER", Guid.NewGuid(), DateTime.Today, DateTime.Today.AddDays(60));
		AdmissionOffering Offering(Guid cycleId, Guid standardId) => new(Guid.NewGuid(), null, cycleId, null,
			board, medium, stage, standardId, "TEST", true, null, null);
		var allowed = Offering(cycle.Id, standard);
		var otherStandard = Offering(cycle.Id, Guid.NewGuid());
		var otherYear = Offering(otherCycle.Id, standard);
		var offeringRepository = Substitute.For<IRepository<AdmissionOffering, Guid>>();
		offeringRepository.GetQueryableAsync().Returns(new[] { allowed, otherStandard, otherYear }.AsQueryable());
		var cycleRepository = Substitute.For<IRepository<AdmissionCycle, Guid>>();
		cycleRepository.GetQueryableAsync().Returns(new[] { cycle, otherCycle }.AsQueryable());
		var divisionRepository = Substitute.For<IRepository<Division, Guid>>();
		divisionRepository.GetQueryableAsync().Returns(Array.Empty<Division>().AsQueryable());
		var scopeService = Substitute.For<IUserScopeAccessService>();
		scopeService.GetCurrentUserScopeAsync().Returns(new UserScopeContext
		{
			AcademicYearIds = [year], BoardIds = [board], MediumIds = [medium], MediumStageIds = [stage],
			StandardIds = [standard], WildcardDivisionStandardIds = [standard]
		});
		var service = new AdmissionAssessmentAccessService(
			Substitute.For<IRepository<AdmissionApplication, Guid>>(),
			Substitute.For<IRepository<AdmissionApplicationStageInstance, Guid>>(),
			Substitute.For<IRepository<AdmissionApplicationWorkflowInstance, Guid>>(),
			Substitute.For<IRepository<AdmissionWorkflowStageDefinition, Guid>>(),
			Substitute.For<IRepository<AdmissionWorkflowDefinition, Guid>>(), offeringRepository, cycleRepository, divisionRepository, scopeService);
		(await service.GetScopedOfferingsAsync()).Select(item => item.Id).ShouldBe([allowed.Id]);
	}
}
