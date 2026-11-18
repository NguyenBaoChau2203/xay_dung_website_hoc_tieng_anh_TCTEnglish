namespace TCTEnglish.Services.Emails;

/// <summary>
/// Canonical design tokens mapped to email-safe literal CSS values.
/// Email clients (Gmail, Outlook, Yahoo, Apple Mail) strip CSS variables and external stylesheets,
/// so all values from DESIGN.md and TCTEnglish/wwwroot/css/tct-learning-ui.css are resolved into
/// verified inline literals here as a single source of truth for email presentation.
/// </summary>
public static class TctEmailTokens
{
    // ---------- Color tokens (DESIGN.md / tct-learning-ui.css) ----------
    // Canvas & Surface
    public const string ClayCanvas = "#f0f3ff";        // --tct-clay-canvas / --tct-primary-tint
    public const string ClaySurface = "#ffffff";       // --tct-clay-surface
    public const string SurfaceMuted = "#f8fafc";      // --tct-surface-muted

    // Brand Primary & Accents
    public const string Primary = "#4255ff";           // --tct-primary
    public const string PrimaryHover = "#2f3fe0";      // --tct-primary-hover
    public const string PrimaryStrong = "#2136e7";     // --tct-primary-strong
    public const string PrimarySoft = "#eef2ff";       // --tct-primary-soft
    public const string AccentYellow = "#ffcd1f";      // --tct-accent (TCT wordmark dot & highlights)
    public const string OnAccent = "#111c2d";          // --tct-on-accent

    // Typography & Contrast (passes WCAG AA on surface/canvas)
    public const string Text = "#111c2d";              // --tct-text (17.10:1 on white)
    public const string TextSecondary = "#444656";     // --tct-text-secondary (9.31:1 on white)
    public const string TextMuted = "#5b6578";         // --tct-text-muted (5.87:1 on white)

    // Borders & Outlines
    public const string Border = "#e5e7eb";            // --tct-border (neutral divider)
    public const string BorderControl = "#7c869b";     // --tct-border-control (accessible boundary)
    public const string CardBorder = "#dbe4ff";        // Clay outer fallback border for clients stripping shadow

    // Semantic Accents (Fill / Text / Soft Background / Border)
    public const string Success = "#10b981";           // --tct-success (icon/badge)
    public const string SuccessText = "#047857";       // --tct-success-text (readable copy)
    public const string SuccessSoft = "#ecfdf5";       // --tct-success-soft
    public const string SuccessBorder = "#a7f3d0";

    public const string Warning = "#f59e0b";           // --tct-warning
    public const string WarningText = "#92400e";       // --tct-warning-text (readable copy)
    public const string WarningSoft = "#fffbeb";       // --tct-warning-soft
    public const string WarningBorder = "#fde68a";

    public const string Danger = "#ba1a1a";            // --tct-danger
    public const string DangerText = "#ba1a1a";        // --tct-danger-text
    public const string DangerSoft = "#fef2f2";        // --tct-danger-soft
    public const string DangerBorder = "#fecaca";

    public const string Info = "#06b6d4";              // --tct-info
    public const string InfoText = "#0e7490";          // --tct-info-text
    public const string InfoSoft = "#ecfeff";          // --tct-info-soft
    public const string InfoBorder = "#a5f3fc";

    // ---------- Typography (Inter + fallback stack) ----------
    public const string FontFamily = "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";
    public const string MonoFontFamily = "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace";

    // ---------- Shape & Depth (Claymorphism for supporting clients) ----------
    public const string CardRadius = "20px";           // --tct-clay-radius-panel / --tct-radius-xl
    public const string ButtonRadius = "12px";         // --tct-radius-md
    public const string BadgeRadius = "9999px";        // --tct-radius-pill
    public const string BoxRadius = "12px";            // --tct-radius-md

    public const string CardShadow = "0 10px 25px rgba(66, 85, 255, 0.08), 0 4px 10px rgba(0, 0, 0, 0.03)";
    public const string ButtonShadow = "0 4px 12px rgba(66, 85, 255, 0.35)";
    public const string InsetBoxShadow = "inset 2px 2px 5px rgba(66, 85, 255, 0.06)";
}
