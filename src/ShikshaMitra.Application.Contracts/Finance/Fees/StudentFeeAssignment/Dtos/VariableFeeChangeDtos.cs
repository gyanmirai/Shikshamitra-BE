using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public record VariableFeeChangeInput(Guid AcademicYearId, Guid FeeHeadId, Guid RequestId,
    string ConcurrencyStamp, DateOnly EffectiveDate, DateOnly ServiceEndDate, decimal MonthlyRate,
    string Reason, string? PreviewToken = null);

public record VariableFeeChangeResult(decimal PreviousAmount, decimal NewAmount,
    List<StudentFeeAssignmentPreviewLineDto> PreviousInstallments,
    List<StudentFeeAssignmentPreviewLineDto> NewInstallments, string? PreviewToken = null);
