namespace GdzieKupicService.Core.Services;

using GdzieKupicService.API.Contracts;

public class AuthService : IAuthService
{
    public async Task RegisterUser(UserContracts.Register.Request request)
    {
    }

    public async Task RegisterMerchant(MerchantContracts.Register.Request request)
    {
    }

    public async Task RegisterGuest(GuestContracts.Register.Request request)
    {
    }

    public async Task LoginUser(UserContracts.Login.Request request)
    {
    }

    public async Task LoginMerchant(MerchantContracts.Login.Request request)
    {
    }
}