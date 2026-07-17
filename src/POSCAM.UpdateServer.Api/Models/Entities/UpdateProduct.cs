using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Entities;

/// <summary>
/// update_products 테이블과 대응하는 제품 Entity.
/// </summary>
public sealed class UpdateProduct
{
    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? ProductDescription { get; set; }

    public ProductStatus ProductStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
