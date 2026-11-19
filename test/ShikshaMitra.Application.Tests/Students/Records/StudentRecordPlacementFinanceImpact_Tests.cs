using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact] public Task Record_placement_class_change_requires_finance_impact_without_mutating_completed_student_or_enrollment() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: false);
		var renewal = await fixture.Service.ActivateRenewalAsync(fixture.Renewal.Id, new() { ConcurrencyStamp = fixture.Renewal.ConcurrencyStamp, Remarks = "Annual checks complete" });
		var finance = await SeedRecordRenewalFinanceAsync(fixture.StudentId, renewal.AcademicYearId);
		await CollectRecordRenewalCashAsync(fixture.StudentId, renewal.AcademicYearId, finance.FeeHeadId, finance.DueId, 6000);
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id);
		renewal = await fixture.Service.ActivateRenewalAsync(renewal.Id, new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Confirmed owner payment evidence" }); renewal.Status.ShouldBe("Completed");
		var target = await WithUnitOfWorkAsync(async () =>
		{
			var sourceClass = await _standardRepository.GetAsync(renewal.StandardId);
			var standard = new Standard(Guid.NewGuid(), TestTenantId, "Corrected target class", 3, sourceClass.EducationMediumStageId, null); await _standardRepository.InsertAsync(standard, true);
			var division = new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "Corrected target A"); await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			return (standard.Id, Division: division.Id);
		});
		var changes = GetRequiredService<IStudentRecordChangeAppService>(); var snapshot = await changes.GetSnapshotAsync(fixture.StudentId, "placement");
		var enrollmentBefore = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.StudentId && x.AcademicYearId == renewal.AcademicYearId));
		var studentBefore = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.StudentId));
		var assignmentBefore = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(x => x.StudentId == fixture.StudentId && x.AcademicYearId == renewal.AcademicYearId));
		var request = new CreateStudentRecordChangeDto { StudentId = fixture.StudentId, Section = "placement", Reason = "Review class correction with existing payments", ClientKey = "financial-class-correction", ExpectedSnapshot = snapshot.Snapshot,
			Payload = JsonSerializer.SerializeToElement(new { academicYearId = renewal.AcademicYearId, standardId = target.Id, divisionId = target.Division, rollNumber = renewal.RollNumber }) };
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(request))).Code.ShouldBe("Students:Records:FinancialPlacementImpactRequired");
		(await changes.GetListAsync(new() { StudentId = fixture.StudentId })).TotalCount.ShouldBe(0);
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordPlacementService>().ApplyAsync(fixture.StudentId,
			new() { AcademicYearId = renewal.AcademicYearId, StandardId = target.Id, DivisionId = target.Division, RollNumber = renewal.RollNumber })));
		var enrollmentAfter = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(enrollmentBefore.Id));
		enrollmentAfter.StandardId.ShouldBe(enrollmentBefore.StandardId); enrollmentAfter.DivisionId.ShouldBe(enrollmentBefore.DivisionId); enrollmentAfter.RollNumber.ShouldBe(enrollmentBefore.RollNumber); enrollmentAfter.ConcurrencyStamp.ShouldBe(enrollmentBefore.ConcurrencyStamp);
		var studentAfter = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.StudentId)); studentAfter.StandardId.ShouldBe(studentBefore.StandardId); studentAfter.DivisionId.ShouldBe(studentBefore.DivisionId); studentAfter.GrNumber.ShouldBe(studentBefore.GrNumber); studentAfter.ConcurrencyStamp.ShouldBe(studentBefore.ConcurrencyStamp);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(assignmentBefore.Id))).AssignmentSnapshotJson.ShouldBe(assignmentBefore.AssignmentSnapshotJson);
		// A division/roll correction stays within the existing class and does not
		// claim a change to the immutable fee assignment's class applicability.
		await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordPlacementService>().ValidateImpactAsync(fixture.StudentId,
			new() { AcademicYearId = renewal.AcademicYearId, StandardId = renewal.StandardId, DivisionId = renewal.DivisionId, RollNumber = renewal.RollNumber }));
	});
}
