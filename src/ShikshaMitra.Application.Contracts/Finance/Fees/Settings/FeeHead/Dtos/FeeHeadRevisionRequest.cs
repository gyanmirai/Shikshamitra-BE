using System;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadRevisionRequest : FeeHeadUpdateRequest
{
    public string Reason { get; set; }
}

public class FeeHeadImpactResponse
{
    public Guid FeeHeadId { get; set; }
    public bool IsUsed { get; set; }
    public System.Collections.Generic.List<string> Blockers { get; set; } = new();
}
