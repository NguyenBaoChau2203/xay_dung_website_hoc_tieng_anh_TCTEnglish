// Template: Service Interface + Implementation
// Usage: Copy this template when creating a new Service for TCTEnglish
// Files:
//   - TCTEnglish/Services/I{Feature}Service.cs   (interface)
//   - TCTEnglish/Services/{Feature}Service.cs     (implementation)
//
// Register in Program.cs:
//   builder.Services.AddScoped<I{Feature}Service, {Feature}Service>();

// ============================================================
// FILE 1: I{Feature}Service.cs
// ============================================================

using TCTEnglish.ViewModels;

namespace TCTEnglish.Services
{
    public interface I{Feature}Service
    {
        Task<List<{Feature}ViewModel>> GetAllAsync(int userId);
        Task<{Feature}ViewModel?> GetByIdAsync(int id, int userId);
        Task<ServiceResult> CreateAsync({Feature}ViewModel model, int userId);
        Task<ServiceResult> UpdateAsync(int id, {Feature}ViewModel model, int userId);
        Task<ServiceResult> DeleteAsync(int id, int userId);
    }

    // Shared result wrapper for service operations
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int? CreatedId { get; set; }

        public static ServiceResult Ok(int? id = null)
            => new() { Success = true, CreatedId = id };

        public static ServiceResult Fail(string error)
            => new() { Success = false, Error = error };
    }
}


// ============================================================
// FILE 2: {Feature}Service.cs
// ============================================================

using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.ViewModels;

namespace TCTEnglish.Services
{
    public class {Feature}Service : I{Feature}Service
    {
        private readonly DbflashcardContext _context;
        private readonly ILogger<{Feature}Service> _logger;

        public {Feature}Service(
            DbflashcardContext context,
            ILogger<{Feature}Service> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<{Feature}ViewModel>> GetAllAsync(int userId)
        {
            return await _context.{Entities}
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new {Feature}ViewModel
                {
                    Id = e.Id,
                    // Map properties here
                })
                .ToListAsync();
        }

        public async Task<{Feature}ViewModel?> GetByIdAsync(int id, int userId)
        {
            return await _context.{Entities}
                .AsNoTracking()
                .Where(e => e.Id == id && e.UserId == userId)  // Anti-IDOR
                .Select(e => new {Feature}ViewModel
                {
                    Id = e.Id,
                    // Map properties here
                })
                .FirstOrDefaultAsync();
        }

        public async Task<ServiceResult> CreateAsync({Feature}ViewModel model, int userId)
        {
            try
            {
                var entity = new {Entity}
                {
                    UserId = userId,
                    // Map model properties here
                    CreatedAt = DateTime.UtcNow
                };

                _context.{Entities}.Add(entity);
                await _context.SaveChangesAsync();

                return ServiceResult.Ok(entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {Feature} for user {UserId}", userId);
                return ServiceResult.Fail("Failed to create. Please try again.");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int id, {Feature}ViewModel model, int userId)
        {
            var entity = await _context.{Entities}
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);  // Anti-IDOR

            if (entity == null)
                return ServiceResult.Fail("Not found.");

            // Update properties here
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAsync(int id, int userId)
        {
            var entity = await _context.{Entities}
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);  // Anti-IDOR

            if (entity == null)
                return ServiceResult.Fail("Not found.");

            _context.{Entities}.Remove(entity);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }
    }
}
