using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentFamilyInfoAppService : StudentRegistrationBaseAppService,
    IStudentFamilyInfoAppService
{
	private readonly IStudentRepository studentRepository;
	private readonly IRepository<StudentFamilyDetail, Guid> familyDetailRepository;
	private readonly EntityValidationService validationService;
	private readonly StudentRegistrationMappingHelper mappingHelper;
	private readonly StudentScopeAccessService studentScopeAccessService;

	public StudentFamilyInfoAppService(
		IStudentRepository studentRepository,
		IRepository<StudentFamilyDetail, Guid> familyDetailRepository,
		EntityValidationService validationService,
		StudentRegistrationMappingHelper mappingHelper,
		StudentScopeAccessService studentScopeAccessService
	) : base(studentRepository, validationService, studentScopeAccessService)
	{
		this.studentRepository = studentRepository;
		this.familyDetailRepository = familyDetailRepository;
		this.validationService = validationService;
		this.mappingHelper = mappingHelper;
		this.studentScopeAccessService = studentScopeAccessService;
	}

    public async Task<StudentFamilyInfoDto> GetFamilyInfoAsync(Guid studentId)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(GetFamilyInfoAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForFamilyInfo,
            includeStudentRecords: true);

        var familyDetail = await familyDetailRepository.FindAsync(studentId);

        var result = new StudentFamilyInfoDto
        {
            MainContactRelationship = StudentFamilyContactSelection.Resolve(student, familyDetail),
            GuardianRelationship = student.GetProperty<string>(StudentFamilyContactSelection.GuardianRelationshipKey),
            StudentId = student.Id,
            Status = student.Status,
            FatherDetails = familyDetail?.FatherDetails != null
                ? mappingHelper.MapParentGuardianDetailsToDto(familyDetail.FatherDetails) : null,
            MotherDetails = familyDetail?.MotherDetails != null
                ? mappingHelper.MapParentGuardianDetailsToDto(familyDetail.MotherDetails) : null,
            GuardianDetails = familyDetail?.GuardianDetails != null
                ? mappingHelper.MapParentGuardianDetailsToDto(familyDetail.GuardianDetails) : null,
            SiblingStudents = new List<SiblingStudentDto>()
        };

        if (familyDetail?.SiblingStudentIds != null && familyDetail.SiblingStudentIds.Any())
        {
            var siblings = await studentRepository
                .GetListAsync(s => familyDetail.SiblingStudentIds.Contains(s.Id));
            var scopedSiblingIds = (await studentScopeAccessService.ApplyCurrentUserScopeAsync(
                    siblings.AsQueryable()))
                .Select(s => s.Id)
                .ToHashSet();

            result.SiblingStudents = siblings
                .Where(s => scopedSiblingIds.Contains(s.Id))
                .Select(s => new SiblingStudentDto
                {
                    StudentId = s.Id,
                    FullName = $"{s.FirstName} {s.MiddleName} {s.LastName}".Trim(),
                    StandardName = s.Standard?.Name,
                    DivisionName = s.Division?.Name,
                    GrNo = s.GrNo
                })
                .ToList();
        }

        return result;
    }

    public async Task<StudentFamilyInfoDto> UpdateFamilyInfoAsync(Guid studentId, UpdateFamilyInfoDto input)
    {
        var student = await GetScopedRegistrationStudentAsync(
            studentId,
            nameof(UpdateFamilyInfoAsync),
            StudentRegistrationErrorCodes.InvalidStudentStatusForFamilyUpdate,
            forProfileWrite: true);

        await validationService.ValidateSiblingStudentsExistAsync(input.SiblingStudentIds);
        if (input.SiblingStudentIds != null)
        {
            await studentScopeAccessService.EnsureCanAccessStudentsAsync(
                input.SiblingStudentIds,
                nameof(UpdateFamilyInfoAsync));
        }

        var fatherDetails = input.FatherDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.FatherDetails) : null;
        var motherDetails = input.MotherDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.MotherDetails) : null;
        var guardianDetails = input.GuardianDetails != null
            ? mappingHelper.MapDtoToParentGuardianDetails(input.GuardianDetails) : null;

        var existingFamilyDetail = await familyDetailRepository.FindAsync(studentId);
        var selectsGuardian = input.MainContactRelationship == "guardian" || input.MainContactRelationship == null &&
            string.IsNullOrEmpty(student.GetProperty<string>(StudentFamilyContactSelection.SelectionKey)) &&
            !StudentFamilyContactSelection.Present(fatherDetails) && !StudentFamilyContactSelection.Present(motherDetails) &&
            StudentFamilyContactSelection.Present(guardianDetails);
        var confirmGuardian = selectsGuardian &&
            StudentFamilyContactSelection.Resolve(student, existingFamilyDetail) != "guardian";
        if (confirmGuardian && (input.GuardianResponsibilityConfirmed != true || !CurrentUser.Id.HasValue || CurrentUser.Id == Guid.Empty))
            throw new BusinessException("Students:Family:GuardianResponsibilityConfirmationRequired",
                "Confirm that neither parent is handling school-contact responsibility before choosing Guardian.");
        var familyDetail = existingFamilyDetail ?? new StudentFamilyDetail(student.Id, student.TenantId);
        var shouldCreateFamilyDetail = existingFamilyDetail == null;
        // Omitted sibling IDs are a contact edit, never an instruction to remove sibling links.
        familyDetail.UpdateFamilyInformation(fatherDetails, motherDetails, guardianDetails, input.SiblingStudentIds ?? familyDetail.SiblingStudentIds);
        if (input.MainContactRelationship != null)
            StudentFamilyContactSelection.Select(student, input.MainContactRelationship, familyDetail);
        if (confirmGuardian)
        {
            student.SetProperty("SchoolContactGuardianResponsibilityConfirmedBy", CurrentUser.Id!.Value.ToString("D"));
            student.SetProperty("SchoolContactGuardianResponsibilityConfirmedAt", Clock.Now.ToUniversalTime().ToString("O"));
        }
        if (input.GuardianRelationship != null)
        {
            if (input.GuardianRelationship.Trim().Length > 128)
                throw new BusinessException("Students:Family:GuardianRelationshipInvalid");
            student.SetProperty(StudentFamilyContactSelection.GuardianRelationshipKey, input.GuardianRelationship.Trim());
        }
        await studentRepository.UpdateAsync(student, autoSave: false);

        if (shouldCreateFamilyDetail)
        {
            await familyDetailRepository.InsertAsync(familyDetail, autoSave: true);
        }
        else
        {
            await familyDetailRepository.UpdateAsync(familyDetail, autoSave: true);
        }

        return await GetFamilyInfoAsync(studentId);
    }
}
