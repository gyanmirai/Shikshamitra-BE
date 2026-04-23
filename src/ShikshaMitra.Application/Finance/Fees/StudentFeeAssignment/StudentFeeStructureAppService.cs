using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

// Alias to resolve the clash between the 'FeeStructure' namespace and the 'FeeStructure' class.
using FeeStructureEntity = ShikshaMitra.Finance.Fees.Settings.FeeStructures.FeeStructure;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public class StudentFeeStructureAppService : ApplicationService, IStudentFeeStructureAppService
{
	private readonly IRepository<StudentFeeStructure, Guid> _studentFeeStructureRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<FeeStructureEntity, Guid> _feeStructureRepository;
	private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;
	private readonly IRepository<Charge, Guid> _chargeRepository;

	public StudentFeeStructureAppService(
		IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<FeeStructureEntity, Guid> feeStructureRepository,
		IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeCategory, Guid> feeCategoryRepository,
		IRepository<Charge, Guid> chargeRepository)
	{
		_studentFeeStructureRepository = studentFeeStructureRepository;
		_studentRepository = studentRepository;
		_feeStructureRepository = feeStructureRepository;
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

		// 2. Load the target FeeStructure (per-standard, replaces old FeeStructureStandard)
		var feeStructure = await _feeStructureRepository.GetAsync(input.StandardFeeStructureId);

		// 3. Validate structure belongs to the student's standard
		if (feeStructure.StandardId != student.StandardId.Value)
		{
			throw new BusinessException("ShikshaMitra:FeeStructureStandardMismatch")
				.WithData("StudentStandardId", student.StandardId.Value)
				.WithData("StructureStandardId", feeStructure.StandardId);
		}

		// 4. Check if student already has a fee structure assigned
		var existingAssignment = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == input.StudentId);

		if (existingAssignment != null)
		{
			// 5. Block change when approved charges already exist
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

		await _studentFeeStructureRepository.InsertAsync(studentFeeStructure, autoSave: true);

		return await GetByStudentIdAsync(input.StudentId);
	}

	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId)
	{
		var studentFeeStructure = await _studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == studentId);

		if (studentFeeStructure == null)
		{
			throw new BusinessException(
				"ShikshaMitra:StudentFeeStructureNotFound",
				"Student FeeStructure is Not Found")
				.WithData("StudentId", studentId);
		}

		// Load the assigned FeeStructure
		var feeStructure = await _feeStructureRepository.GetAsync(
			studentFeeStructure.FeeStructureStandardId);

		var detailsQuery = await _feeStructureDetailRepository.GetQueryableAsync();
		var feeHeadsQuery = await _feeHeadRepository.GetQueryableAsync();
		var categoriesQuery = await _feeCategoryRepository.GetQueryableAsync();

		// FeeStructureDetail.FeeStructureId is the FK to FeeStructure
		var feeHeadsList = (from detail in detailsQuery
							join fh in feeHeadsQuery on detail.FeeHeadId equals fh.Id
							join cat in categoriesQuery on fh.FeeCategoryId equals cat.Id
							where detail.FeeStructureId == studentFeeStructure.FeeStructureStandardId
							select new StudentFeeHeadDto
							{
								FeeHeadId = detail.FeeHeadId,
								FeeHeadName = fh.Name,
								CategoryName = cat.Name,
								Amount = (decimal)detail.Amount,
								IsOptional = detail.IsOptional,
								IsOneTime = detail.IsOneTime,
								IsSelected = true
							}).ToList();

		return new StudentFeeStructureResponseDto
		{
			StudentFeeStructureId = studentFeeStructure.Id,
			StandardFeeStructureId = studentFeeStructure.FeeStructureStandardId,
			StudentId = studentFeeStructure.StudentId,
			StandardId = studentFeeStructure.StandardId,
			StructureName = feeStructure.Name,
			FeeHeads = feeHeadsList
		};
	}
}
