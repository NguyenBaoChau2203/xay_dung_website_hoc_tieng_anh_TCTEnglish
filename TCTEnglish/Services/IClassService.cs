using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IClassService
    {
        Task<ClassPageViewModel> GetClassPageAsync(int userId);
        Task<int> CreateClassAsync(CreateClassViewModel model, int userId);
        Task<OperationResult> UpdateClassAsync(int classId, string className, string? description, int userId, bool isAdmin);
        Task<OperationResult> DeleteClassAsync(int classId, int userId, bool isAdmin);
        Task<ClassDetailViewModel?> GetClassDetailAsync(int classId, int userId, bool isAdmin);
        Task<OperationResult> AddFolderToClassAsync(int classId, int folderId, int userId, bool isAdmin);
        Task<OperationResult> RemoveFolderFromClassAsync(int classId, int folderId, int userId, bool isAdmin);
        Task<OperationResult> KickMemberAsync(int classId, int memberUserId, int currentUserId, bool isAdmin);
        Task<IReadOnlyList<ClassSearchResultViewModel>> SearchClassesAsync(string? keyword, int userId);
        Task<OperationResult> JoinClassAsync(int classId, string? password, int userId);
        Task LeaveClassAsync(int classId, int userId);
        Task<bool> CanAccessClassAsync(int classId, int userId, bool isAdmin);
    }
}
