using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;
using TCTVocabulary.Security;

namespace TCTVocabulary.Controllers
{
    public abstract class BaseController : Controller
    {
        protected int GetCurrentUserId()
        {
            if (!User.TryGetUserId(out var userId))
            {
                throw new InvalidOperationException("Authenticated user id is unavailable.");
            }

            return userId;
        }

        protected bool TryGetCurrentUserId(out int userId)
        {
            return User.TryGetUserId(out userId);
        }

        protected bool IsAdminUser()
        {
            return User.IsInRole(Roles.Admin);
        }
    }
}
