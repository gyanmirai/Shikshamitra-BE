using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class AttendanceReadPerformance_Tests
{
	[Theory]
	[InlineData(1, false)]
	[InlineData(201, false)]
	[InlineData(201, true)]
	public async Task Daily_directory_expansion_reads_one_complete_scoped_class_and_keeps_canonical_masks_and_statuses(int memberCount, bool masked)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var actorId = Guid.NewGuid();
			await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(
				new IdentityUser(actorId, $"workspace-{actorId:N}", $"{actorId:N}@example.test", GetRequiredService<ICurrentTenant>().Id), true);
			using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
				new(AbpClaimTypes.UserId, actorId.ToString()), new(AbpClaimTypes.UserName, "workspace-reader"),
				new(AbpClaimTypes.TenantId, GetRequiredService<ICurrentTenant>().Id!.Value.ToString())], "Test")));
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id!.Value, userId: actorId), true);
			var fixture = await SeedAsync(memberCount);
			var approvals = fixture.Members.Select(member => {
				var approval = new StudentRecordLegacyOverride(Guid.NewGuid(), fixture.TenantId, member.StudentId, Guid.NewGuid(), "Verified synthetic class roster", null);
				approval.Decide(actorId, true, "Synthetic fixture approval", new DateTime(2026, 9, 1)); return approval;
			}).ToList();
			await GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().InsertManyAsync(approvals, true);
			var policy = StudentRecordsPolicy.Starter();
			if (masked) policy.Access.Masks = [new() { FieldGroup = "Identity", AllowedRoleNames = ["not-this-reader"] }];
			var config = new StudentRecordsConfiguration(Guid.NewGuid(), fixture.TenantId, fixture.YearId, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().InsertAsync(config, true);
			var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), fixture.TenantId, config.Id, fixture.YearId, 1, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(version, true);
			config.Publish(version.Id); await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().UpdateAsync(config, true);
			var date = new DateTime(2026, 9, 3);
			if (memberCount > 1)
			{
				var nextYear = await GetRequiredService<AcademicYearManager>().CreateAsync("Other current year", new DateTime(2027, 4, 1), new DateTime(2028, 3, 31));
				await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(nextYear, true);
				var nextBoard = await GetRequiredService<EducationBoardManager>().CreateAsync(nextYear.Id, EducationBoardType.CBSE);
				await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(nextBoard, true);
				var nextMedium = await GetRequiredService<EducationMediumManager>().CreateAsync(nextBoard.Id, "Other current medium");
				await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(nextMedium, true);
				var nextStage = await GetRequiredService<EducationMediumStageManager>().CreateAsync(nextMedium.Id, "Other current stage");
				await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(nextStage, true);
				var nextStandard = await GetRequiredService<StandardManager>().CreateAsync(nextStage.Id, "Other current class", 7, null);
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(nextStandard, true);
				var alternate = await GetRequiredService<DivisionManager>().CreateAsync(nextStandard.Id, "Other current division", 300);
				await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(alternate, true);
				var currentStudent = await GetRequiredService<IStudentRepository>().GetAsync(fixture.Members[1].StudentId);
				currentStudent.UpdateAcademicPlacement(nextYear.Id, nextBoard.Id, nextMedium.Id, nextStage.Id, nextStandard.Id, alternate.Id);
				await GetRequiredService<IStudentRepository>().UpdateAsync(currentStudent, true);
				var currentPolicy = StudentRecordsPolicy.Starter();
				if (!masked) currentPolicy.Access.Masks = [new() { FieldGroup = "Identity", AllowedRoleNames = ["not-this-reader"] }];
				var currentConfig = new StudentRecordsConfiguration(Guid.NewGuid(), fixture.TenantId, nextYear.Id, currentPolicy.ToJson());
				await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().InsertAsync(currentConfig, true);
				var currentVersion = new StudentRecordsPolicyVersion(Guid.NewGuid(), fixture.TenantId, currentConfig.Id, nextYear.Id, 1, currentPolicy.ToJson());
				await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(currentVersion, true);
				currentConfig.Publish(currentVersion.Id); await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().UpdateAsync(currentConfig, true);
				await SaveSnapshotAsync(fixture, fixture.Members[1], fixture.DivisionId, date, AttendanceStatus.Absent, 0, 1);
				await SaveSnapshotAsync(fixture, fixture.Members[1], fixture.DivisionId, date.AddDays(-1), AttendanceStatus.Present, 1, 1);
				// The scoped Directory must not add an enrolled student without the canonical Records eligibility evidence.
				var unapproved = new Student(Guid.NewGuid(), fixture.TenantId, "Hidden", null, "Unapproved", fixture.YearId,
					(await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(fixture.MediumId)).EducationBoardId, fixture.MediumId, fixture.StageId, fixture.StandardId, fixture.DivisionId,
					status: StudentLifeCycleStatusEnum.Enrolled);
				await GetRequiredService<IStudentRepository>().InsertAsync(unapproved, true);
				await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(Guid.NewGuid(),
					fixture.TenantId, unapproved.Id, fixture.YearId, fixture.StandardId, fixture.DivisionId, 999), true);
			}
			var directory = GetRequiredService<StudentDirectoryQueryService>();
			var expected = new List<StudentDirectoryItemDto>();
			while (true)
			{
				var page = await directory.GetAsync(new() { AcademicYearId = fixture.YearId, StandardId = fixture.StandardId,
					DivisionId = fixture.DivisionId, Population = "Active", SkipCount = expected.Count, MaxResultCount = 100 });
				expected.AddRange(page.Items);
				if (expected.Count >= page.TotalCount || page.Items.Count == 0) break;
			}
			expected.Count.ShouldBe(memberCount);
			var firstId = fixture.Members[0].StudentId;
			var rows = new List<StudentAttendanceDto> { new() { StudentId = firstId, IsExpected = true,
				EnrollmentId = fixture.Members[0].Id, Status = AttendanceStatus.Present, Remarks = "Preserved roster remark", RollNo = 1 } };
			var db = await DatabaseAsync(); db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			await GetRequiredService<AttendanceWorkspaceQueryService>().AddDirectoryRowsAsync(rows, fixture.YearId, fixture.StandardId, fixture.DivisionId, date);
			rows.Select(row => row.StudentId).ShouldBe(new[] { firstId }.Concat(expected.Where(row => row.StudentId != firstId).Select(row => row.StudentId)));
			foreach (var item in expected)
			{
				var actual = rows.Single(row => row.StudentId == item.StudentId);
				(actual.FirstName, actual.MiddleName, actual.LastName, actual.GrNo, actual.GrNumber)
					.ShouldBe((item.FirstName, item.MiddleName, item.LastName, item.GrNo, item.GrNumber ?? item.GrNo?.ToString(System.Globalization.CultureInfo.InvariantCulture)));
				(actual.StandardId, actual.DivisionId, actual.StandardName, actual.ClassName, actual.DivisionName)
					.ShouldBe((item.StandardId, item.DivisionId, item.StandardName, item.StandardName, item.DivisionName));
			}
			rows[0].IsExpected.ShouldBeTrue(); rows[0].Status.ShouldBe(AttendanceStatus.Present); rows[0].Remarks.ShouldBe("Preserved roster remark");
			rows.Count(row => row.IsExpected).ShouldBe(1);
			if (memberCount > 1)
			{
				var historical = rows.Single(row => row.StudentId == fixture.Members[1].StudentId);
				historical.DivisionId.ShouldBe(fixture.DivisionId); historical.Status.ShouldBe(AttendanceStatus.Absent);
				historical.IsExpected.ShouldBeFalse(); historical.NotExpectedReason.ShouldBe("No eligible enrollment or active placement for this date");
			}
			if (masked) rows.ShouldAllBe(row => row.FirstName == null && row.LastName == null && row.GrNumber == null);
			var countReads = capture.ReadCommands.Count(sql => sql.Contains("SELECT COUNT(*)", StringComparison.OrdinalIgnoreCase) && sql.Contains("AppStudents", StringComparison.Ordinal));
			var policyReads = capture.ReadCommands.Count(sql => sql.Contains("AppStudentRecordsConfigurations", StringComparison.Ordinal) || sql.Contains("AppStudentRecordsPolicyVersions", StringComparison.Ordinal));
			output.WriteLine($"Synthetic complete class members={memberCount}; masked={masked}; student counts={countReads}; display policy reads={policyReads}; total reads={capture.ReadCount}.");
			countReads.ShouldBe(1); policyReads.ShouldBeLessThanOrEqualTo(memberCount == 1 ? 3 : 5);
		});
	}
}
