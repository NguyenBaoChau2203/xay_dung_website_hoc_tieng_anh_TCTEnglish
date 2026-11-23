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
        
        public async Task<ClassPageViewModel> GetClassPageAsync(int userId, string? role = null)
        {
            var normalizedRole = Roles.Normalize(role);
            var canCreate = Roles.CanCreateClass(normalizedRole);

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
                    IsOwner = c.OwnerId == userId,
                    IsAssistant = c.ClassMembers.Any(cm =>
                        cm.UserId == userId && cm.Role == ClassRole.Assistant),
                    HasPassword = c.HasPassword,
                    RequiresApproval = c.RequiresApproval,
                    IsPublic = c.IsPublic,
                    MemberCount = c.ClassMembers.Count,
                    PendingRequestCount = c.JoinRequests.Count(r => r.Status == JoinRequestStatus.Pending)
                })
                .ToListAsync();

            List<ClassCardViewModel> managed;
            List<ClassCardViewModel> joined;

            if (canCreate)
            {
                // Teacher/Admin "Lớp tôi tạo": owned first; joined others as secondary.
                managed = classes.Where(c => c.IsOwner).ToList();
                joined = classes.Where(c => !c.IsOwner).ToList();
            }
            else
            {
                managed = classes
                    .Where(c => c.IsOwner || c.IsAssistant)
                    .ToList();
                joined = classes
                    .Where(c => !c.IsOwner && !c.IsAssistant)
                    .ToList();
            }

            return new ClassPageViewModel
            {
                Classes = classes,
                ManagedClasses = managed,
                JoinedClasses = joined,
                CanCreateClass = canCreate,
                IsCreatorRole = canCreate,
                PageTitle = canCreate ? "Lớp tôi tạo" : "Lớp học của tôi",
                PageDescription = canCreate
                    ? "Lớp bạn đã tạo và lớp bạn đang tham gia"
                    : "Các lớp bạn đang tham gia"
            };
        }

        public async Task<int> CreateClassAsync(CreateClassViewModel model, int userId, string? role = null)
        {
            if (!Roles.CanCreateClass(role))
            {
                throw new InvalidOperationException("Chỉ giảng viên hoặc quản trị viên được tạo lớp học.");
            }

            _logger.LogInformation("Creating class for user {userId}", userId);

            var joinCode = await ResolveUniqueJoinCodeAsync(model.JoinCode);

            var newClass = new Class
            {
                ClassName = model.ClassName,
                Description = model.Description,
                OwnerId = userId,
                RequiresApproval = model.RequiresApproval,
                IsChatLocked = model.IsChatLocked,
                AllowMemberToPost = model.AllowMemberToPost,
                IsPublic = model.IsPublic,
                JoinCode = joinCode,
                CreatedAt = DateTime.UtcNow
            };

            ApplyClassPassword(newClass, model.Password);
            newClass.ImageUrl = await SaveClassAvatarAsync(model.Avatar);

            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            var ownerMember = new ClassMember
            {
                ClassId = newClass.ClassId,
                UserId = userId,
                Role = ClassRole.Owner,
                JoinedAt = DateTime.UtcNow
            };

            _context.ClassMembers.Add(ownerMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Class created with classId {classId} joinCode assigned by user {userId}",
                newClass.ClassId,
                userId);

            return newClass.ClassId;
        }

        public async Task<OperationResult> UpdateClassAsync(
            int classId,
            string className,
            string? description,
            bool requiresApproval,
            bool isChatLocked,
            bool allowMemberToPost,
            bool? isPublic,
            int userId,
            bool isAdmin)
        {
            var cls = await _context.Classes
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (cls == null) return OperationResult.NotFound();

            if (!isAdmin && cls.OwnerId != userId) return OperationResult.NotFound();

            cls.ClassName = className;
            cls.Description = description;
            cls.RequiresApproval = requiresApproval;
            cls.IsChatLocked = isChatLocked;
            cls.AllowMemberToPost = allowMemberToPost;
            if (cls.IsPublic && isPublic == false)
                cls.JoinCode = await ResolveUniqueJoinCodeAsync(null);

            cls.IsPublic = isPublic ?? cls.IsPublic;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Class {classId} updated by user {userId}", classId, userId);

            return OperationResult.Success();
        }

        public async Task<OperationResult> UpdateClassCoverAsync(int classId, IFormFile cover, int userId, bool isAdmin)
        {
            var cls = await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);
            if (cls == null)
            {
                return OperationResult.NotFound();
            }

            if (!isAdmin && cls.OwnerId != userId)
            {
                return OperationResult.NotFound();
            }

            if (cover == null || cover.Length == 0)
            {
                return OperationResult.Invalid("Ảnh bìa không hợp lệ.");
            }

            try
            {
                cls.ImageUrl = await SaveClassAvatarAsync(cover);
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult.Invalid(ex.Message);
            }

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
                    ImageUrl = c.ImageUrl,
                    RequiresApproval = c.RequiresApproval,
                    IsChatLocked = c.IsChatLocked,
                    AllowMemberToPost = c.AllowMemberToPost,
                    IsPublic = c.IsPublic,
                    JoinCode = c.JoinCode
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

            if (!classSummary.IsPublic && !canViewPrivateContent)
                return null;

            var canManageClass = isOwner || isAssistant || isAdmin;

            var canChatWhenLocked =
                isOwner ||
                isAssistant ||
                isAdmin;

            var isMutedForCurrentUser = memberInfo?.IsMuted == true;
            var canSendChat =
                canViewPrivateContent &&
                !isMutedForCurrentUser &&
                (!classSummary.IsChatLocked || canChatWhenLocked);

            // Join code is manager-only; strip for everyone else.
            if (!canManageClass)
            {
                classSummary.JoinCode = null;
            }

            var viewModel = new ClassDetailViewModel
            {
                Class = classSummary,
                CurrentUserId = userId,

                IsOwner = isOwner,
                IsAssistant = isAssistant,
                IsMember = isMember,
                IsAdmin = isAdmin,

                CanViewPrivateContent = canViewPrivateContent,

                CanManageClass = canManageClass,

                CanJoinClass = !isOwner && !isMember && !isAdmin,

                CanChatWhenLocked = canChatWhenLocked,
                IsMutedForCurrentUser = isMutedForCurrentUser,
                CanSendChat = canSendChat
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

            const int initialMessageTake = 50;
            var latestMessages = await _context.ClassMessages
                .AsNoTracking()
                .Where(m => m.ClassId == classId)
                .OrderByDescending(m => m.MessageId)
                .Take(initialMessageTake + 1)
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

            viewModel.HasMoreMessages = latestMessages.Count > initialMessageTake;
            viewModel.Messages = latestMessages
                .Take(initialMessageTake)
                .Reverse()
                .ToList();

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

            if (isOwner || isAssistant || isAdmin)
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
                    c.OwnerId,
                    c.AllowMemberToPost
                })
                .FirstOrDefaultAsync();

            if (classData == null)
            {
                return OperationResult.NotFound();
            }

            var isOwner = classData.OwnerId == userId;
            var membership = await _context.ClassMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ClassId == classId && cm.UserId == userId);
            var isMember = isOwner || membership != null;
            var isAssistant = membership?.Role == ClassRole.Assistant;

            if (!isOwner && !isMember && !isAdmin)
            {
                _logger.LogWarning(
                    "Access denied when user {userId} tried to add folder {folderId} to class {classId}",
                    userId,
                    folderId,
                    classId);
                return OperationResult.NotFound();
            }

            if (!isAdmin && !isOwner && !isAssistant && !classData.AllowMemberToPost)
            {
                _logger.LogWarning(
                    "Member post denied when user {userId} tried to add folder {folderId} to class {classId}",
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

        public Task<IReadOnlyList<ClassSearchResultViewModel>> SearchClassesAsync(
            string? keyword,
            int userId,
            string? role = null)
        {
            return BrowseClassesAsync(keyword, userId, role);
        }

        public async Task<IReadOnlyList<ClassSearchResultViewModel>> BrowseClassesAsync(
            string? keyword,
            int userId,
            string? role)
        {
            const int suggestTake = 12;
            keyword = keyword?.Trim();
            var normalizedRole = Roles.Normalize(role);
            var isAdmin = normalizedRole == Roles.Admin;
            var isTeacher = normalizedRole == Roles.Teacher;

            var query = _context.Classes.AsNoTracking();

            if (isAdmin)
            {
                // Admin sees every public and private class.
            }
            else if (isTeacher)
            {
                // Teacher: public of others (not yet joined) + own private only.
                query = query.Where(c =>
                    (c.OwnerId == userId && !c.IsPublic) ||
                    (c.IsPublic &&
                     c.OwnerId != userId &&
                     !c.ClassMembers.Any(cm => cm.UserId == userId)));
            }
            else
            {
                // Learners: public only, not already joined.
                query = query.Where(c =>
                    c.IsPublic &&
                    c.OwnerId != userId &&
                    !c.ClassMembers.Any(cm => cm.UserId == userId));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(c => c.ClassName.Contains(keyword));
            }

            return await query
                .OrderByDescending(c => c.ClassId)
                .Take(string.IsNullOrWhiteSpace(keyword) ? suggestTake : 50)
                .Select(c => new ClassSearchResultViewModel
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    OwnerName = c.Owner.FullName ?? string.Empty,
                    HasPassword = c.HasPassword,
                    RequiresApproval = c.RequiresApproval,
                    IsPublic = c.IsPublic,
                    ImageUrl = c.ImageUrl
                })
                .ToListAsync();
        }

        public Task<OperationResult<JoinClassResult>> JoinClassAsync(
            int classId,
            string? password,
            int userId)
        {
            return CompleteJoinAsync(classId, password, userId);
        }

        public async Task<OperationResult<JoinClassResult>> JoinClassByCodeAsync(
            string? joinCode,
            string? password,
            int userId)
        {
            var normalized = ClassJoinCode.Normalize(joinCode);
            if (string.IsNullOrEmpty(normalized))
            {
                return OperationResult<JoinClassResult>.Invalid("Vui lòng nhập mã lớp.");
            }

            var classId = await _context.Classes
                .AsNoTracking()
                .Where(c => c.JoinCode == normalized)
                .Select(c => (int?)c.ClassId)
                .FirstOrDefaultAsync();

            if (!classId.HasValue)
            {
                return OperationResult<JoinClassResult>.Invalid("Mã lớp không đúng hoặc không tồn tại.");
            }

            return await CompleteJoinAsync(classId.Value, password, userId, normalized);
        }

        public async Task<OperationResult<string>> RegenerateJoinCodeAsync(int classId, int userId, bool isAdmin)
        {
            var cls = await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);
            if (cls == null)
            {
                return OperationResult<string>.NotFound();
            }

            var isAssistant = await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm =>
                    cm.ClassId == classId &&
                    cm.UserId == userId &&
                    cm.Role == ClassRole.Assistant);

            if (!isAdmin && cls.OwnerId != userId && !isAssistant)
            {
                return OperationResult<string>.NotFound();
            }

            cls.JoinCode = await ResolveUniqueJoinCodeAsync(null);
            await _context.SaveChangesAsync();

            return OperationResult<string>.Success(cls.JoinCode);
        }

        private async Task<OperationResult<JoinClassResult>> CompleteJoinAsync(
            int classId,
            string? password,
            int userId,
            string? joinCode = null)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId && (joinCode == null ? c.IsPublic : c.JoinCode == joinCode))
                .Select(c => new
                {
                    c.ClassId,
                    c.OwnerId,
                    c.HasPassword,
                    c.PasswordHash,
                    c.RequiresApproval
                })
                .FirstOrDefaultAsync();

            if (cls == null)
                return OperationResult<JoinClassResult>.NotFound();

            JoinClassResult Pack(JoinClassOutcome outcome) => new()
            {
                Outcome = outcome,
                ClassId = classId
            };

            if (cls.OwnerId == userId)
            {
                return OperationResult<JoinClassResult>.Invalid("Bạn đã là chủ lớp này.");
            }

            var isBlacklisted = await _context.ClassBlacklists
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ClassId == classId &&
                    x.UserId == userId);

            if (isBlacklisted)
                return OperationResult<JoinClassResult>.Invalid("Bạn đã bị chặn khỏi lớp này.");

            var alreadyMember = await _context.ClassMembers
                .AsNoTracking()
                .AnyAsync(cm =>
                    cm.ClassId == classId &&
                    cm.UserId == userId);

            if (alreadyMember)
                return OperationResult<JoinClassResult>.Invalid("Bạn đã là thành viên của lớp.");

            if (cls.HasPassword)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    return OperationResult<JoinClassResult>.Success(Pack(JoinClassOutcome.NeedsPassword));
                }

                var isPasswordValid =
                    !string.IsNullOrWhiteSpace(cls.PasswordHash) &&
                    BCrypt.Net.BCrypt.Verify(password, cls.PasswordHash);

                if (!isPasswordValid)
                    return OperationResult<JoinClassResult>.Invalid("Mật khẩu lớp không đúng.");
            }

            if (cls.RequiresApproval)
            {
                var alreadyRequested = await _context.ClassJoinRequests
                    .AsNoTracking()
                    .AnyAsync(r =>
                        r.ClassId == classId &&
                        r.UserId == userId &&
                        r.Status == JoinRequestStatus.Pending);

                if (alreadyRequested)
                    return OperationResult<JoinClassResult>.Invalid(
                        "Bạn đã gửi yêu cầu tham gia trước đó.");

                _context.ClassJoinRequests.Add(new ClassJoinRequest
                {
                    ClassId = classId,
                    UserId = userId,
                    Status = JoinRequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return OperationResult<JoinClassResult>.Success(Pack(JoinClassOutcome.Pending));
            }

            _context.ClassMembers.Add(new ClassMember
            {
                ClassId = classId,
                UserId = userId,
                Role = ClassRole.Member,
                JoinedAt = DateTime.UtcNow,
                IsMuted = false
            });

            await _context.SaveChangesAsync();

            return OperationResult<JoinClassResult>.Success(Pack(JoinClassOutcome.Joined));
        }

        private async Task<string> ResolveUniqueJoinCodeAsync(string? preferred)
        {
            var candidate = ClassJoinCode.Normalize(preferred);
            if (!ClassJoinCode.IsValidFormat(candidate))
            {
                candidate = ClassJoinCode.Generate();
            }

            for (var attempt = 0; attempt < 16; attempt++)
            {
                var exists = await _context.Classes
                    .AsNoTracking()
                    .AnyAsync(c => c.JoinCode == candidate);
                if (!exists)
                {
                    return candidate;
                }

                candidate = ClassJoinCode.Generate();
            }

            throw new InvalidOperationException("Không tạo được mã lớp duy nhất. Vui lòng thử lại.");
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

        public async Task<OperationResult> EnsureCanSendClassChatAsync(int classId, int userId, bool isAdmin)
        {
            if (!await CanAccessClassAsync(classId, userId, isAdmin))
            {
                return OperationResult.NotFound("Không có quyền truy cập lớp.");
            }

            var classFlags = await _context.Classes
                .AsNoTracking()
                .Where(c => c.ClassId == classId)
                .Select(c => new
                {
                    c.OwnerId,
                    c.IsChatLocked
                })
                .FirstOrDefaultAsync();

            if (classFlags == null)
            {
                return OperationResult.NotFound("Không tìm thấy lớp.");
            }

            var membership = await _context.ClassMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.ClassId == classId && cm.UserId == userId);

            if (membership?.IsMuted == true)
            {
                return OperationResult.Invalid("Bạn đang bị mute và không thể gửi tin nhắn.");
            }

            var canChatWhenLocked =
                isAdmin ||
                classFlags.OwnerId == userId ||
                membership?.Role == ClassRole.Owner ||
                membership?.Role == ClassRole.Assistant;

            if (classFlags.IsChatLocked && !canChatWhenLocked)
            {
                return OperationResult.Invalid("Chat đang bị khóa. Chỉ người quản lý lớp mới có thể nhắn tin.");
            }

            return OperationResult.Success();
        }

        public async Task<ClassMessagesPageViewModel> GetClassMessagesAsync(
            int classId,
            int userId,
            bool isAdmin,
            int? beforeMessageId,
            int take)
        {
            if (take <= 0)
            {
                take = 50;
            }

            take = Math.Min(take, 100);

            if (!await CanAccessClassAsync(classId, userId, isAdmin))
            {
                return new ClassMessagesPageViewModel();
            }

            var query = _context.ClassMessages
                .AsNoTracking()
                .Where(m => m.ClassId == classId);

            if (beforeMessageId.HasValue)
            {
                query = query.Where(m => m.MessageId < beforeMessageId.Value);
            }

            var page = await query
                .OrderByDescending(m => m.MessageId)
                .Take(take + 1)
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

            var hasMore = page.Count > take;
            var messages = page.Take(take).Reverse().ToList();

            return new ClassMessagesPageViewModel
            {
                Messages = messages,
                HasMore = hasMore
            };
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
