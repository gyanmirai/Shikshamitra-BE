using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("studentname asc")]
	public async Task Prior_year_enrollment_remains_visible_after_student_moves_to_next_year(string? sorting)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			// Keep placement edits tracked in one UOW; reattaching the auto-included
			// academic navigations in separate repository calls can restore old FKs.
			await WithUnitOfWorkAsync(async () =>
			{
				var (priorYear, studentId, feeHeadId, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
				var student = await _studentRepo.GetAsync(studentId);
				var priorBoard = student.AcademicBoardId!.Value;
				var priorMedium = student.AcademicMediumId;
				var standard = new Standard(Guid.NewGuid(), TestTenantId, "Prior year class", 10,
					student.AcademicMediumStageId!.Value, null);
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
				var division = new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "A1");
				await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
				student.UpdateAcademicPlacement(priorYear, priorBoard, priorMedium,
					student.AcademicMediumStageId.Value, standard.Id, division.Id);
				await _studentRepo.UpdateAsync(student, true);
				await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(
					StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, studentId, priorYear,
						standard.Id, division.Id, rollNumber: 17), true);

				var (nextYear, nextStudentId, _, _, _) = await SeedStudentWithOverdueDueFeeAsync(0, false,
					academicYearName: "2026-27");
				var nextStudent = await _studentRepo.GetAsync(nextStudentId);
				student.UpdateAcademicPlacement(nextYear, nextStudent.AcademicBoardId!.Value,
					nextStudent.AcademicMediumId, nextStudent.AcademicMediumStageId!.Value, null, null);
				await _studentRepo.UpdateAsync(student, true);

				var persistedYear = (await _studentRepo.GetQueryableAsync())
					.Where(item => item.Id == studentId).Select(item => item.AcademicYearId).Single();
				persistedYear.ShouldBe(nextYear);

				var summary = await _feeCollections.GetSummaryAsync(new GetFeeCollectionSummaryInput
				{
					AcademicYearId = priorYear, BoardId = priorBoard,
					EducationMediumId = priorMedium, MaxResultCount = 100, Sorting = sorting
				});
				var row = summary.Items.Single(item => item.StudentId == studentId);
				row.StandardId.ShouldBe(standard.Id);
				row.DivisionId.ShouldBe(division.Id);
				row.RollNumber.ShouldBe("17");
				row.StandardDivision.ShouldBe("Prior year class - A");
				row.IsFeeStructureAssigned.ShouldBeTrue();
				(await _feeCollections.GetCollectFormAsync(studentId, priorYear)).Ledger.ShouldNotBeEmpty();
				var payment = await _feeCollections.CollectFeeAsync(new CollectFeeInput
				{
					IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = studentId,
					AcademicYearId = priorYear,
					DueFeePayments = [new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 5000d }],
					Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 5000d }],
				});
				payment.FeeReceiptAmount.ShouldBe(5000m);
			});
		}
	}
}
