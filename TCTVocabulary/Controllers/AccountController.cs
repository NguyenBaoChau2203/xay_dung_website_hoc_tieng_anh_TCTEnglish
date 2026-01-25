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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                // TODO: Implement password reset logic here (send email)

                // For now, just return to the view with a success message
                ViewBag.Message = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";
                return View("ForgotPasswordConfirmation"); // Or return View(model) with message
            }
            return View(model);
        }
    }
}