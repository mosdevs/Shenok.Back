using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Common.Interfaces
{
    public interface IUnitOfWork
    {
        Task<int> SaveChanges(CancellationToken ct = default);
    }
}
