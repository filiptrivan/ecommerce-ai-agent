using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.DTO;
using EcommerceAIAgent.Business.DTO;
using EcommerceAIAgent.Business.Services;

namespace EcommerceAIAgent.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class NotificationController : NotificationBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly EcommerceAIAgentBusinessService _ecommerceAIAgentBusinessService;

        public NotificationController(
            IApplicationDbContext context, 
            EcommerceAIAgentBusinessService ecommerceAIAgentBusinessService
        )
            : base(context, ecommerceAIAgentBusinessService)
        {
            _context = context;
            _ecommerceAIAgentBusinessService = ecommerceAIAgentBusinessService;
        }

        [HttpGet]
        [AuthGuard]
        public async Task SendNotificationEmail(long notificationId, int notificationVersion)
        {
            await _ecommerceAIAgentBusinessService.SendNotificationEmail(notificationId, notificationVersion);
        }

        [HttpDelete]
        [AuthGuard]
        public async Task DeleteNotificationForCurrentUser(long notificationId, int notificationVersion)
        {
            await _ecommerceAIAgentBusinessService.DeleteNotificationForCurrentUser(notificationId, notificationVersion);
        }

        [HttpGet]
        [AuthGuard]
        public async Task MarkNotificationAsReadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _ecommerceAIAgentBusinessService.MarkNotificationAsReadForCurrentUser(notificationId, notificationVersion);
        }

        [HttpGet]
        [AuthGuard]
        public async Task MarkNotificationAsUnreadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _ecommerceAIAgentBusinessService.MarkNotificationAsUnreadForCurrentUser(notificationId, notificationVersion);
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        [UIDoNotGenerate]
        public async Task<int> GetUnreadNotificationsCountForCurrentUser()
        {
            return await _ecommerceAIAgentBusinessService.GetUnreadNotificationsCountForCurrentUser();
        }

        [HttpPost]
        [AuthGuard]
        public async Task<PaginatedResultDTO<NotificationDTO>> GetNotificationsForCurrentUser(FilterDTO filterDTO)
        {
            return await _ecommerceAIAgentBusinessService.GetNotificationsForCurrentUser(filterDTO);
        }

    }
}

