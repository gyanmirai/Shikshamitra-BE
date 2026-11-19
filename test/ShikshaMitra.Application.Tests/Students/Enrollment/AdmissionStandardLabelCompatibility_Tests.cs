using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("stage", false)]
	[InlineData("medium", false)]
	[InlineData("board", false)]
	[InlineData("year", false)]
	[InlineData("stage", true)]
	[InlineData("medium", true)]
	[InlineData("board", true)]
	[InlineData("year", true)]
	public Task Admission_class_labels_preserve_original_repository_required_ancestor_visibility(string ancestor, bool includeDeleted) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateManagedAssessmentApplicationAsync();
			var inquiryId = await WithUnitOfWorkAsync(async () => (await _applicationRepository.GetAsync(fixture.Application.Id)).InquiryId!.Value);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var yearId = (await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId)).AcademicYearId;
				switch (ancestor)
				{
					case "stage":
						await db.Set<EducationMediumStage>().Where(item => item.Id == fixture.Offering.StageId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
					case "medium":
						await db.Set<EducationMedium>().Where(item => item.Id == fixture.Offering.MediumId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
					case "board":
						await db.Set<EducationBoard>().Where(item => item.Id == fixture.Offering.BoardId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
					case "year":
						await db.Set<AcademicYear>().Where(item => item.Id == yearId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
				}
			});
			await WithUnitOfWorkAsync(async () =>
			{
				using var deletedScope = includeDeleted ? GetRequiredService<IDataFilter>().Disable<ISoftDelete>() : null;
				var standards = GetRequiredService<IRepository<Standard, Guid>>();
				// These are the exact pre-optimization generic-repository calls. Their
				// resolved defaults differ from explicit custom WithDetails requests.
				var expectedListName = (await standards.GetListAsync(item => item.Id == fixture.Offering.StandardId)).SingleOrDefault()?.Name;
				var expectedDetailName = (await standards.FindAsync(fixture.Offering.StandardId))?.Name;
				Console.WriteLine($"Deleted {ancestor}, includeDeleted={includeDeleted}: original Standard list label={expectedListName ?? "null"}; detail label={expectedDetailName ?? "null"}.");
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
				var list = await inquiries.GetListAsync(new() { AdmissionOfferingId = fixture.Offering.Id, MaxResultCount = 10 });
				list.Items.Single().StandardName.ShouldBe(expectedListName);
				(await inquiries.GetAsync(inquiryId)).StandardName.ShouldBe(expectedDetailName);
				var scopes = await GetRequiredService<IAdmissionAssessmentAppService>().GetScopesAsync();
				scopes.Single(item => item.AdmissionOfferingId == fixture.Offering.Id).ClassName.ShouldBe(expectedListName ?? string.Empty);
				db.ChangeTracker.Entries<Standard>().ShouldBeEmpty();
			});
		});
}
