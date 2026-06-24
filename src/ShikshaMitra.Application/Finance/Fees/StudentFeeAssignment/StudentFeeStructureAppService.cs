using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public partial class StudentFeeStructureAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	DueFeeManager dueFeeManager,
	FinanceScopeAccessService financeScopeAccessService
) : ApplicationService, IStudentFeeStructureAppService
{
	public async Task AssignAsync(AssignStudentFeeStructureDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId)
			?? throw new BusinessException("Student with given id does not exist.");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);

		if (student.FeeStructureId != null)
			throw new BusinessException(
				"Fee structure is already assigned to the student. Please unassign the existing fee structure before assigning a new one.");

		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId)
			?? throw new BusinessException("Fee structure with given id does not exist.");

		student.AssignFeeStructure(input.StandardFeeStructureId);

		await studentFeeStructureRepository.InsertAsync(
			new StudentFeeStructure(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				studentId: student.Id,
				feeStructureStandardId: feeStructure.Id),
			autoSave: false);

		var metadata    = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var optionalDetails = BuildOptionalDetails(input.FeeHeadDetails);

		var dueFees = await dueFeeManager.GenerateForStudentAsync(
			student.Id, feeStructure, academicYear, input.SelectedOptionalFeeHeadIds, optionalDetails);

		await dueFeeRepository.InsertManyAsync(dueFees, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
