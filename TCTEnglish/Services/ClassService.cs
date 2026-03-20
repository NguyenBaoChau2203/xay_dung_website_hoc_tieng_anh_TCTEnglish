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

            var newClass = new Class
            {
                ClassName = model.ClassName,
                Description = model.Description,
                OwnerId = userId
            };

            ApplyClassPassword(newClass, model.Password);
            newClass.ImageUrl = await SaveClassAvatarAsync(model.Avatar);

            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Class created with classId {classId} by user {userId}", newClass.ClassId, userId);

            return newClass.ClassId;
        }

        public async Task<OperationResult> UpdateClassAsync(
            int classId,
            string className,
            string? description,
            int userId,
            bool isAdmin)
        {
            var cls = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (cls == null)
            {
                return OperationResult.NotFound();
            }

            if (!isAdmin && cls.OwnerId != userId)
            {
                return OperationResult.NotFound();
            }

            cls.ClassName = className;
            cls.Description = description;

            await _context.SaveChangesAsync();
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
                    ImageUrl = c.ImageUrl
                })
                .FirstOrDefaultAsync();

            if (classSummary == null)
            {
                return null;
            }

            var isOwner = classSummary.OwnerId == userId;
            var isMember = isOwner || await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId);
            var canViewPrivateContent = isOwner || isMember || isAdmin;

            var viewModel = new ClassDetailViewModel
            {
                Class = classSummary,
                CurrentUserId = userId,
                IsOwner = isOwner,
                IsMember = isMember,
                IsAdmin = isAdmin,
                CanViewPrivateContent = canViewPrivateContent,
                CanManageClass = isOwner || isAdmin,
                CanJoinClass = !isOwner && !isMember && !isAdmin
            };

            if (!canViewPrivateContent)
            {
                return viewModel;
            }

            viewModel.Members = await _context.ClassMembers
                .AsNoTracking()
                .Where(cm => cm.ClassId == classId)
                .OrderBy(cm => cm.User.FullName)
                .Select(cm => new ClassMemberItemViewModel
                {
                    UserId = cm.UserId,
                    FullName = cm.User.FullName ?? string.Empty
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

            if (!isAdmin && classData.OwnerId != currentUserId)
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

        public async Task<OperationResult> JoinClassAsync(int classId, string? password, int userId)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.ClassId,
                    c.HasPassword,
                    c.PasswordHash
                })
                .FirstOrDefaultAsync();

            if (cls == null)
            {
                _logger.LogWarning("Class {classId} not found when user {userId} attempted to join", classId, userId);
                return OperationResult.NotFound();
            }

            if (cls.HasPassword)
            {
                var isPasswordValid = !string.IsNullOrWhiteSpace(cls.PasswordHash)
                    && !string.IsNullOrWhiteSpace(password)
                    && BCrypt.Net.BCrypt.Verify(password, cls.PasswordHash);

                if (!isPasswordValid)
                {
                    _logger.LogWarning("Invalid class password for user {userId} joining class {classId}", userId, classId);
                    return OperationResult.Invalid("Mật khẩu lớp không đúng.");
                }
            }

            var exists = await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId);

            if (!exists)
            {
                _context.ClassMembers.Add(new ClassMember
                {
                    ClassId = classId,
                    UserId = userId
                });

                await _context.SaveChangesAsync();
                _logger.LogInformation("User {userId} joined class {classId}", userId, classId);
            }
            else
            {
                _logger.LogDebug("User {userId} is already a member of class {classId}", userId, classId);
            }

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
    }
}
