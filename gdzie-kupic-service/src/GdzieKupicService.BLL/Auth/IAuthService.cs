namespace GdzieKupicService.Core.Services;

using GdzieKupicService.API.Contracts;

public interface IAuthService
{
    Task RegisterUser(UserContracts.Register.Request request);
    Task RegisterMerchant(MerchantContracts.Register.Request request);
    Task RegisterGuest(GuestContracts.Register.Request request);
    Task LoginUser(UserContracts.Login.Request request);
    Task LoginMerchant(MerchantContracts.Login.Request request);
}