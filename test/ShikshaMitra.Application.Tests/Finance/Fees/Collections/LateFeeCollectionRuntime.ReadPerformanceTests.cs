using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Fact]
	public async Task Shared_pinned_policy_reads_are_bounded_without_changing_student_exemptions_or_grace()
	{
		Guid year = default;
		DateTime asOf = default;
		var expected = new Dictionary<Guid, decimal>();
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var seeded = await SeedStudentWithOverdueDueFeeAsync(10, true);
				year = seeded.AcademicYearId;
				var originalStudent = await _studentRepo.GetAsync(seeded.StudentId);
				var originalAssignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == seeded.StudentId)).Single();
				var originalDue = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
				var stage = (await _stageRepo.GetListAsync(x => x.EducationMediumId == originalStudent.AcademicMediumId)).Single();
				asOf = originalDue.DueDate.AddDays(10);
				expected.Add(originalDue.Id, 200m);
				for (var index = 1; index < 10; index++)
				{
					var (student, _, due) = await AddPolicyReadStudentAsync(originalStudent, originalAssignment, originalDue, stage.Id, index);
					if (index is 1 or 2)
					{
						var adjustment = new StudentFeePolicyOverride(Guid.NewGuid(), TestTenantId, student.Id, seeded.FeeStructureId);
						adjustment.Update(index == 1, index == 2 ? 5 : null, false, null);
						await GetRequiredService<IRepository<StudentFeePolicyOverride, Guid>>().InsertAsync(adjustment, true);
					}
					expected.Add(due.Id, index == 1 ? 0m : index == 2 ? 100m : 200m);
				}
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var ids = expected.Keys.ToArray();
				var dues = await _dueFeeRepo.GetListAsync(x => Enumerable.Contains(ids, x.Id));
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
				var balances = await GetRequiredService<LateFeeBalanceService>().GetBalancesAsync(dues, year, asOf);
				balances.Count.ShouldBe(10);
				foreach (var item in expected)
				{
					balances[item.Key].Total.ShouldBe(item.Value);
					balances[item.Key].PendingAmount.ShouldBe(item.Value);
					balances[item.Key].PaidAmount.ShouldBe(0m);
					balances[item.Key].WaivedAmount.ShouldBe(0m);
				}
				int CountReads(Type entityType) => reads.Commands.Count(sql => sql.Contains(
					$"\"{db.Model.FindEntityType(entityType)!.GetTableName()}\"", StringComparison.Ordinal));
				var assignments = CountReads(typeof(StudentFeeStructure));
				var versions = CountReads(typeof(FinancePolicyVersion));
				var overrides = CountReads(typeof(StudentFeePolicyOverride));
				var observed = $"Observed assignment/version/override reads: {assignments}/{versions}/{overrides}.";
				assignments.ShouldBeLessThanOrEqualTo(1, "Use the already-loaded assignments. " + observed);
				versions.ShouldBeLessThanOrEqualTo(1, "Load shared pinned versions once. " + observed);
				overrides.ShouldBeLessThanOrEqualTo(2, "Batch current overrides while retaining historical evidence. " + observed);
			}
		});
	}

	[Theory]
	[InlineData("foreign_tenant")]
	[InlineData("wrong_type")]
	[InlineData("wrong_policy")]
	[InlineData("wrong_year")]
	[InlineData("malformed")]
	public async Task Shared_policy_reads_keep_invalid_pins_rejected(string invalidPin)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var seeded = await SeedStudentWithOverdueDueFeeAsync(10, true);
				var source = await _studentRepo.GetAsync(seeded.StudentId);
				var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == source.Id)).Single();
				var due = await _dueFeeRepo.GetAsync(seeded.DueFeeId);
				var stage = (await _stageRepo.GetListAsync(x => x.EducationMediumId == source.AcademicMediumId)).Single();
				var version = await _policyVersionRepo.GetAsync(assignment.CollectionPolicyVersionId!.Value);
				var badVersionId = Guid.NewGuid();
				var versionTenant = invalidPin == "foreign_tenant" ? Guid.NewGuid() : TestTenantId;
				using (_currentTenant.Change(versionTenant))
					await _policyVersionRepo.InsertAsync(new FinancePolicyVersion(badVersionId, versionTenant,
						invalidPin == "wrong_type" ? "refund" : "collection",
						invalidPin == "wrong_policy" ? Guid.NewGuid() : version.PolicyId,
						invalidPin == "wrong_year" ? Guid.NewGuid() : version.AcademicYearId,
						99, "Invalid pinned version", "Active", invalidPin == "malformed" ? "{invalid" : version.SnapshotJson,
						"changed"), true);
				var (_, _, invalidDue) = await AddPolicyReadStudentAsync(source, assignment, due, stage.Id, 99, badVersionId);
				var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<LateFeeBalanceService>()
					.GetBalancesAsync([invalidDue], seeded.AcademicYearId, due.DueDate.AddDays(10), preserveAssessment: true));
				error.Code.ShouldBe(CollectionPolicyErrorCodes.PolicyVersionNotFound);
				invalidDue.GetAssessedLateFeeAmount().ShouldBe(0m);
				invalidDue.PaidAmount.ShouldBe(0m);
			}
		});
	}

	private async Task<(Student Student, StudentFeeStructure Assignment, DueFee Due)> AddPolicyReadStudentAsync(
		Student source, StudentFeeStructure assignment, DueFee originalDue, Guid stageId, int index, Guid? versionId = null)
	{
		var student = new Student(Guid.NewGuid(), TestTenantId, $"Read {index}", null, "Student",
			assignment.AcademicYearId, source.AcademicBoardId!.Value, source.AcademicMediumId, stageId,
			source.StandardId, source.DivisionId, 100 + index, 100 + index, StudentLifeCycleStatusEnum.Enrolled);
		await _studentRepo.InsertAsync(student, true);
		var copy = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, student.Id, assignment.FeeStructureStandardId,
			assignment.AcademicYearId, null, null, ConcessionFeeHeadScope.EntireFeeStructure, [], assignment.CollectionPolicyId,
			versionId ?? assignment.CollectionPolicyVersionId);
		await _studentFeeStructureRepo.InsertAsync(copy, true);
		var due = new DueFee(Guid.NewGuid(), TestTenantId, student.Id, originalDue.FeeHeadId,
			originalDue.Month, originalDue.Year, false, 5000m, originalDue.DueDate);
		due.BindAssignment(copy.Id);
		await _dueFeeRepo.InsertAsync(due, true);
		return (student, copy, due);
	}
}
