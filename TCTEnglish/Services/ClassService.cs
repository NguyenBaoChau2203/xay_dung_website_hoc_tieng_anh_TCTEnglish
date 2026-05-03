using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public class ClassService : IClassService
    {
        private readonly DbflashcardContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<ClassService> _logger;

        public ClassService(
            DbflashcardContext context,
            IFileStorageService fileStorageService,
            ILogger<ClassService> logger)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }
        
        public async Task<ClassPageViewModel> GetClassPageAsync(int userId)
        {
            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.OwnerId == userId || c.ClassMembers.Any(cm => cm.UserId == userId))
                .OrderByDescending(c => c.ClassId)
                .Select(c => new ClassCardViewModel
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    ImageUrl = c.ImageUrl,
                    OwnerName = c.Owner.FullName ?? "Không xác định",
                    IsOwner = c.OwnerId == userId
                })
                .ToListAsync();

            return new ClassPageViewModel
            {
                Classes = classes
            };
        }

        public async Task<int> CreateClassAsync(CreateClassViewModel model, int userId)
        {
            _logger.LogInformation("Creating class for user {userId}", userId);

            // 1. Ánh xạ các thông tin cơ bản và các Flag mới vào Model
            var newClass = new Class
            {
                ClassName = model.ClassName,
                Description = model.Description,
                OwnerId = userId,

                // Gán các thuộc tính mới[cite: 1]
                RequiresApproval = model.RequiresApproval,
                IsChatLocked = model.IsChatLocked,
                AllowMemberToPost = model.AllowMemberToPost,

                CreatedAt = DateTime.UtcNow
            };

            // 2. Xử lý mật khẩu và ảnh[cite: 10]
            ApplyClassPassword(newClass, model.Password);
            newClass.ImageUrl = await SaveClassAvatarAsync(model.Avatar);

            _context.Classes.Add(newClass);

            // Lưu lần 1 để lấy ClassId
            await _context.SaveChangesAsync();

            // 3. Tự động thêm người tạo vào danh sách thành viên với quyền Owner[cite: 3]
            var ownerMember = new ClassMember
            {
                ClassId = newClass.ClassId,
                UserId = userId,
                Role = ClassRole.Owner, // Đảm bảo Role là Owner[cite: 3]
                JoinedAt = DateTime.UtcNow
            };

            _context.ClassMembers.Add(ownerMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Class created with classId {classId} and owner member added by user {userId}", newClass.ClassId, userId);

            return newClass.ClassId;
        }

        public async Task<OperationResult> UpdateClassAsync(
    int classId,
    string className,
    string? description,
    bool requiresApproval, // Thêm mới
    bool isChatLocked,     // Thêm mới
    bool allowMemberToPost, // Thêm mới
    int userId,
    bool isAdmin)
        {
            var cls = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (cls == null) return OperationResult.NotFound();

            // Kiểm tra quyền: Chỉ chủ sở hữu hoặc Admin mới được sửa[cite: 11, 13]
            if (!isAdmin && cls.OwnerId != userId) return OperationResult.NotFound();

            // Cập nhật các thông tin cơ bản
            cls.ClassName = className;
            cls.Description = description;

            // Cập nhật 3 tính năng mới[cite: 1]
            cls.RequiresApproval = requiresApproval;
            cls.IsChatLocked = isChatLocked;
            cls.AllowMemberToPost = allowMemberToPost;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Class {classId} updated by user {userId}", classId, userId);

            return OperationResult.Success();
        }

        public async Task<OperationResult> DeleteClassAsync(int classId, int userId, bool isAdmin)
        {
            var cls = await _context.Classes
                .Include(c => c.ClassMembers)
                .Include(c => c.ClassMessages)
                .Include(c => c.ClassFolders)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (cls == null)
            {
                return OperationResult.NotFound();
            }

            if (!isAdmin && cls.OwnerId != userId)
            {
                return OperationResult.NotFound();
            }

            _context.Classes.Remove(cls);
            await _context.SaveChangesAsync();
            return OperationResult.Success();
        }

        public async Task<ClassDetailViewModel?> GetClassDetailAsync(int classId, int userId, bool isAdmin)
        {
            var classSummary = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new ClassSummaryViewModel
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    OwnerId = c.OwnerId,
                    Description = c.Description,
                    HasPassword = c.HasPassword,
                    ImageUrl = c.ImageUrl,
                    RequiresApproval = c.RequiresApproval,
                    IsChatLocked = c.IsChatLocked,
                    AllowMemberToPost = c.AllowMemberToPost
                })
                .FirstOrDefaultAsync();

            if (classSummary == null)
                return null;

            var memberInfo = await _context.ClassMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm =>
                    cm.ClassId == classId &&
                    cm.UserId == userId);

            var isOwner = classSummary.OwnerId == userId;
            var isAssistant = memberInfo?.Role == ClassRole.Assistant;
            var isMember = isOwner || memberInfo != null;

            var canViewPrivateContent =
                isOwner ||
                isMember ||
                isAdmin;

            var canChatWhenLocked =
                isOwner ||
                isAssistant ||
                isAdmin;

            var viewModel = new ClassDetailViewModel
            {
                Class = classSummary,
                CurrentUserId = userId,

                IsOwner = isOwner,
                IsAssistant = isAssistant,
                IsMember = isMember,
                IsAdmin = isAdmin,

                CanViewPrivateContent = canViewPrivateContent,

                // SỬA CHỖ NÀY: phó nhóm cũng được quản lý
                CanManageClass = isOwner || isAssistant || isAdmin,

                CanJoinClass = !isOwner && !isMember && !isAdmin,

                CanChatWhenLocked = canChatWhenLocked
            };

            if (!canViewPrivateContent)
                return viewModel;

            viewModel.Members = await _context.ClassMembers
                .AsNoTracking()
                .Where(cm => cm.ClassId == classId)
                .OrderBy(cm => cm.User.FullName)
                .Select(cm => new ClassMemberItemViewModel
                {
                    UserId = cm.UserId,
                    FullName = cm.User.FullName ?? string.Empty,
                    AvatarUrl = cm.User.AvatarUrl,

                    Role = cm.Role,
                    IsMuted = cm.IsMuted
                })
                .ToListAsync();

            viewModel.Messages = await _context.ClassMessages
                .AsNoTracking()
                .Where(m => m.ClassId == classId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ClassMessageViewModel
                {
                    MessageId = m.MessageId,
                    UserId = m.UserId,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    FullName = m.User.FullName ?? string.Empty,
                    IsMine = m.UserId == userId
                })
                .ToListAsync();

            viewModel.ClassFolders = await _context.ClassFolders
                .AsNoTracking()
                .Where(cf => cf.ClassId == classId)
                .OrderByDescending(cf => cf.AddedAt)
                .Select(cf => new ClassFolderItemViewModel
                {
                    FolderId = cf.FolderId,
                    FolderName = cf.Folder.FolderName,
                    AddedByUserId = cf.AddedByUserId,
                    AddedByUserName = cf.AddedByUser.FullName ?? string.Empty,
                    CanRemove = isOwner || isAdmin || cf.AddedByUserId == userId
                })
                .ToListAsync();

            viewModel.MyFolders = await _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.FolderName)
                .Select(f => new FolderOptionViewModel
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName
                })
                .ToListAsync();

            viewModel.SavedFolders = await _context.SavedFolders
                .AsNoTracking()
                .Where(sf => sf.UserId == userId && sf.Folder != null)
                .OrderBy(sf => sf.Folder!.FolderName)
                .Select(sf => new FolderOptionViewModel
                {
                    FolderId = sf.Folder!.FolderId,
                    FolderName = sf.Folder.FolderName
                })
                .ToListAsync();

            // NEW: lấy danh sách yêu cầu tham gia nếu lớp yêu cầu phê duyệt
            if (classSummary.RequiresApproval && (isOwner || isAssistant || isAdmin))
            {
                viewModel.JoinRequests = await _context.ClassJoinRequests
                    .AsNoTracking()
                    .Where(x =>
                        x.ClassId == classId &&
                        x.Status == JoinRequestStatus.Pending)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new ClassJoinRequestItemViewModel
                    {
                        RequestId = x.RequestId,
                        UserId = x.UserId,
                        FullName = x.User.FullName ?? string.Empty,
                        AvatarUrl = x.User.AvatarUrl,
                        RequestMessage = x.RequestMessage,
                        CreatedAt = x.CreatedAt
                    })
                    .ToListAsync();
            }

            return viewModel;
        }

        public async Task<OperationResult> AddFolderToClassAsync(int classId, int folderId, int userId, bool isAdmin)
        {
            var classData = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.OwnerId
                })
                .FirstOrDefaultAsync();

            if (classData == null)
            {
                return OperationResult.NotFound();
            }

            var isOwner = classData.OwnerId == userId;
            var isMember = isOwner || await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId);

            if (!isOwner && !isMember && !isAdmin)
            {
                _logger.LogWarning(
                    "Access denied when user {userId} tried to add folder {folderId} to class {classId}",
                    userId,
                    folderId,
                    classId);
                return OperationResult.NotFound();
            }

            var folder = await _context.Folders
                .AsNoTracking()
                .Where(f => f.FolderId == folderId)
                .Select(f => new
                {
                    f.FolderId,
                    f.UserId
                })
                .FirstOrDefaultAsync();

            if (folder == null)
            {
                return OperationResult.NotFound("Folder không tồn tại");
            }

            var hasSavedFolder = await _context.SavedFolders
                .AsNoTracking()
                .AnyAsync(sf => sf.UserId == userId && sf.FolderId == folderId);

            if (!isAdmin && folder.UserId != userId && !hasSavedFolder)
            {
                _logger.LogWarning(
                    "Folder ownership check failed for user {userId}, folder {folderId}, class {classId}",
                    userId,
                    folderId,
                    classId);
                return OperationResult.NotFound();
            }

            var exists = await _context.ClassFolders
                .AsNoTracking()
                .AnyAsync(cf => cf.ClassId == classId && cf.FolderId == folderId);

            if (exists)
            {
                _logger.LogInformation(
                    "Skipped adding existing folder {folderId} to class {classId} by user {userId}",
                    folderId,
                    classId,
                    userId);
                return OperationResult.Invalid("Folder đã tồn tại trong lớp");
            }

            _context.ClassFolders.Add(new ClassFolder
            {
                ClassId = classId,
                FolderId = folderId,
                AddedByUserId = userId
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Folder {folderId} added to class {classId} by user {userId}", folderId, classId, userId);
            return OperationResult.Success();
        }

        public async Task<OperationResult> RemoveFolderFromClassAsync(int classId, int folderId, int userId, bool isAdmin)
        {
            var item = await _context.ClassFolders
                .FirstOrDefaultAsync(x => x.ClassId == classId && x.FolderId == folderId);

            if (item == null)
            {
                return OperationResult.NotFound();
            }

            var classData = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.OwnerId
                })
                .FirstOrDefaultAsync();

            if (classData == null)
            {
                return OperationResult.NotFound();
            }

            if (!isAdmin && item.AddedByUserId != userId && classData.OwnerId != userId)
            {
                _logger.LogWarning(
                    "Access denied when user {userId} tried to remove folder {folderId} from class {classId}",
                    userId,
                    folderId,
                    classId);
                return OperationResult.NotFound();
            }

            _context.ClassFolders.Remove(item);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Folder {folderId} removed from class {classId} by user {userId}", folderId, classId, userId);
            return OperationResult.Success();
        }

        public async Task<OperationResult> KickMemberAsync(int classId, int memberUserId, int currentUserId, bool isAdmin)
        {
            var member = await _context.ClassMembers
                .FirstOrDefaultAsync(x => x.ClassId == classId && x.UserId == memberUserId);

            if (member == null)
            {
                return OperationResult.NotFound("Thành viên không tồn tại trong lớp.");
            }

            var classData = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.OwnerId
                })
                .FirstOrDefaultAsync();

            if (classData == null)
            {
                return OperationResult.NotFound("Lớp học không tồn tại.");
            }

            var currentMember = await _context.ClassMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClassId == classId && x.UserId == currentUserId);

            var isOwner = classData.OwnerId == currentUserId;
            var isAssistant = currentMember?.Role == ClassRole.Assistant;

            if (!isAdmin && !isOwner && !isAssistant)
            {
                _logger.LogWarning(
                    "Access denied when user {userId} tried to kick member {memberUserId} from class {classId}",
                    currentUserId,
                    memberUserId,
                    classId);
                return OperationResult.NotFound();
            }

            if (memberUserId == currentUserId)
            {
                return OperationResult.Invalid("Chủ lớp không thể tự kick chính mình.");
            }

            if (memberUserId == currentUserId)
            {
                return OperationResult.Invalid("KhÃ´ng thá»ƒ tá»± xÃ³a chÃ­nh mÃ¬nh khá»i lá»›p.");
            }

            if (member.Role == ClassRole.Owner || classData.OwnerId == memberUserId)
            {
                return OperationResult.Invalid("KhÃ´ng thá»ƒ xÃ³a trÆ°á»Ÿng nhÃ³m.");
            }

            if (isAssistant && member.Role != ClassRole.Member)
            {
                return OperationResult.Invalid("PhÃ³ nhÃ³m chá»‰ cÃ³ thá»ƒ xÃ³a thÃ nh viÃªn thÆ°á»ng.");
            }

            _context.ClassMembers.Remove(member);
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Member {memberUserId} kicked from class {classId} by user {userId}",
                memberUserId,
                classId,
                currentUserId);
            return OperationResult.Success();
        }

        public async Task<IReadOnlyList<ClassSearchResultViewModel>> SearchClassesAsync(string? keyword, int userId)
        {
            keyword = keyword?.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Array.Empty<ClassSearchResultViewModel>();
            }

            return await _context.Classes
                .AsNoTracking()
                .Where(c =>
                    c.ClassName.Contains(keyword) &&
                    c.OwnerId != userId &&
                    !c.ClassMembers.Any(cm => cm.UserId == userId))
                .Select(c => new ClassSearchResultViewModel
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    OwnerName = c.Owner.FullName ?? string.Empty,
                    HasPassword = c.HasPassword,
                    ImageUrl = c.ImageUrl
                })
                .ToListAsync();
        }

        public async Task<OperationResult> JoinClassAsync(
       int classId,
       string? password,
       int userId)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.ClassId,
                    c.HasPassword,
                    c.PasswordHash,
                    c.RequiresApproval
                })
                .FirstOrDefaultAsync();

            if (cls == null)
                return OperationResult.NotFound();

            var isBlacklisted = await _context.ClassBlacklists
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ClassId == classId &&
                    x.UserId == userId);

            if (isBlacklisted)
                return OperationResult.Invalid("Ban da bi chan khoi lop nay.");

            // =========================
            // CHECK PASSWORD
            // =========================

            if (cls.HasPassword)
            {
                var isPasswordValid =
                    !string.IsNullOrWhiteSpace(cls.PasswordHash) &&
                    !string.IsNullOrWhiteSpace(password) &&
                    BCrypt.Net.BCrypt.Verify(password, cls.PasswordHash);

                if (!isPasswordValid)
                    return OperationResult.Invalid("Mật khẩu lớp không đúng.");
            }

            // =========================
            // ALREADY MEMBER
            // =========================

            var alreadyMember = await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm =>
                    cm.ClassId == classId &&
                    cm.UserId == userId);

            if (alreadyMember)
                return OperationResult.Invalid("Bạn đã là thành viên của lớp.");

            // =========================
            // NEED APPROVAL
            // =========================

            if (cls.RequiresApproval)
            {
                var alreadyRequested = await _context.ClassJoinRequests
                    .AsNoTracking()
                    .AnyAsync(r =>
                        r.ClassId == classId &&
                        r.UserId == userId &&
                        r.Status == JoinRequestStatus.Pending);

                if (alreadyRequested)
                    return OperationResult.Invalid(
                        "Bạn đã gửi yêu cầu tham gia trước đó.");

                _context.ClassJoinRequests.Add(new ClassJoinRequest
                {
                    ClassId = classId,
                    UserId = userId,
                    Status = JoinRequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return OperationResult.Invalid(
                    "Yêu cầu tham gia đã được gửi. Vui lòng chờ trưởng nhóm phê duyệt.");
            }

            // =========================
            // JOIN DIRECTLY
            // =========================

            _context.ClassMembers.Add(new ClassMember
            {
                ClassId = classId,
                UserId = userId,
                Role = ClassRole.Member,
                JoinedAt = DateTime.UtcNow,
                IsMuted = false
            });

            await _context.SaveChangesAsync();

            return OperationResult.Success();
        }

        public async Task LeaveClassAsync(int classId, int userId)
        {
            var membership = await _context.ClassMembers
                .FirstOrDefaultAsync(x => x.ClassId == classId && x.UserId == userId);

            if (membership == null)
            {
                _logger.LogDebug("User {userId} attempted to leave class {classId} but was not a member", userId, classId);
                return;
            }

            _context.ClassMembers.Remove(membership);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {userId} left class {classId}", userId, classId);
        }

        public async Task<bool> CanAccessClassAsync(int classId, int userId, bool isAdmin)
        {
            if (isAdmin)
            {
                return true;
            }

            var isOwner = await _context.Classes
                .AsNoTracking()
                .AnyAsync(c => c.ClassId == classId && c.OwnerId == userId);

            if (isOwner)
            {
                return true;
            }

            return await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId);
        }

        private static void ApplyClassPassword(Class newClass, string? password)
        {
            if (!string.IsNullOrWhiteSpace(password))
            {
                newClass.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                newClass.HasPassword = true;
                return;
            }

            newClass.PasswordHash = null;
            newClass.HasPassword = false;
        }

        private async Task<string?> SaveClassAvatarAsync(IFormFile? avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var imageUrl = await _fileStorageService.SaveImageAsync(avatar, ImageUploadPolicies.ClassImage);
            _logger.LogInformation("Class image uploaded to {publicUrlPrefix}", ImageUploadPolicies.ClassImage.PublicUrlPrefix);
            return imageUrl;
        }
        // ===== THÊM VÀO ClassService.cs =====

public async Task<OperationResult> ChangeMemberRoleAsync(
    int classId,
    int targetUserId,
    string role,
    int currentUserId,
    bool isAdmin)
{
    var target = await _context.ClassMembers
        .FirstOrDefaultAsync(x =>
            x.ClassId == classId &&
            x.UserId == targetUserId);

    if (target == null)
        return OperationResult.NotFound("Không tìm thấy thành viên.");

    var current = await _context.ClassMembers
        .AsNoTracking()
        .FirstOrDefaultAsync(x =>
            x.ClassId == classId &&
            x.UserId == currentUserId);

    if (current == null && !isAdmin)
        return OperationResult.NotFound();

    bool canManage =
        isAdmin ||
        (current != null && current.Role == ClassRole.Owner);

    if (!canManage)
        return OperationResult.Invalid("Chỉ trưởng nhóm mới được phân quyền.");

    if (target.Role == ClassRole.Owner)
        return OperationResult.Invalid("Không thể chỉnh quyền trưởng nhóm.");

    if (!Enum.TryParse<ClassRole>(role, true, out var parsedRole))
        return OperationResult.Invalid("Role không hợp lệ.");

    if (parsedRole == ClassRole.Owner)
        return OperationResult.Invalid("Không thể chuyển trực tiếp thành trưởng nhóm.");

    target.Role = parsedRole;

    await _context.SaveChangesAsync();

    return OperationResult.Success();
}

        public async Task<OperationResult> ToggleMuteMemberAsync(
            int classId,
            int targetUserId,
            int currentUserId,
            bool isMute,
            bool isAdmin)
        {
            var current = await _context.ClassMembers
        .AsNoTracking()
        .FirstOrDefaultAsync(x =>
            x.ClassId == classId &&
            x.UserId == currentUserId);

    var target = await _context.ClassMembers
        .FirstOrDefaultAsync(x =>
            x.ClassId == classId &&
            x.UserId == targetUserId);

    if (target == null)
        return OperationResult.NotFound("Không tìm thấy thành viên.");

    if (target.UserId == currentUserId)
        return OperationResult.Invalid("Không thể tự mute chính mình.");

    bool canManage =
        isAdmin ||
        (current != null &&
            (current.Role == ClassRole.Owner ||
             current.Role == ClassRole.Assistant));

    if (!canManage)
        return OperationResult.Invalid("Bạn không có quyền mute.");

    if (target.Role == ClassRole.Owner)
        return OperationResult.Invalid("Không thể mute trưởng nhóm.");

    if (current != null &&
        current.Role == ClassRole.Assistant &&
        target.Role == ClassRole.Assistant)
    {
        return OperationResult.Invalid("Phó nhóm không thể mute phó nhóm khác.");
    }

    if (current != null &&
        current.Role == ClassRole.Assistant &&
        target.Role != ClassRole.Member)
    {
        return OperationResult.Invalid("Pho nhom chi co the mute thanh vien thuong.");
    }

    target.IsMuted = isMute;

    await _context.SaveChangesAsync();

    return OperationResult.Success();
}
        public async Task<OperationResult> BlockMemberAsync(
            int classId,
            int targetUserId,
            int currentUserId,
            bool isAdmin)
        {
            var classData = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.OwnerId
                })
                .FirstOrDefaultAsync();

            if (classData == null)
            {
                return OperationResult.NotFound("Khong tim thay lop hoc.");
            }

            if (!isAdmin && classData.OwnerId != currentUserId)
            {
                return OperationResult.Invalid("Chi truong nhom moi co the chan thanh vien.");
            }

            if (targetUserId == currentUserId || targetUserId == classData.OwnerId)
            {
                return OperationResult.Invalid("Khong the chan truong nhom.");
            }

            var targetUserExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(x => x.UserId == targetUserId);

            if (!targetUserExists)
            {
                return OperationResult.NotFound("Khong tim thay nguoi dung.");
            }

            var alreadyBlocked = await _context.ClassBlacklists
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ClassId == classId &&
                    x.UserId == targetUserId);

            if (alreadyBlocked)
            {
                return OperationResult.Invalid("Thanh vien nay da bi chan truoc do.");
            }

            var membership = await _context.ClassMembers
                .FirstOrDefaultAsync(x =>
                    x.ClassId == classId &&
                    x.UserId == targetUserId);

            if (membership != null && membership.Role == ClassRole.Owner)
            {
                return OperationResult.Invalid("Khong the chan truong nhom.");
            }

            if (membership != null)
            {
                _context.ClassMembers.Remove(membership);
            }

            var pendingRequests = await _context.ClassJoinRequests
                .Where(x =>
                    x.ClassId == classId &&
                    x.UserId == targetUserId &&
                    x.Status == JoinRequestStatus.Pending)
                .ToListAsync();

            foreach (var pendingRequest in pendingRequests)
            {
                pendingRequest.Status = JoinRequestStatus.Declined;
            }

            _context.ClassBlacklists.Add(new ClassBlacklist
            {
                ClassId = classId,
                UserId = targetUserId,
                BannedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return OperationResult.Success();
        }
        public async Task<OperationResult> ApproveJoinRequestAsync(
    int requestId,
    int currentUserId,
    bool isAdmin)
        {
            var request = await _context.ClassJoinRequests
                .FirstOrDefaultAsync(x =>
                    x.RequestId == requestId &&
                    x.Status == JoinRequestStatus.Pending);

            if (request == null)
                return OperationResult.NotFound("Không tìm thấy yêu cầu.");

            var cls = await _context.Classes
                .AsNoTracking()
                .Where(x => x.ClassId == request.ClassId)
                .Select(x => new
                {
                    x.OwnerId
                })
                .FirstOrDefaultAsync();

            if (cls == null)
                return OperationResult.NotFound("Không tìm thấy lớp.");

            bool canManage =
                isAdmin ||
                cls.OwnerId == currentUserId ||
                await _context.ClassMembers
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.ClassId == request.ClassId &&
                        x.UserId == currentUserId &&
                        x.Role == ClassRole.Assistant);

            if (!canManage)
                return OperationResult.Invalid("Bạn không có quyền duyệt yêu cầu.");

            var isBlacklistedRequestUser = await _context.ClassBlacklists
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ClassId == request.ClassId &&
                    x.UserId == request.UserId);

            if (isBlacklistedRequestUser)
                return OperationResult.Invalid("Khong the duyet nguoi dung da bi chan.");

            var alreadyMember = await _context.ClassMembers
                .AnyAsync(x =>
                    x.ClassId == request.ClassId &&
                    x.UserId == request.UserId);

            if (!alreadyMember)
            {
                _context.ClassMembers.Add(new ClassMember
                {
                    ClassId = request.ClassId,
                    UserId = request.UserId,
                    JoinedAt = DateTime.UtcNow,
                    Role = ClassRole.Member
                });
            }

            request.Status = JoinRequestStatus.Approved;

            await _context.SaveChangesAsync();

            return OperationResult.Success();
        }
        public async Task<OperationResult> DeclineJoinRequestAsync(
    int requestId,
    int currentUserId,
    bool isAdmin)
        {
            var request = await _context.ClassJoinRequests
                .FirstOrDefaultAsync(x =>
                    x.RequestId == requestId &&
                    x.Status == JoinRequestStatus.Pending);

            if (request == null)
                return OperationResult.NotFound("Không tìm thấy yêu cầu.");

            var cls = await _context.Classes
                .AsNoTracking()
                .Where(x => x.ClassId == request.ClassId)
                .Select(x => new
                {
                    x.OwnerId
                })
                .FirstOrDefaultAsync();

            if (cls == null)
                return OperationResult.NotFound("Không tìm thấy lớp.");

            bool canManage =
                isAdmin ||
                cls.OwnerId == currentUserId ||
                await _context.ClassMembers
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.ClassId == request.ClassId &&
                        x.UserId == currentUserId &&
                        x.Role == ClassRole.Assistant);

            if (!canManage)
                return OperationResult.Invalid("Bạn không có quyền từ chối yêu cầu.");

            request.Status = JoinRequestStatus.Declined;

            await _context.SaveChangesAsync();

            return OperationResult.Success();
        }
    }
}
