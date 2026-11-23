using Microsoft.AspNetCore.Http;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IClassService
    {
        Task<ClassPageViewModel> GetClassPageAsync(int userId, string? role = null);
        Task<int> CreateClassAsync(CreateClassViewModel model, int userId, string? role = null);
        Task<IReadOnlyList<ClassSearchResultViewModel>> BrowseClassesAsync(
            string? keyword,
            int userId,
            string? role);
        Task<OperationResult> UpdateClassAsync(
            int classId,
            string className,
            string? description,
            bool requiresApproval,
            bool isChatLocked,
            bool allowMemberToPost,
            bool isPublic,
            int userId,
            bool isAdmin);
        Task<OperationResult> UpdateClassCoverAsync(int classId, IFormFile cover, int userId, bool isAdmin);
        Task<OperationResult> DeleteClassAsync(int classId, int userId, bool isAdmin);
        Task<ClassDetailViewModel?> GetClassDetailAsync(int classId, int userId, bool isAdmin);
        Task<OperationResult> AddFolderToClassAsync(int classId, int folderId, int userId, bool isAdmin);
        Task<OperationResult> RemoveFolderFromClassAsync(int classId, int folderId, int userId, bool isAdmin);
        Task<OperationResult> KickMemberAsync(int classId, int memberUserId, int currentUserId, bool isAdmin);
        Task<IReadOnlyList<ClassSearchResultViewModel>> SearchClassesAsync(string? keyword, int userId, string? role = null);
        Task<OperationResult<JoinClassResult>> JoinClassAsync(int classId, string? password, int userId);
        Task<OperationResult<JoinClassResult>> JoinClassByCodeAsync(string? joinCode, string? password, int userId);
        Task<OperationResult<string>> RegenerateJoinCodeAsync(int classId, int userId, bool isAdmin);
        Task LeaveClassAsync(int classId, int userId);
        Task<bool> CanAccessClassAsync(int classId, int userId, bool isAdmin);
        Task<OperationResult> EnsureCanSendClassChatAsync(int classId, int userId, bool isAdmin);
        Task<ClassMessagesPageViewModel> GetClassMessagesAsync(
            int classId,
            int userId,
            bool isAdmin,
            int? beforeMessageId,
            int take);
        Task<OperationResult> ChangeMemberRoleAsync(int classId, int targetUserId, string role, int currentUserId, bool isAdmin);
        Task<OperationResult> ToggleMuteMemberAsync(int classId, int targetUserId, int currentUserId, bool isMute, bool isAdmin);
        Task<OperationResult> BlockMemberAsync(int classId, int targetUserId, int currentUserId, bool isAdmin);
        Task<OperationResult> ApproveJoinRequestAsync(int requestId, int currentUserId, bool isAdmin);
        Task<OperationResult> DeclineJoinRequestAsync(int requestId, int currentUserId, bool isAdmin);
    }
}
