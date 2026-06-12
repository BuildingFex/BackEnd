namespace BuildingFex.Api.Finances.Application.Internal.MercadoPago;

/// <summary>
/// Binds the "MercadoPago" configuration section.
/// </summary>
public class MercadoPagoSettings
{
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}
