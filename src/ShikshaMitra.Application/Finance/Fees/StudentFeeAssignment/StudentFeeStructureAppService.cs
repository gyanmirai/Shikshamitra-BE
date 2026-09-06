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
using ShikshaMitra.Domain.Finance.Policies;

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
	IRepository<ConcessionPolicy, Guid> concessionPolicyRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
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

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);
		var policySnapshot = await ValidateConcessionPolicySnapshotAsync(feeStructure, academicYear.Id);

		student.AssignFeeStructure(input.StandardFeeStructureId);
		var assignmentId = GuidGenerator.Create();
		await studentFeeStructureRepository.InsertAsync(
			new StudentFeeStructure(
				id: assignmentId,
				tenantId: CurrentTenant.Id,
				studentId: student.Id,
				feeStructureStandardId: feeStructure.Id,
				academicYearId: academicYear.Id,
				concessionPolicyId: policySnapshot.Policy.Id,
				concessionPolicyVersionId: policySnapshot.Version.Id,
				concessionFeeHeadScope: feeStructure.ConcessionFeeHeadScope,
				concessionApplicableFeeHeadIds: feeStructure.GetConcessionApplicableFeeHeadIds()),
			autoSave: false);

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var optionalDetails = BuildOptionalDetails(input.FeeHeadDetails);

		var dueFees = await dueFeeManager.GenerateForStudentAsync(
			student.Id, feeStructure, academicYear, input.SelectedOptionalFeeHeadIds, optionalDetails);

		await dueFeeRepository.InsertManyAsync(dueFees, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task<(ConcessionPolicy Policy, FinancePolicyVersion Version)> ValidateConcessionPolicySnapshotAsync(
		FeeStructure feeStructure, Guid academicYearId)
	{
		if (!feeStructure.ConcessionPolicyId.HasValue || !feeStructure.ConcessionPolicyVersionId.HasValue)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotAttached);
		var policy = await concessionPolicyRepository.FindAsync(feeStructure.ConcessionPolicyId.Value)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != ConcessionPolicyStatus.Active)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != academicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearMismatch);
		var version = await financePolicyVersionRepository.FindAsync(feeStructure.ConcessionPolicyVersionId.Value)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "concession" || version.PolicyId != policy.Id || version.AcademicYearId != academicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		return (policy, version);
	}
}
