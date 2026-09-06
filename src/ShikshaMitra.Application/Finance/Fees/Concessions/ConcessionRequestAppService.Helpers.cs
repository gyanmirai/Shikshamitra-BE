using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Policies;
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
        var policyVersion = await financePolicyVersionRepository.GetAsync(request.ConcessionPolicyVersionId);
        var policy = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(policyVersion.SnapshotJson)
            ?? throw new Volo.Abp.BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);

        var requiredDocs = type.ConcessionTypeDocuments.Select(d => new ConcessionDocumentRequirementDto
        {
            DocumentId = d.Id, DocumentName = d.DocumentName,
			IsRequired = d.IsRequired,
			MaxFileSizeBytes = d.MaxFileSizeBytes,
			AcceptedFormats = d.AcceptedFormats.Select(format => format.DocumentType).ToList(),
            IsUploaded = request.ConcessionRequestDocuments.Any(rd => rd.DocumentId == d.Id),
        }).ToList();

        return new ConcessionRequestDetailDto
        {
            ConcessionRequestId = request.Id, StudentId = request.StudentId,
            StudentFeeStructureId = request.StudentFeeStructureId,
            ConcessionPolicyId = request.ConcessionPolicyId,
            ConcessionPolicyVersionId = request.ConcessionPolicyVersionId,
            ConcessionPolicyName = policy.PolicyName,
            ConcessionPolicyCode = policy.PolicyCode,
            ConcessionPolicyVersion = policy.Version,
            StudentName = FormatStudentName(student), ConcessionTypeId = request.ConcessionTypeId,
            ConcessionTypeName = type.Name, Status = request.Status, StatusName = request.Status.ToString(),
            TotalConcessionAmount = request.GetTotalRequestedConcession(),
            TotalRequestedConcessionAmount = request.GetTotalRequestedConcession(),
			TotalApprovedConcessionAmount = request.GetTotalApprovedConcession(),
			TotalReversedConcessionAmount = request.GetTotalReversedConcession(),
			NetApprovedConcessionAmount = request.GetNetApprovedConcession(),
            Remarks = request.Remarks,
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
			DocumentSubmissionState = requiredDocs.All(x => x.IsUploaded) ? "Complete" : "DocumentsPending",
			MissingDocumentIds = requiredDocs.Where(x => !x.IsUploaded).Select(x => x.DocumentId).ToList(),
            CreatedAt = request.CreationTime,
        };
    }

	private async Task<List<ConcessionRequestDocument>> ValidateAndBuildDocumentsAsync(
		ConcessionRequest request,
		IEnumerable<UploadConcessionDocumentEntryDto> entries)
	{
		var submitted = entries.ToList();
		if (submitted.Select(x => x.DocumentId).Distinct().Count() != submitted.Count)
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		var requirements = type.ConcessionTypeDocuments.ToDictionary(x => x.Id);
		var result = new List<ConcessionRequestDocument>(submitted.Count);

		foreach (var entry in submitted)
		{
			if (entry.Status != 1 || string.IsNullOrWhiteSpace(entry.Url)
				|| !requirements.TryGetValue(entry.DocumentId, out var requirement))
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

			const string fileUrlPrefix = "/api/files/";
			var fileUrl = entry.Url.Trim();
			if (!fileUrl.StartsWith(fileUrlPrefix, StringComparison.Ordinal)
				|| fileUrl.Length == fileUrlPrefix.Length)
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

			var storedFileName = Uri.UnescapeDataString(fileUrl[fileUrlPrefix.Length..]);
			var blob = await fileStorageAppService.GetAsync(storedFileName);
			var extension = Path.GetExtension(blob.FileName).ToLowerInvariant();
			var documentType = extension switch
			{
				".jpg" => ShikshaMitra.Common.DocumentType.Jpg,
				".jpeg" => ShikshaMitra.Common.DocumentType.Jpeg,
				".png" => ShikshaMitra.Common.DocumentType.Png,
				".docx" => ShikshaMitra.Common.DocumentType.Docx,
				".pdf" => ShikshaMitra.Common.DocumentType.Pdf,
				".xlsx" => ShikshaMitra.Common.DocumentType.Xlsx,
				_ => throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission),
			};

			if (!requirement.AcceptedFormats.Any(x => x.DocumentType == documentType)
				|| blob.Content.LongLength > requirement.MaxFileSizeBytes)
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission)
					.WithData("DocumentId", requirement.Id);

			try
			{
				FileValidationService.ValidateDocumentFileSignature(blob.Content, extension);
			}
			catch (Volo.Abp.BusinessException)
			{
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission)
					.WithData("DocumentId", requirement.Id);
			}

			result.Add(new ConcessionRequestDocument(
				guidGenerator.Create(), CurrentTenant.Id, request.Id,
				entry.DocumentId, entry.Url.Trim(), entry.Status));
		}

		return result;
	}

    private static StudentConcessionRequestItemDto MapStudentItem(
        ConcessionRequest request, ConcessionType? type, Dictionary<Guid, string> feeHeadMap)
    {
        return new StudentConcessionRequestItemDto
        {
            StudentConcessionTypeRequestId = request.Id, ConcessionTypeId = request.ConcessionTypeId,
            ConcessionTypeName = type?.Name ?? string.Empty, Status = request.Status,
            TotalAmount = request.GetTotalRequestedConcession(),
            TotalRequestedAmount = request.GetTotalRequestedConcession(),
            TotalApprovedAmount = request.GetTotalApprovedConcession(),
            TotalReversedAmount = request.GetTotalReversedConcession(),
            NetApprovedAmount = request.GetNetApprovedConcession(),
            FeeHeads = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
            {
                FeeHeadId = m.FeeHeadId, FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
                AnnualBaseAmount = m.PreConcessionFeeHeadAmount, BasedOn = (int)m.UserSpecifiedConcessionAmountType,
                Amount = m.UserSpecifiedConcessionAmountValue, DiscountAmount = m.FeeHeadConcessionAmount,
                NetAmount = m.PostConcessionFeeHeadAmount,
            }).ToList(),
            RequiredDocuments = type?.ConcessionTypeDocuments.Select(d => new ConcessionDocumentRequirementDto
            {
                DocumentId = d.Id, DocumentName = d.DocumentName,
				IsRequired = d.IsRequired,
				MaxFileSizeBytes = d.MaxFileSizeBytes,
				AcceptedFormats = d.AcceptedFormats.Select(format => format.DocumentType).ToList(),
                IsUploaded = request.ConcessionRequestDocuments.Any(rd => rd.DocumentId == d.Id),
                Url = request.ConcessionRequestDocuments.FirstOrDefault(rd => rd.DocumentId == d.Id)?.Url
            }).ToList() ?? [],
            CreatedAt = request.CreationTime, UpdatedAt = request.LastModificationTime,
            Message = request.ApproverMessage ?? request.RejectionReason,
            ApprovedAt = request.GetApprovedOn(),
        };
    }

    private static string FormatStudentName(Student? student) =>
        student == null ? string.Empty
            : string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

}
