using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TCTVocabulary.Models;
using TCTVocabulary.Models.ViewModels;
using TCTVocabulary.ViewModel;

namespace TCTVocabulary.Controllers
{
    public class HomeController : Controller
    {
        private readonly DbflashcardContext _context;

        public HomeController(DbflashcardContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Folder()
        {
            var myFolders = _context.Folders
                                    .Where(f => f.UserId == 1 && f.ParentFolderId == null)
                                    .ToList();

            return View(myFolders);
        }
        [HttpPost]
        public IActionResult CreateFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return RedirectToAction("Index");
            }

            var folder = new Folder
            {
                FolderName = folderName,
                UserId = 1,
                ParentFolderId = null
            };

            _context.Folders.Add(folder);
            _context.SaveChanges();

            return RedirectToAction(
     "FolderDetail",
     "Home",
     new { id = folder.FolderId });
        }
        public IActionResult FolderDetail(int id)
        {
            var folder = _context.Folders
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
                .FirstOrDefault(f => f.FolderId == id);

            if (folder == null)
                return NotFound();

            var vm = new FolderDetailViewModel
            {
                Folder = folder,
                Sets = folder.Sets.ToList()
            };

            return View(vm);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }
        public IActionResult CreateClass()
        {
            return View();
        }
        public IActionResult Register()
        {
            return View();
        }
        public IActionResult Landing() 
        {
            return View();
        }
        // Sửa int thành int? để folderId có thể mang giá trị null
        public IActionResult CreateSet(int? folderId)
        {
            ViewBag.FolderId = folderId;
            return View();
        }
        [HttpPost]
        public IActionResult CreateSet(int? folderId, string SetName, string Description, string[] Terms, string[] Definitions)
        {
            // 1. Khởi tạo đối tượng Set mới
            var newSet = new Set
            {
                SetName = SetName,
                FolderId = folderId, // Gán vào thư mục cha
                OwnerId = 1, // Tạm thời gán ID người dùng cố định (cần sửa theo Login thực tế)
                CreatedAt = DateTime.Now
            };

            // 2. Duyệt qua mảng Terms và Definitions để tạo danh sách Card
            if (Terms != null && Definitions != null)
            {
                for (int i = 0; i < Terms.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(Terms[i]))
                    {
                        newSet.Cards.Add(new Card
                        {
                            Term = Terms[i],
                            Definition = Definitions[i]
                        });
                    }
                }
            }

            // 3. Lưu dữ liệu vào DbContext
            _context.Sets.Add(newSet);
            _context.SaveChanges();

            // 4. Điều hướng về trang FolderDetail của thư mục cha nếu có
            if (folderId.HasValue)
            {
                return RedirectToAction("FolderDetail", new { id = folderId.Value });
            }
            return RedirectToAction("Folder");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [HttpPost]
        public IActionResult DeleteFolder(int id)
        {
            var folder = _context.Folders
                .Include(f => f.Sets)
                .FirstOrDefault(f => f.FolderId == id);

            if (folder != null)
            {
                // Lưu ý: Tùy vào thiết lập DB, bạn có thể cần xóa các Sets liên quan trước
                // hoặc để DB tự động xóa (Cascade Delete).
                _context.Folders.Remove(folder);
                _context.SaveChanges();
            }

            // Sau khi xóa xong, quay về trang danh sách thư mục chính
            return RedirectToAction("Folder");
        }
        [HttpPost]
        public IActionResult UpdateFolderName(int folderId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return RedirectToAction("FolderDetail", new { id = folderId });
            }

            var folder = _context.Folders.FirstOrDefault(f => f.FolderId == folderId);
            if (folder != null)
            {
                folder.FolderName = newName;
                _context.SaveChanges();
            }

            return RedirectToAction("FolderDetail", new { id = folderId });
        }
        [HttpPost]
        public IActionResult DeleteSet(int setId, int folderId)
        {
            // Tìm Set kèm theo các Cards của nó
            var set = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == setId);

            if (set != null)
            {
                // Xóa Cards trước
                if (set.Cards != null && set.Cards.Any())
                {
                    _context.Cards.RemoveRange(set.Cards);
                }

                // Xóa Set
                _context.Sets.Remove(set);
                _context.SaveChanges();
            }

            // Quay lại trang chi tiết thư mục
            return RedirectToAction("FolderDetail", new { id = folderId });
        }
        [HttpPost]
        public IActionResult RemoveSetFromFolder(int setId, int folderId)
        {
            // Tìm học phần cần xóa
            var set = _context.Sets
                .Include(s => s.Cards) // Bao gồm cả các thẻ con để xóa sạch dữ liệu liên quan
                .FirstOrDefault(s => s.SetId == setId && s.FolderId == folderId);

            if (set != null)
            {
                // 1. Xóa tất cả các thẻ (Cards) thuộc học phần này trước để tránh lỗi ràng buộc
                if (set.Cards != null && set.Cards.Any())
                {
                    _context.Cards.RemoveRange(set.Cards);
                }

                // 2. Xóa chính học phần (Set)
                _context.Sets.Remove(set);

                // 3. Lưu thay đổi vào DB
                _context.SaveChanges();
            }

            // Quay lại trang chi tiết thư mục hiện tại
            return RedirectToAction("FolderDetail", new { id = folderId });
        }

        public IActionResult Study(int id)
        {
            // Phải có .Include(s => s.Cards) để lấy danh sách từ vựng
            var set = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == id);

            if (set == null)
            {
                return NotFound(); // Trả về 404 nếu không tìm thấy ID
            }

            if (!set.Cards.Any())
            {
                // Nếu học phần không có thẻ nào, báo lỗi hoặc chuyển hướng
                return RedirectToAction("FolderDetail", new { id = set.FolderId });
            }

            var vm = new StudyViewModel
            {
                Set = set,
                Cards = set.Cards.ToList()
            };

            return View(vm);
        }

        public IActionResult Speaking(int? id)
        {
            if (id == null || id == 0)
            {
                // Nếu click từ Sidebar thì chưa có id cụ thể của Học phần (Set).
                // Chuyển hướng người dùng về trang Thư mục để họ chọn một Học phần.
                return RedirectToAction("Folder");
            }

            var set = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == id);

            if (set == null)
            {
                return NotFound();
            }

            if (!set.Cards.Any())
            {
                return RedirectToAction("FolderDetail", new { id = set.FolderId });
            }

            var vm = new StudyViewModel
            {
                Set = set,
                Cards = set.Cards.ToList()
            };

            return View(vm);
        }

        public IActionResult Listening()
        {
            return View();
        }

        public IActionResult Grammar()
        {
            return View();
        }

        public IActionResult Reading()
        {
            return View();
        }

        public IActionResult Writing()
        {
            return View();
        }

        // GET: Home/EditSet/5
public IActionResult EditSet(int id)
{
    // Lấy thông tin Set cùng với danh sách Cards của nó
    var set = _context.Sets
        .Include(s => s.Cards)
        .FirstOrDefault(s => s.SetId == id);

    if (set == null) return NotFound();

    return View(set); // Trả về View EditSet.cshtml
}

[HttpPost]
public IActionResult EditSet(int SetId, string SetName, string Description, string[] Terms, string[] Definitions)
{
    var existingSet = _context.Sets
        .Include(s => s.Cards)
        .FirstOrDefault(s => s.SetId == SetId);

    if (existingSet == null) return NotFound();

    // Cập nhật thông tin cơ bản
    existingSet.SetName = SetName;
    existingSet.Description = Description;

    // Xóa toàn bộ Cards cũ và thêm lại Cards mới (cách đơn giản nhất)
    _context.Cards.RemoveRange(existingSet.Cards);
    
    if (Terms != null)
    {
        for (int i = 0; i < Terms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(Terms[i]))
            {
                existingSet.Cards.Add(new Card
                {
                    Term = Terms[i],
                    Definition = Definitions[i]
                });
            }
        }
    }

    _context.SaveChanges();

    return RedirectToAction("FolderDetail", new { id = existingSet.FolderId });
}
        [HttpPost]
        public async Task<IActionResult> CreateClass(CreateClassViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var newClass = new Class
            {
                ClassName = model.ClassName,
                Description = model.Description,
                OwnerId = 1 // ⚠️ tạm thời, sau này lấy từ User.Identity
            };

            // Hash password (nếu có)
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                newClass.PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(model.Password);
            }

            // Upload avatar (nếu có)
            if (model.Avatar != null)
            {
                var folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/images/classes"
                );

                // 👉 DÒNG QUAN TRỌNG NHẤT (SỬA LỖI)
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(model.Avatar.FileName);
                var filePath = Path.Combine(folderPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await model.Avatar.CopyToAsync(stream);

                newClass.ImageUrl = "/images/classes/" + fileName;
            }

            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            // 👉 Chuyển sang ClassDetail
            return RedirectToAction("ClassDetail", new { id = newClass.ClassId });
        }
        public IActionResult ClassDetail(int id)
        {
            var classEntity = _context.Classes
                .Include(c => c.Users)
                .Include(c => c.ClassMessages)
                    .ThenInclude(m => m.User)
                .FirstOrDefault(c => c.ClassId == id);

            if (classEntity == null)
                return NotFound();

            var viewModel = new ClassDetailViewModel
            {
                Class = classEntity,
                Members = classEntity.Users.ToList(),
                Messages = classEntity.ClassMessages
                    .OrderBy(m => m.CreatedAt)
                    .ToList()
            };

            return View(viewModel); // ✅ ĐÚNG KIỂU
        }
        [HttpPost]
        public IActionResult SendClassMessage(int classId, string content)
        {
            var message = new ClassMessage
            {
                ClassId = classId,
                UserId = 1, // tạm thời
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.ClassMessages.Add(message);
            _context.SaveChanges();

            return Ok();
        }
        public IActionResult Class()
        {
            int userId = 1; // tạm thời hardcode

            var classes = _context.Classes
                .Where(c =>
                    c.OwnerId == userId ||
                    c.Users.Any(u => u.UserId == userId))
                .Include(c => c.Owner)
                .ToList();

            return View(classes);
        }

        // Action cho Write Mode
        public IActionResult WriteMode(int id)
        {
            var set = _context.Sets.Include(s => s.Cards).FirstOrDefault(s => s.SetId == id);
            if (set == null) return NotFound();
            var vm = new StudyViewModel { Set = set, Cards = set.Cards.ToList() };
            return View(vm);
        }

        // Action cho Quiz Mode
        public IActionResult QuizMode(int id)
        {
            var set = _context.Sets.Include(s => s.Cards).FirstOrDefault(s => s.SetId == id);
            if (set == null) return NotFound();
            var vm = new StudyViewModel { Set = set, Cards = set.Cards.ToList() };
            return View(vm);
        }
        public IActionResult MatchingMode(int id)
        {
            var set = _context.Sets.Include(s => s.Cards).FirstOrDefault(s => s.SetId == id);
            if (set == null) return NotFound();
            var vm = new StudyViewModel { Set = set, Cards = set.Cards.ToList() };
            return View(vm);
        }

      
    }
}
