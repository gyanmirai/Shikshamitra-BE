using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Records_certificate_history_projects_complete_ordered_metadata_without_frozen_json_and_keeps_live_student_guards() => RunCertificationScenarioAsync(async () =>
	{
		await EnsureTestIdentityUserAsync(TestUserId, "certificate-history-reader");
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "Certificate history", "Student"); await GrantCompletedRecordAsync(student);
		var other = await CreateExistingStudentAsync(offering, "Other certificate history", "Student"); await GrantCompletedRecordAsync(other);
		var expected = new List<StudentCertificateHistoryDto>(); var sourceId = Guid.NewGuid();
		var firstDate = DateTime.UtcNow.Date.AddYears(-1);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentCertificate, Guid>>();
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			for (var index = 0; index < 125; index++)
			{
				var id = index == 0 ? sourceId : Guid.NewGuid(); var type = (StudentCertificateType)(index % 4 + 1);
				var status = (StudentCertificateStatus)(index % 5 + 1); var issue = new DateTimeOffset(firstDate.AddDays(index), TimeSpan.Zero);
				Guid? actor = index % 2 == 0 ? TestUserId : null;
				Guid? transition = index % 2 == 0 ? Guid.NewGuid() : null; Guid? source = index > 0 && index % 3 == 0 ? sourceId : null;
				var number = "HISTORY-" + index.ToString("D4"); var created = firstDate.AddMinutes(index);
				var item = new StudentCertificate(id, TestTenantId, student.Id, number, type, issue, actor, status,
					JsonSerializer.Serialize(new { retainedStudentDetails = new string('s', 4096) }),
					JsonSerializer.Serialize(new { retainedTemplate = new string('t', 4096) }), index % 2 == 0 ? student.AcademicYearId : null);
				item.RecordIssuanceContext("history-read-" + index, transition, source);
				await repository.InsertAsync(item);
				db.Entry(item).Property(row => row.CreationTime).CurrentValue = created;
				expected.Add(new()
				{
					CertificateId = id, CertificateNumber = number, CertificateType = type.ToString(), IssueDate = issue, GeneratedBy = actor,
					Status = status.ToString(), CreatedDate = created, TransitionId = transition, SourceCertificateId = source,
					SupportedActions = status == StudentCertificateStatus.Cancelled ? ["View"] : ["View", "Download", "Reprint", "Duplicate", "Void"]
				});
			}
			await repository.InsertAsync(new(Guid.NewGuid(), TestTenantId, other.Id, "OTHER-STUDENT-HISTORY", StudentCertificateType.Bonafide,
				DateTimeOffset.UtcNow, TestUserId, StudentCertificateStatus.Generated, "{}", "{}"));
			await repository.InsertAsync(new(Guid.NewGuid(), Guid.NewGuid(), student.Id, "OTHER-TENANT-HISTORY", StudentCertificateType.Bonafide,
				DateTimeOffset.UtcNow, TestUserId, StudentCertificateStatus.Generated, "{}", "{}"));
			var deleted = new StudentCertificate(Guid.NewGuid(), TestTenantId, student.Id, "DELETED-HISTORY", StudentCertificateType.Bonafide,
				DateTimeOffset.UtcNow, TestUserId, StudentCertificateStatus.Generated, "{}", "{}");
			await repository.InsertAsync(deleted); db.Entry(deleted).Property(item => item.IsDeleted).CurrentValue = true;
			await db.SaveChangesAsync();
		});
		// Match the repository's persisted timestamp representation (SQLite drops Kind),
		// while keeping every expected clock value independent of the history mapper.
		var persistedCreationTime = await WithUnitOfWorkAsync(async () =>
			await (await GetRequiredService<IRepository<StudentCertificate, Guid>>().GetQueryableAsync())
				.Where(item => item.Id == sourceId).Select(item => item.CreationTime).SingleAsync());
		foreach (var item in expected) item.CreatedDate = DateTime.SpecifyKind(item.CreatedDate, persistedCreationTime.Kind);
		var service = GetRequiredService<IStudentCertificateAppService>();
		var measured = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await service.GetHistoryAsync(student.Id);
			return (Result: result, Sql: counter.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudentCertificates\"", StringComparison.Ordinal)).ToArray(),
				Tracked: db.ChangeTracker.Entries<StudentCertificate>().Count());
		});
		measured.Result.Count.ShouldBe(125, "Complete historical metadata remains available beyond the normal 100-row list cap.");
		JsonSerializer.Serialize(measured.Result).ShouldBe(JsonSerializer.Serialize(expected.AsEnumerable().Reverse()));
		var staff = Guid.NewGuid(); await EnsureTestIdentityUserAsync(staff, "unassigned-history-" + staff.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(staff)))
			(await Should.ThrowAsync<BusinessException>(() => service.GetHistoryAsync(student.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetHistoryAsync(student.Id));
		await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), false);
		try { await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetHistoryAsync(student.Id)); }
		finally { await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), true); }
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		await PublishDirectoryPolicyAsync(policy);
		(await Should.ThrowAsync<BusinessException>(() => service.GetHistoryAsync(student.Id))).Code.ShouldBe("Students:Records:DocumentFieldsRestricted");
		await SetInlineFormPolicyAsync(StudentRecordsPolicy.Starter(), 2);
		(await service.GetHistoryAsync(student.Id)).Count.ShouldBe(125);
		measured.Sql.Length.ShouldBe(1);
		measured.Sql.ShouldAllBe(sql => !sql.Contains("\"CertificateDataJson\"", StringComparison.Ordinal) && !sql.Contains("\"TemplateSnapshotJson\"", StringComparison.Ordinal),
			"Metadata history must not fetch full frozen student and template documents.");
		measured.Tracked.ShouldBe(0, "No certificate aggregate is required to read immutable history metadata.");
	});
}
