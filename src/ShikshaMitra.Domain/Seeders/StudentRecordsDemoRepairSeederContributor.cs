using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>Supplies canonical records for legacy synthetic students, without bypassing Directory eligibility.</summary>
public sealed class StudentRecordsDemoRepairSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	IDataFilter<ISoftDelete> softDeleteFilter,
	IAsyncQueryableExecuter queries,
	IStudentRepository students,
	IRepository<IdentityUser, Guid> users,
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentRecordLegacyOverride, Guid> overrides,
	IRepository<AcademicYear, Guid> years,
	IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions,
	ILogger<StudentRecordsDemoRepairSeederContributor> logger)
	: BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	internal const string StepName = "StudentRecordsDemoRepairSeeder";
	internal const string StepVersion = "StudentRecordsDemoRepairSeeder:v1";
	internal const int StepOrder = 171;

	public override async Task SeedAsync(DataSeedContext context)
	{
		// The orchestrator shares a host seed context while switching the ambient tenant for each step.
		if (!CurrentTenant.Id.HasValue || context.TenantId.HasValue && context.TenantId != CurrentTenant.Id ||
			context.Properties.TryGetValue(ShikshaMitraSeederProfile.ContextPropertyName, out var profile) &&
			!ShikshaMitraSeederProfile.Demo.Contains(ShikshaMitraSeederProfile.Normalize(profile?.ToString()))) return;
		var tenantId = CurrentTenant.Id.Value;
		var demoUsers = (await users.GetListAsync(u => u.TenantId == tenantId &&
			u.UserName.StartsWith("student-") && u.Email == u.UserName + "@example.invalid", includeDetails: false))
			.Select(u => u.Id).ToHashSet();
		var candidates = (await queries.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.TenantId == tenantId &&
			s.Status == StudentLifeCycleStatusEnum.Enrolled && s.GrNo >= 10001 && s.RollNo > 0 &&
			s.AcademicYearId.HasValue && s.StandardId.HasValue && s.DivisionId.HasValue)))
			.Where(s => s.UserId == s.Id && demoUsers.Contains(s.Id)).ToList();
		if (candidates.Count == 0) return;

		var approver = await users.FindAsync(u => u.TenantId == tenantId &&
			u.UserName == "admin-ddvs" && u.Email == "admin-ddvs@ddvs.com");
		if (approver == null)
			throw new InvalidOperationException("Student demo repair requires the existing seeded admin-ddvs account.");

		// Deleted or rejected records are also blockers: a demo rerun must not undo an operator's decision.
		List<StudentEnrollment> existingEnrollments;
		HashSet<Guid> managedStudents;
		using (softDeleteFilter.Disable())
		{
			existingEnrollments = await enrollments.GetListAsync(e => e.TenantId == tenantId, includeDetails: false);
			managedStudents = existingEnrollments.Select(e => e.StudentId).ToHashSet();
			managedStudents.UnionWith((await cases.GetListAsync(c => c.TenantId == tenantId, includeDetails: false)).Select(c => c.StudentId));
			managedStudents.UnionWith((await overrides.GetListAsync(o => o.TenantId == tenantId, includeDetails: false)).Select(o => o.StudentId));
		}
		var validYears = (await years.GetListAsync(y => y.TenantId == tenantId, includeDetails: false)).Select(y => y.Id).ToHashSet();
		var validStandards = (await queries.ToListAsync((await standards.GetQueryableAsync()).Where(s => s.TenantId == tenantId)
			.Select(s => new { s.Id, s.EducationMediumStageId }))).ToDictionary(s => s.Id);
		var validDivisions = (await queries.ToListAsync((await divisions.GetQueryableAsync()).Where(d => d.TenantId == tenantId)
			.Select(d => new { d.Id, d.StandardId }))).ToDictionary(d => d.Id);
		var occupiedRolls = existingEnrollments.Where(e => !e.IsDeleted && e.IsActive && e.RollNumber.HasValue)
			.Select(e => (e.AcademicYearId, e.DivisionId, e.RollNumber)).ToHashSet();
		var repaired = 0;
		foreach (var student in candidates.OrderBy(s => s.GrNo).ThenBy(s => s.Id))
		{
			if (managedStudents.Contains(student.Id) || student.Id == approver.Id ||
				!validYears.Contains(student.AcademicYearId!.Value) ||
				!validStandards.TryGetValue(student.StandardId!.Value, out var standard) ||
				standard.EducationMediumStageId != student.AcademicMediumStageId ||
				!validDivisions.TryGetValue(student.DivisionId!.Value, out var division) || division.StandardId != standard.Id ||
				!occupiedRolls.Add((student.AcademicYearId.Value, student.DivisionId, student.RollNo))) continue;

			await enrollments.InsertAsync(StudentEnrollment.Create(GuidGenerator.Create(), tenantId,
				student.Id, student.AcademicYearId.Value, student.StandardId, student.DivisionId, student.RollNo));
			var approval = new StudentRecordLegacyOverride(GuidGenerator.Create(), tenantId, student.Id, student.Id,
				"Synthetic StudentSeeder demo record imported with its seeded academic placement.", StepVersion);
			approval.Decide(approver.Id, true, "Demo-data approval only; no admission checklist or payment evidence is asserted.", DateTime.UtcNow);
			await overrides.InsertAsync(approval);
			repaired++;
		}
		await CurrentUnitOfWork.SaveChangesAsync();
		logger.LogInformation("[StudentRecordsDemoRepairSeeder] Repaired={Repaired}, Candidates={Candidates}, TenantId={TenantId}",
			repaired, candidates.Count, tenantId);
	}
}
