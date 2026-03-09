{{!-- Template: Razor View --}}
{{!-- Usage: Copy this template when creating a new Razor View for TCTEnglish --}}
{{!-- File location: TCTEnglish/Views/{Controller}/{Action}.cshtml --}}
{{!-- --}}
{{!-- Instructions: --}}
{{!--   1. Replace {Feature} with your feature name --}}
{{!--   2. Replace @model declaration with your actual ViewModel type --}}
{{!--   3. Customize the content area with your feature-specific HTML --}}
{{!--   4. Add feature-specific CSS/JS in the sections at the bottom --}}

@model TCTEnglish.ViewModels.{Feature}ViewModel
@{
    ViewData["Title"] = "{Feature}";
    ViewData["ActivePage"] = "{feature}";  // Used by _Layout.cshtml to highlight nav item
}

<div class="container-fluid py-4">

    {{!-- Page Header --}}
    <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
            <h1 class="h3 mb-1">{Feature}</h1>
            <p class="text-muted mb-0">Manage your {feature}s</p>
        </div>
        <div>
            <a asp-action="Create" class="btn btn-primary">
                <i class="bi bi-plus-lg me-2"></i>New {Feature}
            </a>
        </div>
    </div>

    {{!-- Alert Messages (from TempData) --}}
    @if (TempData["Success"] != null)
    {
        <div class="alert alert-success alert-dismissible fade show" role="alert">
            <i class="bi bi-check-circle-fill me-2"></i>@TempData["Success"]
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    }
    @if (TempData["Error"] != null)
    {
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <i class="bi bi-exclamation-circle-fill me-2"></i>@TempData["Error"]
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    }

    {{!-- Main Content Area --}}
    @if (Model.Items?.Any() == true)
    {
        <div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-4">
            @foreach (var item in Model.Items)
            {
                <div class="col">
                    <div class="card h-100 shadow-sm" style="border-radius: 12px;">
                        <div class="card-body">
                            <h6 class="card-title fw-semibold mb-1">@item.Name</h6>
                            <p class="text-muted small mb-0">@item.Description</p>
                        </div>
                        <div class="card-footer bg-transparent border-0 d-flex gap-2">
                            <a asp-action="Details" asp-route-id="@item.Id"
                               class="btn btn-sm btn-primary flex-grow-1">View</a>
                            @if (item.IsOwner)
                            {
                                <a asp-action="Edit" asp-route-id="@item.Id"
                                   class="btn btn-sm btn-outline-secondary">Edit</a>
                                <button class="btn btn-sm btn-outline-danger"
                                        onclick="showDeleteModal('@item.Name', '@Url.Action("Delete")/@item.Id')">
                                    <i class="bi bi-trash"></i>
                                </button>
                            }
                        </div>
                    </div>
                </div>
            }
        </div>
    }
    else
    {
        {{!-- Empty State --}}
        <div class="text-center py-5 text-muted">
            <i class="bi bi-inbox display-3 d-block mb-3 opacity-50"></i>
            <h5 class="mb-2">No {feature}s yet</h5>
            <p class="mb-4">Create your first {feature} to get started.</p>
            <a asp-action="Create" class="btn btn-primary">
                <i class="bi bi-plus-lg me-2"></i>Create First {Feature}
            </a>
        </div>
    }

</div>

{{!-- Delete Confirmation Modal --}}
<div class="modal fade" id="deleteModal" tabindex="-1" role="dialog"
     aria-labelledby="deleteModalLabel" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
            <div class="modal-header border-0">
                <h5 class="modal-title text-danger" id="deleteModalLabel">
                    <i class="bi bi-exclamation-triangle-fill me-2"></i>Confirm Delete
                </h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <p id="deleteModalMessage">Are you sure you want to delete this item?</p>
                <p class="text-muted small mb-0">This action cannot be undone.</p>
            </div>
            <div class="modal-footer border-0">
                <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
                <form id="deleteForm" method="post">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="btn btn-danger">Delete</button>
                </form>
            </div>
        </div>
    </div>
</div>

@section Styles {
    {{!-- Optional: page-specific CSS --}}
    @* <link rel="stylesheet" href="~/css/{feature}.css" asp-append-version="true" /> *@
}

@section Scripts {
    @* Page-specific JavaScript only *@
    <script>
        function showDeleteModal(itemName, deleteUrl) {
            document.getElementById('deleteModalMessage').textContent =
                `Delete "${itemName}"? This cannot be undone.`;
            document.getElementById('deleteForm').action = deleteUrl;
            new bootstrap.Modal(document.getElementById('deleteModal')).show();
        }
    </script>
    @* For complex JS, move to wwwroot/js/{feature}.js: *@
    @* <script src="~/js/{feature}.js" asp-append-version="true"></script> *@
}
