// Template: EF Core Entity
// Usage: Copy this template when creating a new database entity for TCTEnglish
// File location: TCTEnglish/Models/{EntityName}.cs
//
// After creating the entity:
//   1. Add DbSet to DbflashcardContext: public DbSet<{EntityName}> {EntityNamePlural} { get; set; }
//   2. Configure in OnModelCreating if needed (indexes, constraints)
//   3. Run: Add-Migration Add{EntityName} -Context DbflashcardContext
//   4. Review the migration file for safety
//   5. Run: Update-Database

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TCTEnglish.Models
{
    public class {EntityName}
    {
        // ============================================================
        // Primary Key
        // ============================================================
        [Key]
        public int Id { get; set; }

        // ============================================================
        // Required Properties
        // ============================================================
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // ============================================================
        // Optional Properties (nullable = migration-safe)
        // ============================================================
        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }  // e.g., "active", "archived"

        // ============================================================
        // Foreign Key — Owner/User relationship
        // ============================================================
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // ============================================================
        // Optional: Secondary Foreign Key
        // ============================================================
        public int? ParentId { get; set; }  // For hierarchical data

        [ForeignKey("ParentId")]
        public virtual {EntityName}? Parent { get; set; }

        // ============================================================
        // Timestamps (always UTC)
        // ============================================================
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // ============================================================
        // Navigation Properties (collections)
        // ============================================================
        public virtual ICollection<{RelatedEntity}> {RelatedEntities} { get; set; }
            = new List<{RelatedEntity}>();
    }
}


// ============================================================
// DbflashcardContext Registration (add to DbflashcardContext.cs)
// ============================================================

// In DbflashcardContext class:
/*
public DbSet<{EntityName}> {EntityNamePlural} { get; set; }

// In OnModelCreating:
modelBuilder.Entity<{EntityName}>(entity =>
{
    // String length constraints:
    entity.Property(e => e.Name).HasMaxLength(200);

    // Performance indexes (for frequently queried columns):
    entity.HasIndex(e => e.UserId).HasDatabaseName("IX_{EntityNamePlural}_UserId");
    entity.HasIndex(e => e.Status).HasDatabaseName("IX_{EntityNamePlural}_Status");

    // Relationships:
    entity.HasOne(e => e.User)
          .WithMany()
          .HasForeignKey(e => e.UserId)
          .OnDelete(DeleteBehavior.Cascade);  // or Restrict if you don't want cascade delete

    // Self-referencing (for hierarchical/nested):
    entity.HasOne(e => e.Parent)
          .WithMany(e => e.Children)
          .HasForeignKey(e => e.ParentId)
          .OnDelete(DeleteBehavior.Restrict);
});
*/


// ============================================================
// Migration Command
// ============================================================
// In Package Manager Console:
//   Add-Migration Add{EntityName} -Context DbflashcardContext
//
// Generated migration should ONLY contain AddTable operations.
// Review for any unexpected DROP operations before applying.
//
// Apply:
//   Update-Database
