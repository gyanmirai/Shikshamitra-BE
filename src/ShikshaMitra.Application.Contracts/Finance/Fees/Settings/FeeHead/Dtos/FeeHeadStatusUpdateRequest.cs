namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadStatusUpdateRequest
{
    public bool IsActive { get; set; }
    public string DeactivationReason { get; set; }
}
