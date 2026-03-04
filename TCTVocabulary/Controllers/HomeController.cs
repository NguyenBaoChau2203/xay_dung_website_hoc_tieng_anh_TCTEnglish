using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
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
            // 1. Lấy UserId từ Claims (nơi lưu trữ sau khi SignInUserAsync chạy)
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Kiểm tra xem người dùng đã đăng nhập chưa
            if (string.IsNullOrEmpty(userIdClaim))
            {
                // Nếu chưa đăng nhập, điều hướng về trang Login
                return RedirectToAction("Login", "Account");
            }

            // 3. Chuyển đổi ID từ chuỗi sang số nguyên (int)
            int currentUserId = int.Parse(userIdClaim);

            // 4. Truy vấn dữ liệu dựa trên UserId động
            var myFolders = _context.Folders
                                    .Where(f => f.UserId == currentUserId && f.ParentFolderId == null)
                                    .ToList();

            return View(myFolders);
        }
        [HttpPost]
        public async Task<IActionResult> CreateFolder(string folderName)
        {
            // 1. Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return RedirectToAction("Folder");
            }

            // 2. Lấy UserId của người dùng hiện tại từ Claims
            // Ép kiểu sang int vì Model Folder yêu cầu UserId là kiểu int
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // 3. Khởi tạo đối tượng Folder mới
            var folder = new Folder
            {
                FolderName = folderName,
                UserId = userId, // Sử dụng ID động thay vì số 1 cố định
                ParentFolderId = null
            };

            // 4. Lưu vào Database (Sử dụng Async để tối ưu hiệu năng)
            _context.Folders.Add(folder);
            await _context.SaveChangesAsync();

            // 5. Điều hướng đến trang chi tiết của Folder vừa tạo
            return RedirectToAction("FolderDetail", "Home", new { id = folder.FolderId });
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

            // 1. Lấy UserId từ Claims của người dùng hiện tại
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var newClass = new Class
            {
                ClassName = model.ClassName,
                Description = model.Description,
                OwnerId = userId // ✅ Đã thay số 1 bằng userId động
            };

            // 2. Hash password (giữ nguyên logic của bạn - rất tốt)
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                newClass.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                newClass.HasPassword = true;          // ⭐ BẮT BUỘC
            }
            else
            {
                newClass.PasswordHash = null;
                newClass.HasPassword = false;
            }

            // 3. Xử lý Upload avatar
            if (model.Avatar != null)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/classes");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(model.Avatar.FileName);
                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Avatar.CopyToAsync(stream);
                }

                newClass.ImageUrl = "/images/classes/" + fileName;
            }

            // 4. Lưu vào Database
            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            // 5. Điều hướng sang trang chi tiết lớp học
            return RedirectToAction("ClassDetail", new { id = newClass.ClassId });
        }
        [HttpPost]
        public IActionResult EditClass(int classId, string className, string? description)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var cls = _context.Classes.FirstOrDefault(c => c.ClassId == classId);

            if (cls == null || cls.OwnerId != userId)
                return Forbid();

            cls.ClassName = className;
            cls.Description = description;

            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public IActionResult DeleteClass(int classId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var cls = _context.Classes
                .Include(c => c.ClassMembers)
                .Include(c => c.ClassMessages)
                .Include(c => c.ClassFolders)
                .FirstOrDefault(c => c.ClassId == classId);

            if (cls == null || cls.OwnerId != userId)
                return Forbid();

            _context.Classes.Remove(cls);
            _context.SaveChanges();

            return Ok();
        }
        public IActionResult ClassDetail(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var cls = _context.Classes
                .Include(c => c.Owner)
                .Include(c => c.ClassMembers)
                    .ThenInclude(cm => cm.User)
                .Include(c => c.ClassMessages)
                    .ThenInclude(m => m.User)
                .Include(c => c.ClassFolders)
                    .ThenInclude(cf => cf.Folder)
                .Include(c => c.ClassFolders)
                    .ThenInclude(cf => cf.AddedByUser)
                .FirstOrDefault(c => c.ClassId == id);

            if (cls == null)
                return NotFound();

            var messages = cls.ClassMessages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ClassMessageViewModel
                {
                    MessageId = m.MessageId,
                    UserId = m.UserId,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    FullName = m.User.FullName!,
                    IsMine = m.UserId == userId
                })
                .ToList();

            var vm = new ClassDetailViewModel
            {
                Class = cls,

                Members = cls.ClassMembers.Select(cm => cm.User).ToList(),

                Messages = messages,

                MyFolders = _context.Folders.Where(f => f.UserId == userId).ToList(),

                SavedFolders = _context.SavedFolders
                    .Where(sf => sf.UserId == userId)
                    .Select(sf => sf.Folder)
                    .ToList(),

                ClassFolders = cls.ClassFolders.ToList(),

                IsOwner = cls.OwnerId == userId,
                IsMember = cls.ClassMembers.Any(cm => cm.UserId == userId)
            };

            return View(vm);
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddFolderToClass(int classId, int folderId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var exists = await _context.ClassFolders
                .AnyAsync(cf => cf.ClassId == classId && cf.FolderId == folderId);

            if (exists)
                return BadRequest("Folder đã tồn tại trong lớp");

            _context.ClassFolders.Add(new ClassFolder
            {
                ClassId = classId,
                FolderId = folderId,
                AddedByUserId = userId
            });

            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpGet]
        public async Task<IActionResult> SearchClass(string keyword)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var classes = await _context.Classes
                .Where(c =>
                    c.ClassName.Contains(keyword) &&
                    c.OwnerId != userId &&
                    !c.ClassMembers.Any(cm => cm.UserId == userId)
                )
                .Select(c => new
                {
                    classId = c.ClassId,
                    className = c.ClassName,
                    ownerName = c.Owner.FullName,
                    hasPassword = c.HasPassword   // ⭐ BẮT BUỘC
                })
                .ToListAsync();

            return Ok(classes);
        }
        [HttpPost]
        public async Task<IActionResult> SendClassMessage(int classId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Nội dung tin nhắn không được để trống.");

            // 1. Lấy UserId từ Claims
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // 2. Khởi tạo tin nhắn với UserId động
            var message = new ClassMessage
            {
                ClassId = classId,
                UserId = userId, // ✅ Thay số 1 bằng userId
                Content = content,
                CreatedAt = DateTime.UtcNow // Khuyên dùng UtcNow để đồng bộ múi giờ
            };

            _context.ClassMessages.Add(message);
            await _context.SaveChangesAsync();

            return Ok();
        }
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> JoinClass(int classId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var exists = await _context.ClassMembers
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId);

            if (!exists)
            {
                _context.ClassMembers.Add(new ClassMember
                {
                    ClassId = classId,
                    UserId = userId
                });
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> LeaveClass(int classId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var cm = await _context.ClassMembers
                .FirstOrDefaultAsync(x => x.ClassId == classId && x.UserId == userId);

            if (cm != null)
            {
                _context.ClassMembers.Remove(cm);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
        [Authorize]

        public async Task<IActionResult> Class()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var classes = await _context.Classes
                .Include(c => c.Owner)
                .Include(c => c.ClassMembers)
                .Where(c =>
                    c.OwnerId == userId ||
                    c.ClassMembers.Any(cm => cm.UserId == userId))
                .OrderByDescending(c => c.ClassId)
                .ToListAsync();

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
