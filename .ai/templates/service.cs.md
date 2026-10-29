# Service template guidance

Read AGENTS.md and tct-feature-scaffold. Resolve existing type namespaces and
method signatures through CodeGraph before adapting this scaffold. New code uses
TCTEnglish namespaces; some existing shared types still use TCTVocabulary.

The verified shared result is Services/OperationResult.cs. It exposes Status and
ErrorMessage, with Success(), NotFound(...) and Invalid(...). Do not generate a
second ServiceResult type or assume a boolean Success property/CreatedId exists.

```csharp
// Example alias for the currently verified legacy result namespace:
using OperationResult = TCTVocabulary.Services.OperationResult;

namespace TCTEnglish.Services;

public interface I{Feature}Service
{
    Task<{Feature}ViewModel?> GetByIdAsync(int id, int userId);
    Task<OperationResult> DeleteAsync(int id, int userId);
}
```

For an implementation inject DbflashcardContext through the constructor. Project
read-only queries into typed ViewModels with AsNoTracking. For a mutation, locate
the entity with both the requested ID and the actual owner/access predicate, return
OperationResult.NotFound() if absent, mutate and await SaveChangesAsync(), then
return OperationResult.Success(). Match real entity keys and owner properties;
not every domain uses Id/UserId. Keep database work in the feature service.

Registration in Program.cs is required for new services but editing that protected
file needs explicit authority. A scaffold request alone does not authorize it.
