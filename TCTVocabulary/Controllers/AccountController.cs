using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;

namespace TCTVocabulary.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }
    }
}