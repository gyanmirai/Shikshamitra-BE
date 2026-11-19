using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("preview")]
	[InlineData("issue")]
	[InlineData("pdf")]
	public Task Records_idcard_bulk_reuses_school_identity_metadata_by_exact_path_with_isolated_fresh_snapshots(string operation) => RunCertificationScenarioAsync(async () =>
	{
		using var actor = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			BuildAdminPrincipal().Claims.Append(new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())), "Test")));
		var workflow = await CreateAndPublishWorkflowAsync();
		var firstPath = await CreateAvailableOfferingAsync(workflow.Id);
		var students = new List<Student>();
		for (var index = 0; index < 4; index++)
		{
			var student = await CreateExistingStudentAsync(firstPath, "CardRead" + index, "Student");
			await GrantCompletedRecordAsync(student); students.Add(student);
		}
		var target = await NextRecordYearAsync(students[0].AcademicYearId!.Value);
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(students[3].Id);
			var standard = await _standardRepository.GetAsync(target.Standard);
			var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
			student.UpdateAcademicPlacement(target.Year, medium.EducationBoardId, medium.Id, stage.Id, target.Standard, target.Division);
			await _studentRepository.UpdateAsync(student, true);
			var old = await _studentEnrollmentRepository.GetAsync(item => item.StudentId == student.Id);
			old.Deactivate(); await _studentEnrollmentRepository.UpdateAsync(old, true);
			await _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, student.Id,
				target.Year, target.Standard, target.Division, null), true);
		});
		var identity = GetRequiredService<ISchoolIdentityAppService>();
		var logo = await identity.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "batch-read-logo.png",
			SchoolIdentityTestImage.Png(32, 32));
		var profile = await identity.UpdateAsync(new() { Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = logo.Id } } });
		var service = GetRequiredService<IStudentIdCardAppService>();
		var oracles = new List<StudentIdCardDto>();
		foreach (var student in students) oracles.Add((await service.PreviewAsync(new() { StudentIds = [student.Id] })).Single());
		oracles.Select(card => (card.TemplateSnapshot.SchoolIdentity!.BoardId, card.TemplateSnapshot.SchoolIdentity.MediumId,
			card.TemplateSnapshot.SchoolIdentity.OrganizationId)).Distinct().Count().ShouldBe(2);
		var input = new IssueStudentIdCardsDto { StudentIds = students.Select(student => student.Id).ToList(), IdempotencyKey = "school-identity-read-batch" };
		var measured = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			List<StudentIdCardDto>? cards = null;
			if (operation == "preview") cards = await service.PreviewAsync(input);
			else if (operation == "issue") cards = await service.IssueAsync(input);
			else
			{
				var pdf = await service.ExportPdfAsync(new() { StudentIds = input.StudentIds, CardsPerPage = 8 });
				pdf.Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
			}
			return (Cards: cards, Reads: counter.ReadCommands.Count(sql => sql.Contains("FROM \"AppSchoolIdentityProfiles\"", StringComparison.Ordinal)));
		});
		if (measured.Cards != null)
		{
			measured.Cards.Select(card => card.StudentId).ShouldBe(input.StudentIds);
			for (var index = 0; index < measured.Cards.Count; index++)
			{
				JsonSerializer.Serialize(measured.Cards[index].StudentSnapshot).ShouldBe(JsonSerializer.Serialize(oracles[index].StudentSnapshot));
				JsonSerializer.Serialize(measured.Cards[index].TemplateSnapshot).ShouldBe(JsonSerializer.Serialize(oracles[index].TemplateSnapshot));
				measured.Cards[index].TemplateSnapshot.SchoolIdentity!.LogoAssetId.ShouldBe(logo.Id);
			}
			ReferenceEquals(measured.Cards[0].TemplateSnapshot.SchoolIdentity, measured.Cards[1].TemplateSnapshot.SchoolIdentity).ShouldBeFalse();
			measured.Cards[0].TemplateSnapshot.SchoolIdentity!.LogoAssetId = Guid.NewGuid();
			measured.Cards[1].TemplateSnapshot.SchoolIdentity!.LogoAssetId.ShouldBe(logo.Id);
		}
		if (operation == "issue")
		{
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordDocumentEvent, Guid>>()
				.CountAsync(item => input.StudentIds.Contains(item.StudentId) && item.Action == "Issued"))).ShouldBe(4);
			(await service.IssueAsync(input)).Select(card => card.Id).ShouldBe(measured.Cards!.Select(card => card.Id));
		}
		await identity.UpdateAsync(new() { ConcurrencyStamp = profile.ConcurrencyStamp, Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Hidden } } });
		(await service.PreviewAsync(new() { StudentIds = [students[0].Id] })).Single().TemplateSnapshot.SchoolIdentity!.LogoAssetId.ShouldBeNull();
		if (operation == "issue")
			(await service.GetHistoryAsync(students[0].Id)).Single().TemplateSnapshot.SchoolIdentity!.LogoAssetId.ShouldBe(logo.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var cases = await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetListAsync(item => item.StudentId == students[1].Id);
			cases.ShouldNotBeEmpty();
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			foreach (var item in cases) db.Entry(item).Property(x => x.IsDeleted).CurrentValue = true;
			await db.SaveChangesAsync();
		});
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(new() { StudentIds = [students[0].Id, students[1].Id] })))
			.Code.ShouldBe("Students:Records:OnboardingIncomplete");
		Console.WriteLine($"I-card {operation}: school-identity profile reads={measured.Reads}, distinct complete school paths=2.");
		measured.Reads.ShouldBe(2, "Three pupils share the exact school path; the fourth has a different board, medium, year and organization. Each path's persisted metadata is read once within this operation.");
	});
}
