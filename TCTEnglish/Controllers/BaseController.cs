using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TCTVocabulary.Controllers
{
    public abstract class BaseController : Controller
    {
        protected int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                throw new InvalidOperationException("Authenticated user id is unavailable.");
            }

            return userId;
        }

        protected bool TryGetCurrentUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
    }
}
