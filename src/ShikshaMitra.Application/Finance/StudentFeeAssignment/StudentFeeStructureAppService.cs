using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment;
using ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Setting.FeeStructure;
using ShikshaMitra.Finance.StudentFeeAssignment;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public class StudentFeeStructureAppService : ApplicationService, IStudentFeeStructureAppService
{
	private readonly IRepository<StudentFeeStructure, Guid> _studentFeeStructureRepository;
	private readonly IRepository<StudentFeeHead, Guid> _studentFeeHeadRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<FeeStructureStandard, Guid> _feeStructureStandardRepository;
	private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;

	public StudentFeeStructureAppService(
		IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
		IRepository<StudentFeeHead, Guid> studentFeeHeadRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<FeeStructureStandard, Guid> feeStructureStandardRepository,
		IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeCategory, Guid> feeCategoryRepository)
	{
		_studentFeeStructureRepository = studentFeeStructureRepository;
		_studentFeeHeadRepository = studentFeeHeadRepository;
		_studentRepository = studentRepository;
		_feeStructureStandardRepository = feeStructureStandardRepository;
		_feeStructureDetailRepository = feeStructureDetailRepository;
		_feeHeadRepository = feeHeadRepository;
		_feeCategoryRepository = feeCategoryRepository;
	}

	public async Task<StudentFeeStructureResponseDto> AssignAsync(AssignStudentFeeStructureDto input)
	{
		// 1. Validate student exists
		var student = await _studentRepository.GetAsync(input.StudentId);

		if (student.StandardId == null)
		{
			throw new BusinessException("ShikshaMitra:StudentHasNoStandard")
				.WithData("StudentId", input.StudentId);
		}

		// 2. Check if student already has fee structure assigned - if yes, delete it to allow update
		var existingAssignment = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == input.StudentId);

		if (existingAssignment != null)
		{
			// Delete existing assignment to allow update
			// This will cascade delete all StudentFeeHeads due to the foreign key relationship
			await _studentFeeStructureRepository.DeleteAsync(existingAssignment, autoSave: true);
		}

		// 3. Load fee structure standard with details
		var feeStructureStandardQuery = await _feeStructureStandardRepository
			.WithDetailsAsync(x => x.FeeStructureDetails);

		var feeStructureStandard = await AsyncExecuter.FirstOrDefaultAsync(
			feeStructureStandardQuery.Where(x => x.Id == input.StandardFeeStructureId));

		if (feeStructureStandard == null)
		{
			throw new BusinessException("ShikshaMitra:FeeStructureStandardNotFound")
				.WithData("StandardFeeStructureId", input.StandardFeeStructureId);
		}

		// 4. Validate structure belongs to student's standard
		if (feeStructureStandard.StandardId != student.StandardId.Value)
		{
			throw new BusinessException("ShikshaMitra:FeeStructureStandardMismatch")
				.WithData("StudentStandardId", student.StandardId.Value)
				.WithData("StructureStandardId", feeStructureStandard.StandardId);
		}

		// 5. Load fee heads and categories for snapshot
		var feeHeadIds = feeStructureStandard.FeeStructureDetails
			.Select(x => x.FeeHeadId)
			.Distinct()
			.ToList();

		var feeHeads = await _feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
		var feeHeadDict = feeHeads.ToDictionary(x => x.Id, x => x);

		var categoryIds = feeHeads.Select(x => x.FeeCategoryId).Distinct().ToList();
		var categories = await _feeCategoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
		var categoryDict = categories.ToDictionary(x => x.Id, x => x.Name);

		// 6. Validate selected optional fee heads exist in structure
		var optionalFeeHeadIds = feeStructureStandard.FeeStructureDetails
			.Where(x => x.IsOptional)
			.Select(x => x.FeeHeadId)
			.ToList();

		var invalidOptionalIds = input.SelectedOptionalFeeHeadIds
			.Where(x => !optionalFeeHeadIds.Contains(x))
			.ToList();

		if (invalidOptionalIds.Any())
		{
			throw new BusinessException("ShikshaMitra:InvalidOptionalFeeHeadSelected")
				.WithData("InvalidFeeHeadIds", string.Join(", ", invalidOptionalIds));
		}

		// 7. Create student fee structure (snapshot)
		var studentFeeStructure = new StudentFeeStructure(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.StudentId,
			student.StandardId.Value,
			input.StandardFeeStructureId,
			feeStructureStandard.Name);

		// 8. Create student fee heads (snapshot with selection logic)
		foreach (var detail in feeStructureStandard.FeeStructureDetails)
		{
			var feeHead = feeHeadDict.ContainsKey(detail.FeeHeadId)
				? feeHeadDict[detail.FeeHeadId]
				: null;

			var categoryName = feeHead != null && categoryDict.ContainsKey(feeHead.FeeCategoryId)
				? categoryDict[feeHead.FeeCategoryId]
				: string.Empty;

			// Determine if this fee head is selected:
			// - Mandatory fee heads are always selected
			// - Optional fee heads are selected only if in the input list
			var isSelected = !detail.IsOptional ||
							 input.SelectedOptionalFeeHeadIds.Contains(detail.FeeHeadId);

			var studentFeeHead = new StudentFeeHead(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				studentFeeStructure.Id,
				feeStructureStandard.Id,
				detail.FeeHeadId,
				feeHead?.Name ?? string.Empty,
				categoryName,
				detail.Amount,
				detail.IsOptional,
				detail.IsOneTime,
				isSelected);

			studentFeeStructure.AddFeeHead(studentFeeHead);
		}

		// 9. Save to database
		await _studentFeeStructureRepository.InsertAsync(studentFeeStructure, autoSave: true);

		// 10. Return response
		return await BuildResponseDto(studentFeeStructure);
	}

	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		var query = await _studentFeeStructureRepository.WithDetailsAsync(x => x.StudentFeeHeads);

		var studentFeeStructure = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(x => x.StudentId == studentId));

		if (studentFeeStructure == null)
		{
			throw new BusinessException("ShikshaMitra:StudentFeeStructureNotFound", "Student FeeStructure is Not Found")
				.WithData("StudentId", studentId);
		}

		return await BuildResponseDto(studentFeeStructure);
	}

	private async Task<StudentFeeStructureResponseDto> BuildResponseDto(StudentFeeStructure studentFeeStructure)
	{
		var response = new StudentFeeStructureResponseDto
		{
			StudentFeeStructureId = studentFeeStructure.Id,
			StandardFeeStructureId = studentFeeStructure.FeeStructureStandardId,
			StudentId = studentFeeStructure.StudentId,
			StandardId = studentFeeStructure.StandardId,
			StructureName = studentFeeStructure.StructureName,
			FeeHeads = studentFeeStructure.StudentFeeHeads.Select(fh => new StudentFeeHeadDto
			{
				FeeHeadId = fh.FeeHeadId,
				FeeHeadName = fh.FeeHeadName,
				CategoryName = fh.CategoryName,
				Amount = fh.Amount,
				IsOptional = fh.IsOptional,
				IsOneTime = fh.IsOneTime,
				IsSelected = fh.IsSelected
			}).ToList()
		};

		return await Task.FromResult(response);
	}
}
