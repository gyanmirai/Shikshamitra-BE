using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
    private static List<FeeHeadConcessionInput> MapFeeHeadInputs(IEnumerable<FeeHeadConcessionDto> items) =>
        items.Select(i => new FeeHeadConcessionInput(i.FeeHeadId, (DiscountBasis)i.BasedOn, i.Amount)).ToList();

    private async Task<ConcessionRequestDetailDto> MapDetailAsync(ConcessionRequest request)
    {
        var student = await studentRepository.GetAsync(request.StudentId);
        var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
        var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id)) : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var requiredDocs = type.ConcessionTypeDocuments.Select(d => new ConcessionDocumentRequirementDto
        {
            DocumentId = d.Id, DocumentName = d.DocumentName,
            IsUploaded = request.ConcessionRequestDocuments.Any(rd => rd.DocumentId == d.Id),
        }).ToList();

        return new ConcessionRequestDetailDto
        {
            ConcessionRequestId = request.Id, StudentId = request.StudentId,
            StudentName = FormatStudentName(student), ConcessionTypeId = request.ConcessionTypeId,
            ConcessionTypeName = type.Name, Status = request.Status, StatusName = request.Status.ToString(),
            TotalConcessionAmount = request.GetTotalRequestedConcession(), Remarks = request.Remarks,
            BoardId = request.GetBoardId(), EducationMediumId = request.GetEducationMediumId(),
            StandardId = request.GetStandardId(), DivisionId = request.GetDivisionId(),
            FeeHeadConcessions = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
            {
                FeeHeadId = m.FeeHeadId, FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
                AnnualBaseAmount = m.PreConcessionFeeHeadAmount, BasedOn = (int)m.UserSpecifiedConcessionAmountType,
                Amount = m.UserSpecifiedConcessionAmountValue, DiscountAmount = m.FeeHeadConcessionAmount,
                NetAmount = m.PostConcessionFeeHeadAmount,
            }).ToList(),
            RequiredDocuments = requiredDocs,
            SubmittedDocuments = request.ConcessionRequestDocuments.Select(d => new ConcessionSubmittedDocumentDto
            {
                DocumentId = d.DocumentId,
                DocumentName = type.ConcessionTypeDocuments.FirstOrDefault(ct => ct.Id == d.DocumentId)?.DocumentName ?? string.Empty,
                Url = d.Url, Status = d.Status,
            }).ToList(),
            CreatedAt = request.CreationTime,
        };
    }

    private static StudentConcessionRequestItemDto MapStudentItem(
        ConcessionRequest request, ConcessionType? type, Dictionary<Guid, string> feeHeadMap)
    {
        return new StudentConcessionRequestItemDto
        {
            StudentConcessionTypeRequestId = request.Id, ConcessionTypeId = request.ConcessionTypeId,
            ConcessionTypeName = type?.Name ?? string.Empty, Status = request.Status,
            TotalAmount = request.GetTotalRequestedConcession(),
            FeeHeads = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
            {
                FeeHeadId = m.FeeHeadId, FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
                AnnualBaseAmount = m.PreConcessionFeeHeadAmount, BasedOn = (int)m.UserSpecifiedConcessionAmountType,
                Amount = m.UserSpecifiedConcessionAmountValue, DiscountAmount = m.FeeHeadConcessionAmount,
                NetAmount = m.PostConcessionFeeHeadAmount,
            }).ToList(),
            SubmittedDocuments = request.ConcessionRequestDocuments.Select(d => new ConcessionSubmittedDocumentDto
            {
                DocumentId = d.DocumentId,
                DocumentName = type?.ConcessionTypeDocuments.FirstOrDefault(ct => ct.Id == d.DocumentId)?.DocumentName ?? string.Empty,
                Url = d.Url, Status = d.Status,
            }).ToList(),
            CreatedAt = request.CreationTime, UpdatedAt = request.LastModificationTime,
            Message = request.ApproverMessage ?? request.RejectionReason,
            ApprovedAt = request.GetApprovedOn(),
        };
    }

    private static string FormatStudentName(Student? student) =>
        student == null ? string.Empty
            : string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

    private bool IsPrivilegedUser() =>
        CurrentUser.IsInRole("admin") || CurrentUser.IsInRole("Finance Officer");
}
