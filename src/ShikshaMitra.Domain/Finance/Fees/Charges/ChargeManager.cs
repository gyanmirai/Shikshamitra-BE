using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Students;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Domain service that orchestrates charge creation and enforces
/// invariants that span multiple aggregates (e.g. scope derivation,
/// category-active check, maker-checker rule).
/// </summary>
public class ChargeManager(
	IRepository<Charge, Guid> chargeRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	IGuidGenerator guidGenerator,
	ILogger<ChargeManager> logger) : DomainService
{
	/// <summary>
	/// Creates a new Charge.
	/// Scope (BoardId, MediumId, StandardId, DivisionId) is derived from the
	/// Student aggregate — never accepted from the caller — to prevent tampering.
	/// Initial status is determined by <see cref="ChargeCategory.IsApprovalRequired"/>.
	/// </summary>
	public async Task<Charge> CreateAsync(
		Guid? tenantId,
		Guid academicYearId,
		Guid studentId,
		Guid chargeCategoryId,
		string purposeDescription,
		decimal amount,
		DateTime dueDate,
		ChargePriority priority,
		Guid requestorId,
		string? additionalNotes = null)
	{
		logger.LogDebug(
			"[ChargeManager] Creating charge: StudentId={StudentId}, CategoryId={CategoryId}, Amount={Amount}",
			studentId, chargeCategoryId, amount);

		var student = await studentRepository.FindAsync(studentId)
			?? throw new BusinessException(FinanceChargeErrorCodes.StudentNotFound)
				.WithData("StudentId", studentId);

		if (student.StandardId is null || student.DivisionId is null)
		{
			throw new BusinessException(FinanceChargeErrorCodes.StudentMissingAcademicScope)
				.WithData("StudentId", studentId);
		}

		var category = await chargeCategoryRepository.FindAsync(chargeCategoryId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryNotFound)
				.WithData("ChargeCategoryId", chargeCategoryId);

		if (!category.IsActive)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryInactive)
				.WithData("ChargeCategoryId", chargeCategoryId)
				.WithData("CategoryName", category.Name);
		}

		var initialStatus = category.IsApprovalRequired
			? ChargeStatus.Pending
			: ChargeStatus.Approved;

		var charge = new Charge(
			id: guidGenerator.Create(),
			tenantId: tenantId,
			academicYearId: academicYearId,
			chargeCategoryId: chargeCategoryId,
			studentId: studentId,
			boardId: student.AcademicBoardId ?? Guid.Empty,
			educationMediumId: student.AcademicMediumId,
			standardId: student.StandardId.Value,
			divisionId: student.DivisionId.Value,
			purposeDescription: purposeDescription,
			amount: amount,
			dueDate: dueDate,
			priority: priority,
			initialStatus: initialStatus,
			requestorId: requestorId,
			additionalNotes: additionalNotes);

		await chargeRepository.InsertAsync(charge, autoSave: true);
		return charge;
	}
}
