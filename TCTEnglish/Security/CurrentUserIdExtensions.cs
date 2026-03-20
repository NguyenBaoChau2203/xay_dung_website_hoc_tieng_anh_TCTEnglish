using System.Security.Claims;

namespace TCTVocabulary.Security
{
    public static class CurrentUserIdExtensions
    {
        public static bool TryGetUserId(this ClaimsPrincipal? user, out int userId)
        {
            userId = 0;
            return int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
    }
}
