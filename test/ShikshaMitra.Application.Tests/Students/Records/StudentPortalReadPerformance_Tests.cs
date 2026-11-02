using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Portal_legacy_login_reads_only_scalar_student_keys() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePortalReadFixtureAsync(1);
		var measured = await MeasurePortalReadAsync(() => GetRequiredService<StudentPortalLoginService>().CanAccessAsync(fixture.User));
		measured.Allowed.ShouldBeTrue();
		measured.Sql.ShouldNotBeEmpty();
		measured.Sql.ShouldAllBe(sql => !sql.Contains("PhotoBlobName", StringComparison.Ordinal),
			"Legacy portal eligibility uses Student IDs, never full Student profile and auto-included academic graphs.");
		measured.TrackedStudents.ShouldBe(0);
	});

	[Fact]
	public Task Portal_legacy_login_preserves_deleted_parent_visibility_self_revocation_and_other_child_access() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePortalReadFixtureAsync(2);
		var service = GetRequiredService<StudentPortalLoginService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(fixture.Students[0].AcademicYearId!.Value);
			year.IsDeleted = true;
			await GetRequiredService<IRepository<AcademicYear, Guid>>().UpdateAsync(year, true);
		});
		// Student's academic navigations are configured optional: a deleted parent must
		// not become a new login requirement when the aggregate graph is removed.
		(await WithUnitOfWorkAsync(() => service.CanAccessAsync(fixture.User))).ShouldBeTrue();
		fixture.User.SetIsActive(false);
		(await WithUnitOfWorkAsync(() => service.CanAccessAsync(fixture.User))).ShouldBeFalse();
		fixture.User.SetIsActive(true);
		using (_currentTenant.Change(Guid.NewGuid()))
			(await WithUnitOfWorkAsync(() => service.CanAccessAsync(fixture.User))).ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
		{
			var link = new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, fixture.Students[0].Id, fixture.User.Id,
				"student", "self", TestUserId);
			var repository = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			await repository.InsertAsync(link, true);
			await repository.DeleteAsync(link, true);
		});
		(await WithUnitOfWorkAsync(() => service.CanAccessAsync(fixture.User))).ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
				fixture.Students[1].Id, fixture.User.Id, "guardian", "father", TestUserId), true);
		});
		(await WithUnitOfWorkAsync(() => service.CanAccessAsync(fixture.User))).ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Students[1].Id);
			student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
			await _studentRepository.UpdateAsync(student, true);
		});
		(await WithUnitOfWorkAsync(() => service.CanAccessAsync(fixture.User))).ShouldBeFalse();
	});

	[Fact]
	public Task Portal_guardian_provenance_uses_one_existence_query_as_verified_child_links_grow() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePortalReadFixtureAsync(4);
		var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
		await WithUnitOfWorkAsync(() => links.InsertAsync(new(Guid.NewGuid(), TestTenantId, fixture.Students[0].Id,
			fixture.User.Id, "guardian", "father", TestUserId), true));
		var service = GetRequiredService<StudentPortalSiblingIdentityService>();
		var single = await MeasurePortalReadAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User));
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var student in fixture.Students.Skip(1))
				await links.InsertAsync(new(Guid.NewGuid(), TestTenantId, student.Id, fixture.User.Id, "guardian", "father", TestUserId), true);
		});
		var many = await MeasurePortalReadAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User));
		single.Allowed.ShouldBeFalse(); many.Allowed.ShouldBeFalse();
		Console.WriteLine($"Portal guardian identity SQLite reads: links1={single.Reads}, links4={many.Reads}.");
		many.Reads.ShouldBeLessThanOrEqualTo(single.Reads,
			"An unconfirmed parent's exact credential-request provenance must be one correlated existence query, not one query per child link.");
	});

	[Fact]
	public Task Portal_guardian_provenance_preserves_exact_identity_link_child_email_status_and_soft_delete() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePortalReadFixtureAsync(2);
		var link = new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, fixture.Students[0].Id, fixture.User.Id,
			"guardian", "father", TestUserId);
		var request = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), link.StudentId,
			link.Id, fixture.User.Id, "guardian", fixture.User.Email!, TestUserId, DateTime.UtcNow);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link, true);
			await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().InsertAsync(request, true);
		});
		var service = GetRequiredService<StudentPortalSiblingIdentityService>();
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeTrue();
		foreach (var (property, invalid) in new (string Property, object Invalid)[]
		{
			(nameof(StudentCredentialEmailRequest.UserId), Guid.NewGuid()),
			(nameof(StudentCredentialEmailRequest.LinkId), Guid.NewGuid()),
			(nameof(StudentCredentialEmailRequest.StudentId), fixture.Students[1].Id),
			(nameof(StudentCredentialEmailRequest.Email), "different-owner@example.test"),
			(nameof(StudentCredentialEmailRequest.AccountType), "student"),
			(nameof(StudentCredentialEmailRequest.Status), "Cancelled"),
			(nameof(StudentCredentialEmailRequest.IsDeleted), true),
			(nameof(StudentCredentialEmailRequest.TenantId), Guid.NewGuid())
		})
		{
			var previous = await SetPortalFixturePropertyAsync<StudentCredentialEmailRequest>(request.Id, property, invalid);
			(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeFalse(property);
			await SetPortalFixturePropertyAsync<StudentCredentialEmailRequest>(request.Id, property, previous!);
		}
		foreach (var status in new[] { "Pending", "Processing", "Sent", "Failed" })
		{
			await SetPortalFixturePropertyAsync<StudentCredentialEmailRequest>(request.Id, nameof(StudentCredentialEmailRequest.Status), status);
			(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeTrue(status);
		}
		await SetPortalFixturePropertyAsync<StudentPortalAccountLink>(link.Id, nameof(StudentPortalAccountLink.IsDeleted), true);
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeFalse();
		await SetPortalFixturePropertyAsync<StudentPortalAccountLink>(link.Id, nameof(StudentPortalAccountLink.IsDeleted), false);
		await SetPortalFixturePropertyAsync<StudentPortalAccountLink>(link.Id, nameof(StudentPortalAccountLink.VerifiedByUserId), Guid.Empty);
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeFalse();
		await SetPortalFixturePropertyAsync<StudentPortalAccountLink>(link.Id, nameof(StudentPortalAccountLink.VerifiedByUserId), TestUserId);
		await SetPortalFixturePropertyAsync<StudentCredentialEmailRequest>(request.Id, nameof(StudentCredentialEmailRequest.Status), "Cancelled");
		fixture.User.SetEmailConfirmed(true);
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeTrue();
		fixture.User.SetIsActive(false);
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeFalse();
		fixture.User.SetIsActive(true);
		using (_currentTenant.Change(Guid.NewGuid()))
			(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(fixture.User))).ShouldBeFalse();
	});

	private async Task<(IdentityUser User, Student[] Students)> CreatePortalReadFixtureAsync(int count)
	{
		var userId = Guid.NewGuid();
		var user = new IdentityUser(userId, $"portal-read-{userId:N}", $"portal-read-{userId:N}@example.test", TestTenantId);
		await WithUnitOfWorkAsync(async () => (await GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue());
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var students = new Student[count];
		for (var index = 0; index < count; index++)
		{
			students[index] = await CreateExistingStudentAsync(offering, $"PortalRead{index}", "Student");
			await WithUnitOfWorkAsync(async () =>
			{
				var current = await _studentRepository.GetAsync(students[index].Id);
				current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
				if (index == 0) current.LinkUser(user.Id);
				await _studentRepository.UpdateAsync(current, true);
			});
		}
		return (user, students);
	}

	private Task<object?> SetPortalFixturePropertyAsync<TEntity>(Guid id, string property, object value) where TEntity : class => WithUnitOfWorkAsync(async () =>
	{
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		var entity = await db.Set<TEntity>().IgnoreQueryFilters().SingleAsync(row => EF.Property<Guid>(row, "Id") == id);
		var entry = db.Entry(entity).Property(property); var previous = entry.CurrentValue; entry.CurrentValue = value;
		await db.SaveChangesAsync(); return previous;
	});

	private Task<(bool Allowed, int Reads, int TrackedStudents, string[] Sql)> MeasurePortalReadAsync(Func<Task<bool>> read) => WithUnitOfWorkAsync(async () =>
	{
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
		using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
		var allowed = await read();
		return (allowed, counter.ReadCount, db.ChangeTracker.Entries<Student>().Count(), counter.ReadCommands.ToArray());
	});
}
