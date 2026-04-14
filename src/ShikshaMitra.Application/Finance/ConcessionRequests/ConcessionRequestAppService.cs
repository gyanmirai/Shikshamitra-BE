using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;
using ShikshaMitra.Finance.Setting.ConcessionType;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Core lifecycle operations: create, read, update, submit, and delete Draft requests.
///
/// <list type="bullet">
///   <item>Wizard steps (3–6) → <see cref="ConcessionRequestWizardAppService"/></item>
///   <item>Approval workflow → <see cref="ConcessionRequestApprovalAppService"/></item>
///   <item>Student / document queries → <see cref="ConcessionRequestQueryAppService"/></item>
/// </list>
/// </summary>
[RemoteService(false)]
public class ConcessionRequestAppService(
	IConcessionRequestRepository concessionRequestRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<Student, Guid> studentRepository,
	ConcessionRequestManager concessionRequestManager)
	: ShikshaMitraAppService, IConcessionRequestAppService
{
    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the anchor request enriched with the student's enrollment context
    /// and all sibling requests (same student, same status) as
    /// <c>selectedConcessionTypes</c>.
    /// </summary>
    public virtual async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id)
    {
        // 1. Load the anchor request
        var anchor = await concessionRequestRepository.GetAsync(id);

        // 2. Load the student to source the enrollment context fields
        var student = await studentRepository.GetAsync(anchor.StudentId);

        // 3. Load all requests for this student that share the anchor's status
        //    — they form the logical "batch" that was created / updated together.
        var siblingRequests = await concessionRequestRepository.GetListAsync(
            skipCount: 0,
            maxResultCount: int.MaxValue,
            studentId: anchor.StudentId,
            status: anchor.Status);

        // 4. Build the aggregated response
        var detail = new ConcessionRequestDetailDto
        {
            ConcessionRequestId   = anchor.Id,
            BoardId               = student.AcademicBoardId,
            EducationMediumId     = student.AcademicMediumId,
            StandardId            = student.StandardId,
            DivisionId            = student.DivisionId,
            StudentId             = anchor.StudentId,
            Status                = ConcessionRequestDetailDto.MapStatus(anchor.Status),
            CreatedAt             = anchor.CreationTime,
            UpdatedAt             = anchor.LastModificationTime ?? anchor.CreationTime,
            SelectedConcessionTypes = siblingRequests
                .Select(r => new SelectedConcessionTypeDto
                {
                    ConcessionTypeId                 = r.ConcessionTypeId,
                    ConcessionRequestConcessionTypeId = r.Id,
                })
                .ToList(),
        };

        return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(detail);
    }

    public virtual async Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(
        GetConcessionRequestListRequestDto input)
    {
        var totalCount = await concessionRequestRepository.GetCountAsync(
            studentId: input.StudentId,
            concessionTypeId: input.ConcessionTypeId,
            status: input.Status,
            filter: input.Filter);

        var list = await concessionRequestRepository.GetListAsync(
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount,
            sorting: input.Sorting,
            studentId: input.StudentId,
            concessionTypeId: input.ConcessionTypeId,
            status: input.Status,
            filter: input.Filter);

        return new PagedResultDto<ConcessionRequestDto>(
            totalCount,
            ObjectMapper.Map<List<ConcessionRequest>, List<ConcessionRequestDto>>(list));
    }

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates one <see cref="ConcessionRequest"/> per concession-type ID (Steps 1 + 2).
    /// Validates student existence and enrollment context before persisting any records.
    /// </summary>
    public async Task CreateAsync(CreateConcessionRequestDto input)
    {
        var student = await studentRepository.GetAsync(input.StudentId);

        ValidateStudentEnrollmentContext(student,
            input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

        await EnsureAllConcessionTypesExistAsync(input.ConcessionTypeIds);

        await concessionRequestManager.CreateAsync(
            CurrentTenant.Id,
            input.StudentId,
            input.ConcessionTypeIds);
    }

    /// <summary>
    /// Replaces the student's Pending concession-type selection in full.
    /// The <paramref name="id"/> must be Pending and must belong to the student
    /// specified in <paramref name="input"/>. The manager performs a batch replace:
    /// Pending requests for types removed from the list are deleted; newly selected
    /// types get new Draft requests created for them.
    /// </summary>
    public virtual async Task<ConcessionRequestDto> UpdateAsync(Guid id, UpdateConcessionRequestDto input)
    {
        var anchor = await concessionRequestRepository.GetAsync(id);

        if (anchor.Status != ConcessionRequestStatus.Pending)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonPending)
                .WithData("RequestId", id)
                .WithData("Status", anchor.Status.ToString());
        }

        if (anchor.StudentId != input.StudentId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentRequestOwnershipMismatch)
                .WithData("RequestId", id)
                .WithData("AnchorStudentId", anchor.StudentId)
                .WithData("InputStudentId", input.StudentId);
        }

        var student = await studentRepository.GetAsync(input.StudentId);

        ValidateStudentEnrollmentContext(student,
            input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

        await EnsureAllConcessionTypesExistAsync(input.ConcessionTypeIds);

        var result = await concessionRequestManager.UpdateAsync(
            CurrentTenant.Id,
            input.StudentId,
            input.ConcessionTypeIds);

        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(result);
    }

    /// <summary>Submits a Draft request for approval (Draft → Pending).</summary>
    public virtual async Task<ConcessionRequestDto> SubmitAsync(Guid id)
    {
        var entity = await concessionRequestRepository.GetWithMappingsAsync(id);
        entity = await concessionRequestManager.SubmitAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    /// <summary>Deletes a Draft ConcessionRequest.</summary>
    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await concessionRequestRepository.GetAsync(id);

        if (entity.Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotDeleteNonDraft)
                .WithData("Status", entity.Status.ToString());
        }

        await concessionRequestRepository.DeleteAsync(id);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against stale UI state: verifies that the four enrollment context
    /// identifiers submitted by the caller match the student's record.
    /// </summary>
    private static void ValidateStudentEnrollmentContext(
        Student student,
        Guid boardId,
        Guid educationMediumId,
        Guid standardId,
        Guid divisionId)
    {
        if (student.AcademicBoardId != boardId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentBoardMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedBoardId", boardId)
                .WithData("ActualBoardId", student.AcademicBoardId);
        }

        if (student.AcademicMediumId != educationMediumId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentMediumMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedMediumId", educationMediumId)
                .WithData("ActualMediumId", student.AcademicMediumId);
        }

        if (student.StandardId != standardId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentStandardMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedStandardId", standardId)
                .WithData("ActualStandardId", student.StandardId);
        }

        if (student.DivisionId != divisionId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentDivisionMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedDivisionId", divisionId)
                .WithData("ActualDivisionId", student.DivisionId);
        }
    }

    /// <summary>
    /// Validates all concession-type IDs up front so the operation either
    /// succeeds entirely or fails before any data is written.
    /// </summary>
    private async Task EnsureAllConcessionTypesExistAsync(IEnumerable<Guid> typeIds)
    {
        foreach (var typeId in typeIds)
        {
            var exists = await concessionTypeRepository.AnyAsync(t => t.Id == typeId);
            if (!exists)
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeNotFound)
                    .WithData("ConcessionTypeId", typeId);
            }
        }
    }
}
