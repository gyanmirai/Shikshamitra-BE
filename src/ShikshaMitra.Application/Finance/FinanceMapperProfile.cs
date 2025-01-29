using AutoMapper;
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



		#endregion


		#region
		CreateMap<PaymentStatusOption, PaymentStatusOptionDto>();

		CreateMap<CreateUpdatePaymentStatusOptionDto, PaymentStatusOption>();
		#endregion
		#endregion
		#endregion
	}

}
