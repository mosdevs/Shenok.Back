using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.UpdateUser
{
    public sealed class UpdateUserUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateUserUseCase
            (IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

    }
}
