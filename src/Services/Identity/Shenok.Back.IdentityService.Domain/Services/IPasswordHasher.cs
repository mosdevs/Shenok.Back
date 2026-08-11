using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Domain.Services
{
    public interface IPasswordHasher
    {
        string Hash(string plainPassword);
        bool Verify(string plainPassword, string hashedPassword);
    }
}
