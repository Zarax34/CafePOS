using System.Text.Json;

namespace CafePOS.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "cashier";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string PermissionsJson { get; set; } = "{}";

    public bool IsManager => Role == "manager";
    public bool IsStoreManager => Role == "storemanager";
    public bool IsCashier => Role == "cashier";

    private Dictionary<string, bool>? _permissions;

    /// <summary>
    /// Explicit permissions for this user (empty means role-based defaults apply).
    /// </summary>
    public Dictionary<string, bool> Permissions => _permissions ??= LoadPermissions();

    private Dictionary<string, bool> LoadPermissions()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PermissionsJson)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(PermissionsJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Checks whether the user is allowed to perform the given action.
    /// Managers always have full access. Users with no explicit permissions
    /// fall back to the default permissions of their role.
    /// </summary>
    public bool HasPermission(string key)
    {
        if (IsManager) return true;
        if (Permissions.Count == 0) return DefaultRolePermission(key);
        return Permissions.TryGetValue(key, out var allowed) && allowed;
    }

    private bool DefaultRolePermission(string key)
    {
        if (IsStoreManager)
        {
            return key is "invoices_view" or "invoices_search" or "invoices_print"
                or "purchases_view" or "purchases_add"
                or "expenses_view" or "expenses_add"
                or "workers_view" or "workers_add" or "workers_edit" or "workers_delete"
                or "workers_withdrawals"
                or "reports_view"
                or "products_view" or "products_add" or "products_edit" or "products_delete"
                or "categories_manage";
        }

        // Cashier defaults
        return key is "pos_access" or "pos_checkout" or "pos_discount" or "pos_cancel"
            or "invoices_view" or "invoices_search" or "invoices_print"
            or "returns_manage"
            or "expenses_view" or "expenses_add"
            or "shift_close";
    }
}
