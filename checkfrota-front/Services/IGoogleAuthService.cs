using System;
using System.Collections.Generic;
using System.Text;

namespace checkfrota_front.Services
{
    public interface IGoogleAuthService
    {
        Task<GoogleSignInResult?> SignInAsync();
    }
}
