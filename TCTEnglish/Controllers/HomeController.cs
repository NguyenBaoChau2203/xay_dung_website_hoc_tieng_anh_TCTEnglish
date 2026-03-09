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

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;

            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idClaim, out userId))
            {
                return true;
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var dbUserId = _context.Users
                .Where(u => u.Email == email)
                .Select(u => (int?)u.UserId)
                .FirstOrDefault();

            if (!dbUserId.HasValue)
            {
                return false;
            }

            userId = dbUserId.Value;
            return true;
        }

        private bool IsAdminUser()
        {
            return User.IsInRole(Roles.Admin);
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Landing");
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .AsNoTracking()
                .AsSplitQuery()
                .Include(u => u.Folders)
                    .ThenInclude(f => f.Sets)
                .Include(u => u.Sets)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var setIds = user.Sets.Select(s => s.SetId).ToList();

            var cardCount = await _context.Cards
                .AsNoTracking()
                .CountAsync(c => setIds.Contains(c.SetId));

            // 🔥 3 folder ngẫu nhiên
            var todayFolders = await _context.Folders
                .AsNoTracking()
                .Include(f => f.Sets)
                .Include(f => f.User)
                .Where(f => f.UserId != userId)
                .OrderBy(f => Guid.NewGuid())
                .Take(3)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                FullName = user.FullName,
                Streak = user.Streak ?? 0,
                Goal = user.Goal ?? 0,
                FolderCount = user.Folders.Count,
                SetCount = user.Sets.Count,
                CardCount = cardCount,
                DailyChallenge = await GetRandomChallengeAsync(),
                TodayFolders = todayFolders
            };

            return View(model);
        }

        // =========================
        // DAILY CHALLENGE (AJAX)
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetDailyChallenge()
        {
            var challenge = await GetRandomChallengeAsync();
            return PartialView("_DailyChallenge", challenge);
        }

        // =========================
        // CHECK ANSWER
        // =========================
        [HttpPost]
        [Authorize]
        public IActionResult CheckAnswer(int selectedCardId, int correctCardId)
        {
            bool isCorrect = selectedCardId == correctCardId;

            if (isCorrect)
            {
                UpdateStreak();
            }

            return Json(new
            {
                correct = isCorrect
            });
        }

        // =========================
        // RANDOM CHALLENGE LOGIC
        // =========================
        private async Task<DailyChallengeViewModel> GetRandomChallengeAsync()
        {
            var totalCards = await _context.Cards.CountAsync();
            if (totalCards == 0)
                return new DailyChallengeViewModel();

            var randomIndex = Random.Shared.Next(totalCards);
            var randomCard = await _context.Cards
                .AsNoTracking()
                .Skip(randomIndex)
                .FirstAsync();

            var wrongAnswers = await _context.Cards
                .AsNoTracking()
                .Where(c => c.CardId != randomCard.CardId)
                .OrderBy(c => Guid.NewGuid())
                .Take(3)
                .Select(c => new AnswerOption
                {
                    CardId = c.CardId,
                    Definition = c.Definition
                })
                .ToListAsync();

            wrongAnswers.Add(new AnswerOption
            {
                CardId = randomCard.CardId,
                Definition = randomCard.Definition
            });

            return new DailyChallengeViewModel
            {
                CardId = randomCard.CardId,
                Term = randomCard.Term,
                Options = wrongAnswers.OrderBy(_ => Random.Shared.Next()).ToList()
            };
        }

        // =========================
        // STREAK UPDATE
        // =========================
        private void UpdateStreak()
        {
            if (!TryGetCurrentUserId(out var userId)) return;

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return;

            var today = DateTime.UtcNow.Date;

            if (user.LastStudyDate == null)
            {
                user.Streak = 1;
            }
            else
            {
                var lastStudy = user.LastStudyDate.Value.Date;

                if (lastStudy == today)
                {
                    return; // đã học hôm nay rồi
                }

                if (lastStudy == today.AddDays(-1))
                {
                    user.Streak = (user.Streak ?? 0) + 1;
                }
                else
                {
                    user.Streak = 1; // reset streak
                }
            }

            user.LastStudyDate = today;

            _context.SaveChanges();
        }

        // =========================
        // LANDING (NO LOGIN)
        // =========================
        [AllowAnonymous]
        public IActionResult Landing()
        {
            return View();
        }

        // =========================
        // PRIVACY
        // =========================
        public IActionResult Privacy()
        {
            return View();
        }
        [HttpGet]
      
        public IActionResult Search(string q)
        {
            var vm = new SearchViewModel
            {
                Query = q ?? ""
            };

            if (!string.IsNullOrWhiteSpace(q))
            {
                vm.Folders = _context.Folders
                    .Include(f => f.User)
                    .Where(f => f.FolderName.Contains(q))
                    .ToList();

                vm.Classes = _context.Classes
                    .Include(c => c.Owner)
                    .Where(c => c.ClassName.Contains(q))
                    .ToList();

                vm.Users = _context.Users
                    .Where(u => u.FullName.Contains(q) || u.Email.Contains(q))
                    .ToList();
            }

            return View(vm); // ✅ VIEW, KHÔNG partial
        }
        public IActionResult Folder()
        {
            if (!TryGetCurrentUserId(out var currentUserId))
                return RedirectToAction("Login", "Account");

            // 🔹 Folder của chính mình
            var myFolders = _context.Folders
                .Where(f => f.UserId == currentUserId && f.ParentFolderId == null)
                .ToList();

            // 🔹 Folder đã lưu (SavedFolder)
            var savedFolders = _context.SavedFolders
                .Where(sf => sf.UserId == currentUserId)
                .Include(sf => sf.Folder)
                .Select(sf => sf.Folder)
                .ToList();

            var vm = new FolderPageViewModel
            {
                MyFolders = myFolders,
                SavedFolders = savedFolders
            };

            return View(vm);
        }
        [HttpPost]
        [Authorize]
        public IActionResult UnsaveFolder(int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
                return RedirectToAction("Login", "Account");

            var saved = _context.SavedFolders
                .FirstOrDefault(sf => sf.UserId == userId && sf.FolderId == folderId);

            if (saved != null)
            {
                _context.SavedFolders.Remove(saved);
                _context.SaveChanges();
            }

            return RedirectToAction("FolderDetail", new { id = folderId });
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateFolder(string folderName)
        {
            // 1. Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return RedirectToAction("Folder");
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

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
        [Authorize]
        public IActionResult FolderDetail(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var folder = _context.Folders
                .Include(f => f.User)
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
                .FirstOrDefault(f => f.FolderId == id);

            if (folder == null)
                return NotFound();

            // ✅ KIỂM TRA ĐÃ LƯU CHƯA
            var isSaved = _context.SavedFolders
                .Any(sf => sf.UserId == userId && sf.FolderId == id);

            var vm = new FolderDetailViewModel
            {
                Folder = folder,
                Sets = folder.Sets.ToList(),
                IsSaved = isSaved   // ✅ thêm đúng 1 dòng
            };

            return View(vm);
        }

        public IActionResult Login()
        {
            return View();
        }
        [Authorize]
        public IActionResult CreateClass()
        {
            return View();
        }
        public IActionResult Register()
        {
            return View();
        }
      
        // Sửa int thành int? để folderId có thể mang giá trị null
        [Authorize]
        public IActionResult CreateSet(int? folderId)
        {
            ViewBag.FolderId = folderId;
            return View();
        }
        [HttpPost]
        [Authorize] // [FIX-AI-AUTH] Bắt buộc đăng nhập
        public async Task<IActionResult> CreateSet(int? folderId, string SetName, string Description, string[] Terms, string[] Definitions)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (folderId.HasValue)
            {
                var folder = await _context.Folders.FirstOrDefaultAsync(f => f.FolderId == folderId.Value);
                if (folder == null)
                {
                    return NotFound();
                }

                if (!IsAdminUser() && folder.UserId != userId)
                {
                    return Forbid();
                }
            }

            // 1. Khởi tạo đối tượng Set mới
            var newSet = new Set
            {
                SetName = SetName,
                FolderId = folderId, // Gán vào thư mục cha
                OwnerId = userId, // [FIX-AI-AUTH] Sử dụng ID người dùng thực tế
                CreatedAt = DateTime.UtcNow
            };

            // 2. Duyệt qua mảng Terms và Definitions để tạo danh sách Card
            if (Terms != null && Definitions != null)
            {
                for (int i = 0; i < Terms.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(Terms[i]))
                    {
                        string? imageUrl = null;

                        // Xử lý upload ảnh cho card này (dùng tên indexed để tránh lệch vị trí)
                        var imageFile = Request.Form.Files[$"ImageFile_{i}"];
                        if (imageFile != null && imageFile.Length > 0)
                        {
                            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/cards");
                            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                            var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                            var filePath = Path.Combine(folderPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await imageFile.CopyToAsync(stream);
                            }
                            imageUrl = "/images/cards/" + fileName;
                        }

                        newSet.Cards.Add(new Card
                        {
                            Term = Terms[i],
                            Definition = Definitions[i],
                            ImageUrl = imageUrl
                        });
                    }
                }
            }

            // 3. Lưu dữ liệu vào DbContext
            _context.Sets.Add(newSet);
            await _context.SaveChangesAsync();

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
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteFolder(int id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var folder = _context.Folders
                .Include(f => f.Sets)
                .FirstOrDefault(f => f.FolderId == id);

            if (folder == null)
            {
                return NotFound();
            }

            if (!IsAdminUser() && folder.UserId != currentUserId)
            {
                return Forbid();
            }

            _context.Folders.Remove(folder);
            _context.SaveChanges();

            return RedirectToAction("Folder");
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateFolderName(int folderId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return RedirectToAction("FolderDetail", new { id = folderId });
            }

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var folder = _context.Folders
                .FirstOrDefault(f => f.FolderId == folderId);

            if (folder == null)
            {
                return NotFound();
            }

            if (!IsAdminUser() && folder.UserId != currentUserId)
            {
                return Forbid();
            }

            folder.FolderName = newName;
            _context.SaveChanges();

            return RedirectToAction("FolderDetail", new { id = folderId });
        }
        [HttpPost]
        [Authorize]
        public IActionResult DeleteSet(int setId, int folderId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var set = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == setId);

            if (set == null)
            {
                return NotFound();
            }

            if (!IsAdminUser() && set.OwnerId != currentUserId)
            {
                return Forbid();
            }

            if (set.Cards != null && set.Cards.Any())
            {
                _context.Cards.RemoveRange(set.Cards);
            }

            _context.Sets.Remove(set);
            _context.SaveChanges();

            // Quay lại trang chi tiết thư mục
            return RedirectToAction("FolderDetail", new { id = folderId });
        }
        [HttpPost]
        [Authorize]
        public IActionResult RemoveSetFromFolder(int setId, int folderId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var set = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == setId && s.FolderId == folderId);

            if (set == null)
            {
                return NotFound();
            }

            if (!IsAdminUser() && set.OwnerId != currentUserId)
            {
                return Forbid();
            }

            if (set.Cards != null && set.Cards.Any())
            {
                _context.Cards.RemoveRange(set.Cards);
            }

            _context.Sets.Remove(set);
            _context.SaveChanges();

            // Quay lại trang chi tiết thư mục hiện tại
            return RedirectToAction("FolderDetail", new { id = folderId });
        }

        [Authorize]
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
        [Authorize]
        public IActionResult EditSet(int id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            // Lấy thông tin Set cùng với danh sách Cards của nó
            var set = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == id);

            if (set == null) return NotFound();

            if (!IsAdminUser() && set.OwnerId != currentUserId)
            {
                return Forbid();
            }

            return View(set); // Trả về View EditSet.cshtml
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditSet(int SetId, string SetName, string Description, string[] Terms, string[] Definitions, string[] ExistingImageUrls)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var existingSet = _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefault(s => s.SetId == SetId);

            if (existingSet == null) return NotFound();

            if (!IsAdminUser() && existingSet.OwnerId != currentUserId)
            {
                return Forbid();
            }

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
                        string? imageUrl = null;

                        // Nếu có ảnh mới upload → lưu file mới (dùng tên indexed để tránh lệch vị trí)
                        var imageFile = Request.Form.Files[$"ImageFile_{i}"];
                        if (imageFile != null && imageFile.Length > 0)
                        {
                            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/cards");
                            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                            var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                            var filePath = Path.Combine(folderPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await imageFile.CopyToAsync(stream);
                            }
                            imageUrl = "/images/cards/" + fileName;
                        }
                        // Nếu không upload mới → giữ ảnh cũ
                        else if (ExistingImageUrls != null && i < ExistingImageUrls.Length && !string.IsNullOrEmpty(ExistingImageUrls[i]))
                        {
                            imageUrl = ExistingImageUrls[i];
                        }

                        existingSet.Cards.Add(new Card
                        {
                            Term = Terms[i],
                            Definition = Definitions[i],
                            ImageUrl = imageUrl
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("FolderDetail", new { id = existingSet.FolderId });
        }
        [HttpPost]
        [Authorize]

        public async Task<IActionResult> CreateClass(CreateClassViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

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
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult EditClass(int classId, string className, string? description)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var cls = _context.Classes.FirstOrDefault(c => c.ClassId == classId);

            if (cls == null)
                return NotFound();

            if (!IsAdminUser() && cls.OwnerId != userId)
                return Forbid();

            cls.ClassName = className;
            cls.Description = description;

            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteClass(int classId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var cls = _context.Classes
                .Include(c => c.ClassMembers)
                .Include(c => c.ClassMessages)
                .Include(c => c.ClassFolders)
                .FirstOrDefault(c => c.ClassId == classId);

            if (cls == null)
                return NotFound();

            if (!IsAdminUser() && cls.OwnerId != userId)
                return Forbid();

            _context.Classes.Remove(cls);
            _context.SaveChanges();

            return Ok();
        }
        [Authorize]
        public IActionResult ClassDetail(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

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
        [Authorize]
        public async Task<IActionResult> AddFolderToClass(int classId, int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var cls = await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);
            if (cls == null)
            {
                return NotFound();
            }

            if (!IsAdminUser() && cls.OwnerId != userId)
            {
                return Forbid();
            }

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
        [HttpPost]
        [Authorize]
        public IActionResult SaveFolder(int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // 1. Kiểm tra đã lưu chưa
            var existed = _context.SavedFolders
                .Any(sf => sf.UserId == userId && sf.FolderId == folderId);

            if (existed)
                return RedirectToAction("FolderDetail", new { id = folderId });

            // 2. Lưu mới
            var saved = new SavedFolder
            {
                UserId = userId,
                FolderId = folderId
            };

            _context.SavedFolders.Add(saved);
            _context.SaveChanges();

            return RedirectToAction("FolderDetail", new { id = folderId });
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SearchClass(string keyword)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinClass(int classId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var classExists = await _context.Classes
                .AnyAsync(c => c.ClassId == classId);

            if (!classExists)
                return NotFound();

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveClass(int classId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

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
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

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
        [Authorize]
        public IActionResult WriteMode(int id)
        {
            var set = _context.Sets.Include(s => s.Cards).FirstOrDefault(s => s.SetId == id);
            if (set == null) return NotFound();
            var vm = new StudyViewModel { Set = set, Cards = set.Cards.ToList() };
            return View(vm);
        }

        // Action cho Quiz Mode
        [Authorize]
        public IActionResult QuizMode(int id)
        {
            var set = _context.Sets.Include(s => s.Cards).FirstOrDefault(s => s.SetId == id);
            if (set == null) return NotFound();
            var vm = new StudyViewModel { Set = set, Cards = set.Cards.ToList() };
            return View(vm);
        }
        [Authorize]
        public IActionResult MatchingMode(int id)
        {
            var set = _context.Sets.Include(s => s.Cards).FirstOrDefault(s => s.SetId == id);
            if (set == null) return NotFound();
            var vm = new StudyViewModel { Set = set, Cards = set.Cards.ToList() };
            return View(vm);
        }


    }
    public class ChatController : Controller
    {
        private readonly IWebHostEnvironment _env;
        public ChatController(IWebHostEnvironment env) => _env = env;

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UploadImage(IFormFile image, int classId)
        {
            if (image == null || image.Length == 0) return BadRequest();

            // Tạo thư mục lưu trữ nếu chưa có
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads/chat");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Tạo tên file duy nhất
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            string filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Trả về đường dẫn để JS gọi Hub
            return Json(new { imageUrl = "/uploads/chat/" + fileName });
        }
    }
}
