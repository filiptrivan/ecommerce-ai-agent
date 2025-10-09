using Azure.Storage.Blobs;
using Spiderly.Security.Services;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Exceptions;
using EcommerceAIAgent.Business.Entities;
using EcommerceAIAgent.Business.DTO;
using EcommerceAIAgent.Business.Enums;

namespace EcommerceAIAgent.Business.Services
{
    public class AuthorizationBusinessService : AuthorizationBusinessServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;

        public AuthorizationBusinessService(
            IApplicationDbContext context, 
            AuthenticationService authenticationService
        )
            : base(context, authenticationService)
        {
            _context = context;
            _authenticationService = authenticationService;
        }

        #region User

        public override async Task AuthorizeUserReadAndThrow(long? userId)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminReadPermission = await IsAuthorizedAsync<User>(BusinessPermissionCodes.ReadUser);
                bool isCurrentUser = _authenticationService.GetCurrentUserId() == userId;

                if (isCurrentUser == false && hasAdminReadPermission == false)
                    throw new UnauthorizedException();
            });
        }

        public override async Task AuthorizeUserUpdateAndThrow(UserDTO userDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                bool hasAdminUpdatePermission = await IsAuthorizedAsync<User>(BusinessPermissionCodes.UpdateUser);
                if (hasAdminUpdatePermission)
                    return;

                long currentUserId = _authenticationService.GetCurrentUserId();
                if (currentUserId != userDTO.Id)
                    throw new UnauthorizedException();

                User user = await GetInstanceAsync<User, long>(userDTO.Id, null);

                if (
                    userDTO.IsDisabled != user.IsDisabled ||
                    userDTO.HasLoggedInWithExternalProvider != user.HasLoggedInWithExternalProvider
                )
                {
                    throw new UnauthorizedException();
                }
            });
        }

        #endregion

    }
}
