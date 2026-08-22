namespace Store.Web;

/// <summary>
/// Empty marker type — carries no members, exists only so `IStringLocalizer&lt;SharedResource&gt;`
/// has something to bind resource lookups to. Deliberately placed at the project root, not inside
/// `Resources/` (see Program.cs's localization setup comment for why: a marker type physically
/// inside its own `ResourcesPath` folder causes a doubled "Resources.Resources..." resource name).
/// One shared resource set for the whole app rather than one `.resx` per view — with ~58 views
/// sharing a lot of the same strings ("Add to cart", "Sign In", validation labels), a single
/// lookup table is far more maintainable than duplicating those strings per file.
/// </summary>
public sealed class SharedResource;
