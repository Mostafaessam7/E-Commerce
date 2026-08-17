namespace Security;

/// <summary>
/// Every permission string the system knows about, grouped by module. This is the single source
/// of truth both sides of authorization use: Identity (Phase 3) seeds these into
/// <c>AspNetRoleClaims</c>/permission-role mappings, and Web authorization policies
/// (<c>[Authorize(Policy = Permissions.Catalog.Edit)]</c>) reference the same constants — so a
/// typo can't silently create a permission nobody was ever granted.
///
/// Deliberately flat strings ("Catalog.Edit"), not an enum: enums are awkward to store as claim
/// values and awkward to extend per-module without touching a shared assembly's compiled enum;
/// plain strings let each module's constants live conceptually with that module while still
/// being centralized here for now (Phase 1 has no per-module Contracts assemblies wired yet to
/// host them separately, and duplicating this list per module would be worse than one shared file).
/// </summary>
public static class Permissions
{
    public static class Catalog
    {
        public const string View = "Catalog.View";
        public const string Create = "Catalog.Create";
        public const string Edit = "Catalog.Edit";
        public const string Delete = "Catalog.Delete";
    }

    public static class Inventory
    {
        public const string View = "Inventory.View";
        public const string Adjust = "Inventory.Adjust";
    }

    public static class Orders
    {
        public const string View = "Orders.View";
        public const string Edit = "Orders.Edit";
        public const string Cancel = "Orders.Cancel";
    }

    public static class Payments
    {
        public const string View = "Payments.View";
        public const string Refund = "Payments.Refund";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string Edit = "Customers.Edit";
    }

    public static class Reports
    {
        public const string View = "Reports.View";
    }

    public static class Promotions
    {
        public const string View = "Promotions.View";
        public const string Manage = "Promotions.Manage";
    }

    public static class Users
    {
        public const string View = "Users.View";
        public const string Manage = "Users.Manage";
    }

    public static class Roles
    {
        public const string Manage = "Roles.Manage";
    }

    public static class PermissionsAdmin
    {
        public const string Manage = "Permissions.Manage";
    }

    /// <summary>All permission constants declared above, for seeding + admin UI listing.</summary>
    public static IReadOnlyCollection<string> All { get; } =
    [
        Catalog.View, Catalog.Create, Catalog.Edit, Catalog.Delete,
        Inventory.View, Inventory.Adjust,
        Orders.View, Orders.Edit, Orders.Cancel,
        Payments.View, Payments.Refund,
        Customers.View, Customers.Edit,
        Reports.View,
        Promotions.View, Promotions.Manage,
        Users.View, Users.Manage,
        Roles.Manage,
        PermissionsAdmin.Manage,
    ];
}
