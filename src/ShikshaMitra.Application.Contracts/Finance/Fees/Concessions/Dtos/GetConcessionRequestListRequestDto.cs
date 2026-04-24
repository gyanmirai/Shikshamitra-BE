using ShikshaMitra.Finance.ConcessionRequests;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

public class GetConcessionRequestListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>Free-text search applied to student name / concession type name.</summary>
    public string? Filter { get; set; }

    /// <summary>Optional filter by student.</summary>
    public Guid? StudentId { get; set; }

    /// <summary>Optional filter by concession type.</summary>
    public Guid? ConcessionTypeId { get; set; }

    /// <summary>Optional filter by request status.</summary>
    public ConcessionRequestStatus? Status { get; set; }
}
