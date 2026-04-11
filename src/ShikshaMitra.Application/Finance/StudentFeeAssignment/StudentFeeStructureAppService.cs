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
using ShikshaMitra.Finance.Charges;
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
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<FeeStructureStandard, Guid> _feeStructureStandardRepository;
	private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;
	private readonly IRepository<Charge, Guid> _chargeRepository;

	public StudentFeeStructureAppService(
		IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<FeeStructureStandard, Guid> feeStructureStandardRepository,
		IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeCategory, Guid> feeCategoryRepository,
		IRepository<Charge, Guid> chargeRepository)
	{
		_studentFeeStructureRepository = studentFeeStructureRepository;
		_studentRepository = studentRepository;
		_feeStructureStandardRepository = feeStructureStandardRepository;
		_feeStructureDetailRepository = feeStructureDetailRepository;
		_feeHeadRepository = feeHeadRepository;
		_feeCategoryRepository = feeCategoryRepository;
		_chargeRepository = chargeRepository;
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

		// 2. Load fee structure standard
		var feeStructureStandard = await _feeStructureStandardRepository.GetAsync(input.StandardFeeStructureId);

		// 3. Validate structure belongs to student's standard
		if (feeStructureStandard.StandardId != student.StandardId.Value)
		{
			throw new BusinessException("ShikshaMitra:FeeStructureStandardMismatch")
				.WithData("StudentStandardId", student.StandardId.Value)
				.WithData("StructureStandardId", feeStructureStandard.StandardId);
		}

		// 4. Check if student already has fee structure assigned
		var existingAssignment = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == input.StudentId);

		if (existingAssignment != null)
		{
			// 5. Add protection: If payment exists (Approved charges), block structure update
			var hasPayments = await _chargeRepository.AnyAsync(x =>
				x.StudentId == input.StudentId &&
				x.StandardId == existingAssignment.StandardId &&
				x.Status == ChargeStatus.Approved);

			if (hasPayments)
			{
				throw new BusinessException("ShikshaMitra:CannotChangeFeeStructureWhenPaymentExists")
					.WithData("StudentId", input.StudentId);
			}

			// Update existing assignment
			existingAssignment.UpdateFeeStructure(input.StandardFeeStructureId);
			await _studentFeeStructureRepository.UpdateAsync(existingAssignment, autoSave: true);
			return await GetByStudentIdAsync(input.StudentId);
		}

		// 6. Create new assignment
		var studentFeeStructure = new StudentFeeStructure(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.StudentId,
			student.StandardId.Value,
			input.StandardFeeStructureId);

		// 7. Save to database
		await _studentFeeStructureRepository.InsertAsync(studentFeeStructure, autoSave: true);

		// 8. Return response
		return await GetByStudentIdAsync(input.StudentId);
	}

	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		var studentFeeStructure = await _studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);

		if (studentFeeStructure == null)
		{
			throw new BusinessException("ShikshaMitra:StudentFeeStructureNotFound", "Student FeeStructure is Not Found")
				.WithData("StudentId", studentId);
		}

		// Build DTO dynamically using joins
		var feeStructureStandard = await _feeStructureStandardRepository.GetAsync(studentFeeStructure.FeeStructureStandardId);

		var detailsQuery = await _feeStructureDetailRepository.GetQueryableAsync();
		var feeHeadsQuery = await _feeHeadRepository.GetQueryableAsync();
		var categoriesQuery = await _feeCategoryRepository.GetQueryableAsync();

		var feeHeadsList = (from detail in detailsQuery
							join fh in feeHeadsQuery on detail.FeeHeadId equals fh.Id
							join cat in categoriesQuery on fh.FeeCategoryId equals cat.Id
							where detail.FeeStructureStandardId == studentFeeStructure.FeeStructureStandardId
							select new StudentFeeHeadDto
							{
								FeeHeadId = detail.FeeHeadId,
								FeeHeadName = fh.Name,
								CategoryName = cat.Name,
								Amount = detail.Amount,
								IsOptional = detail.IsOptional,
								IsOneTime = detail.IsOneTime,
								IsSelected = true // By default all are selected in this view, 
												  // selection logic can be refined if needed
							}).ToList();

		return new StudentFeeStructureResponseDto
		{
			StudentFeeStructureId = studentFeeStructure.Id,
			StandardFeeStructureId = studentFeeStructure.FeeStructureStandardId,
			StudentId = studentFeeStructure.StudentId,
			StandardId = studentFeeStructure.StandardId,
			StructureName = feeStructureStandard.Name,
			FeeHeads = feeHeadsList
		};
	}
}
