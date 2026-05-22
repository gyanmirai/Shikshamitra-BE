using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public class StudentFeeStructureAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<DueFee, Guid> dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	DueFeeManager dueFeeManager
) : ApplicationService, IStudentFeeStructureAppService
{
	public async Task AssignAsync(AssignStudentFeeStructureDto input)
	{
		// get the student
		var student = await studentRepository.GetAsync(input.StudentId) ?? throw new BusinessException("Student with given id does not exist.");

		if (student.FeeStructureId != null)
		{
			throw new BusinessException("Fee structure is already assigned to the student. Please unassign the existing fee structure before assigning a new one.");
		}

		// validate the standard fee structure id
		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId) ?? throw new BusinessException("Fee structure with given id does not exist.");

		// assign the fee structure to the student
		student.AssignFeeStructure(input.StandardFeeStructureId);

		// ── Create StudentFeeStructure record for fee collection module lookups ──
		await studentFeeStructureRepository.InsertAsync(
			new StudentFeeStructure(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				studentId: student.Id,
				feeStructureStandardId: feeStructure.Id),
			autoSave: false);

		// ── Load FeeStructureMetadata to reach the AcademicYear ──────────────
		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);

		// ── Load AcademicYear for month-range computation ─────────────────────
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);

		// ── Generate DueFee records via domain manager ────────────────────────
		var dueFees = await dueFeeManager.GenerateForStudentAsync(
			student.Id,
			feeStructure,
			academicYear,
			input.SelectedOptionalFeeHeadIds);

		// ── Bulk insert — autoSave: false to commit everything atomically ─────
		foreach (var dueFee in dueFees)
			await dueFeeRepository.InsertAsync(dueFee, autoSave: false);

		// ── Single atomic commit: student assignment + StudentFeeStructure + all due fees ─
		await CurrentUnitOfWork.SaveChangesAsync();
	}

	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId)
			?? throw new BusinessException("Student with given id does not exist.");

		if (student.FeeStructureId == null)
			throw new BusinessException("No fee structure has been assigned to this student.");

		var feeStructure = await feeStructureRepository.GetAsync(student.FeeStructureId.Value)
			?? throw new BusinessException("Assigned fee structure could not be found.");

		// Determine which fee heads have due-fee records (i.e., were selected during assignment)
		var selectedFeeHeadIds = (await dueFeeRepository.GetListAsync(d => d.StudentId == studentId))
			.Select(d => d.FeeHeadId)
			.ToHashSet();

		var feeHeads = feeStructure.FeeStructureDetails
			.Select(detail => new StudentFeeHeadDto
			{
				FeeHeadId = detail.FeeHeadId,
				FeeHeadName = detail.FeeHead?.Name,
				CategoryName = detail.FeeHead?.FeeCategory?.Name,
				Amount = (decimal)detail.Amount,
				IsOptional = detail.IsOptional,
				IsOneTime = detail.IsOneTime,
				IsSelected = !detail.IsOptional || selectedFeeHeadIds.Contains(detail.FeeHeadId)
			})
			.ToList();

		return new StudentFeeStructureResponseDto
		{
			StudentFeeStructureId = feeStructure.Id,
			StandardFeeStructureId = feeStructure.Id,
			StudentId = studentId,
			StandardId = feeStructure.StandardId,
			StructureName = feeStructure.Name,
			FeeHeads = feeHeads
		};
	}
}
