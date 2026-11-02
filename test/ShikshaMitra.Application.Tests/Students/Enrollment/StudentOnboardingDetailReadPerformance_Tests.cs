using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Onboarding_detail_preserves_complete_command_mapping_without_loading_academic_and_history_graphs(bool deletedYear) =>
		RunCertificationScenarioAsync(async () =>
		{
			Guid caseId;
			Guid studentId;
			if (deletedYear)
			{
				var fixture = await CreateOneClickCredentialsCaseAsync("activation", accountsEnabled: false, emailEnabled: false);
				caseId = fixture.Case.Id;
				studentId = fixture.Case.StudentId;
				await WithUnitOfWorkAsync(async () =>
				{
					var years = GetRequiredService<IRepository<AcademicYear, Guid>>();
					var year = await years.GetAsync(fixture.Case.AcademicYearId);
					year.IsDeleted = true;
					await years.UpdateAsync(year, true);
				});
			}
			else
			{
				var fixture = await CreateCertificationFixtureAsync("DetailProfile");
				var finalized = await FinalizeAsync(fixture.Application, true, "detail-profile-read");
				caseId = finalized.OnboardingCaseId;
				studentId = finalized.StudentId;
			}
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetAsync(studentId);
				if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue)
					student.AssignGrNumber($"DETAIL-{Guid.NewGuid():N}"[..24], null, "Existing permanent identifier");
				student.SetProperty("AdmissionStudentField.Academic.SecondLanguage", "Detail profile sentinel");
				await _studentRepository.UpdateAsync(student, true);
			});
			await WithUnitOfWorkAsync(async () =>
				(await _studentRepository.GetAsync(studentId)).GetProperty<string>("AdmissionStudentField.Academic.SecondLanguage")
					.ShouldBe("Detail profile sentinel"));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			// Complete automatic adoption before comparing stable full read payloads.
			var warmed = await service.GetAsync(caseId);
			var expected = await service.AssignGrNumberAsync(caseId, new() { ConcurrencyStamp = warmed.ConcurrencyStamp, NumberSeriesId = Guid.NewGuid() });
			expected.GrNumber.ShouldNotBeNullOrWhiteSpace();
			expected.CapturedStudentFields.ShouldContain(field => field.Key == "Academic.SecondLanguage" && field.Value == "Detail profile sentinel",
				JsonSerializer.Serialize(expected.CapturedStudentFields));
			expected.PortalAccountOwners.ShouldNotBeNull();
			expected.PortalAccountOwners.ShouldContain(owner => owner.AccountType == "student");
			expected.Events.ShouldNotBeEmpty();
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var actual = await service.GetAsync(caseId);
				JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected),
					"Read-only detail must retain every canonical command-mapped field, event, actor, captured answer and portal owner.");
				if (deletedYear)
				{
					actual.AcademicYearName.ShouldBe(string.Empty);
					actual.IntendedBoardName.ShouldBe(string.Empty);
					actual.IntendedMediumName.ShouldBe(string.Empty);
					actual.IntendedStandardName.ShouldNotBeNullOrWhiteSpace();
				}
				db.ChangeTracker.Entries<Standard>().ShouldBeEmpty("A detail label must not recursively hydrate the academic hierarchy.");
				db.ChangeTracker.Entries<EducationMediumStage>().ShouldBeEmpty();
				db.ChangeTracker.Entries<EducationMedium>().ShouldBeEmpty();
				db.ChangeTracker.Entries<EducationBoard>().ShouldBeEmpty();
				db.ChangeTracker.Entries<AcademicYear>().ShouldBeEmpty();
				capture.ReadCommands.ShouldAllBe(sql => !sql.Contains("StudentNotes", StringComparison.Ordinal) && !sql.Contains("StatusHistories", StringComparison.Ordinal),
					"The displayed Student root/contact/family profile must not include unrelated notes and lifecycle history collections.");
			});
		});
}
