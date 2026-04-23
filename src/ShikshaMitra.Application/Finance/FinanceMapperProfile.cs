using AutoMapper;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategory.Dtos;
using ShikshaMitra.Finance.Fees.Settings.GeneralSetting.FeeHead;
using ShikshaMitra.Finance.Setting.FeeStructures;
using ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;


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
		#endregion

		#region Charges
		// Note: Charge mappings are handled manually in the service layer
		// because we need to populate related entity names via joins
		#endregion

		#region ConcessionRequests

		CreateMap<ConcessionRequest, ConcessionRequestDto>();
		CreateMap<ConcessionFeeHeadMapping, ConcessionFeeHeadMappingDto>();

		#endregion
	}
}
