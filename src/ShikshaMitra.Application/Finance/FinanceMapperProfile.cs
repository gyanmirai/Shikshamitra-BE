using AutoMapper;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.Settings.GeneralSetting.FeeHead;


namespace ShikshaMitra.Finance;

public class FinanceMapperProfile : Profile
{
	public FinanceMapperProfile()
	{
		#region Setting
		#region GeneralSetting
		#region FeeHead

		CreateMap<FeeHead, FeeHeadDto>();
		CreateMap<CreateUpdateFeeHeadDto, FeeHead>();
		CreateMap<ChargeCategory, ChargeCategoryResponse>();

		#endregion
		#endregion

		#region FeeCategory

		CreateMap<FeeCategory, FeeCategoryResponse>()
			.ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreationTime))
			.ForMember(d => d.UpdatedAt, o => o.MapFrom(s => s.LastModificationTime))
			.ForMember(d => d.FeeHeads, o => o.Ignore())
			.ForMember(d => d.IsUsedInDownstream, o => o.Ignore());

		CreateMap<FeeHead, FeeHeadResponse>()
			.ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreationTime))
			.ForMember(d => d.UpdatedAt, o => o.MapFrom(s => s.LastModificationTime))
			.ForMember(d => d.FeeCategoryName, o => o.Ignore())
			.ForMember(d => d.IsUsedInDownstream, o => o.Ignore());

		#endregion
		#endregion

		#region Charges
		// Note: Charge mappings are handled manually in the service layer
		// because we need to populate related entity names via joins
		#endregion

		#region ConcessionRequests
		#endregion
	}
}
