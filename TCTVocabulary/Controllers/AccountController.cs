using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;

namespace TCTVocabulary.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Login
        public IActionResult Login()
        {
            ViewData["ActiveTab"] = "login";
            return View("Auth");
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            ViewData["ActiveTab"] = "register";
            return View("Auth");
        }

        // GET: /Account/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }
    }
}