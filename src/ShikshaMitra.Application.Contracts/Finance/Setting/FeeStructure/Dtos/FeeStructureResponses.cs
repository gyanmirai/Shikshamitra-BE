using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure.Dtos;

public class FeeStructureCreateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

public class FeeStructureDetailsResponse : EntityDto<Guid>
{
    public string Name { get; set; }
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; }
    public Guid BoardId { get; set; }
    public string BoardName { get; set; }
    public Guid MediumId { get; set; }
    public string MediumName { get; set; }
    public Guid ReceiptBookId { get; set; }
    public string ReceiptBookName { get; set; }
    public int Status { get; set; }
}

public class FeeStructureListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeStructureDetailsResponse> Items { get; set; }
}

public class StandardFeeStructureResponse
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; }
    public List<StandardFeeStructureResponseItem> StandardFeeStructures { get; set; }
}

public class StandardFeeStructureResponseItem : EntityDto<Guid>
{
    public string Name { get; set; }
    public List<FeeHeadAssignmentResponse> FeeHeads { get; set; }
}

public class FeeHeadAssignmentResponse
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; }
    public string CategoryName { get; set; }
    public decimal Amount { get; set; }
    public bool IsOneTime { get; set; }
    public bool IsOptional { get; set; }
}
