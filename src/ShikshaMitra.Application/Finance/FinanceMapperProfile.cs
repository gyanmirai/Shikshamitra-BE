using AutoMapper;
using ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;
using ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;


namespace ShikshaMitra.Finance;

    public class FinanceMapperProfile :Profile 
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


	}

}
