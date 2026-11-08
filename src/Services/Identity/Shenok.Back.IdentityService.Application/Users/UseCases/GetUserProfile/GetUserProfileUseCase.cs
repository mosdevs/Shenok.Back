using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.GetUserProfile
{
    public sealed class GetUserProfileUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        public GetUserProfileUseCase(IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<GetUserProfileResponse> ExecuteAsync(
            GetUserProfileRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
            if (user is null)
                throw new ArgumentNullException("User not found!", nameof(user));

            return new GetUserProfileResponse(
                user.Id.Value,
                user.Profile.DisplayName,
                user.Profile.AvatarUrl,
                user.Profile.Bio);
        }
    }
}
