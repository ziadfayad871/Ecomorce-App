using System.ComponentModel.DataAnnotations;

namespace Core.Application.Members.Models;

public class MemberPanelVm
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    public int OrdersCount { get; set; }
    public int PendingOrdersCount { get; set; }
    public int PaidOrdersCount { get; set; }
    public int FavoritesCount { get; set; }
    public int CartItemsCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderDateUtc { get; set; }

    public List<MemberOrderListItemVm> RecentOrders { get; set; } = new();
    public List<MemberFavoriteCardVm> FavoriteProducts { get; set; } = new();
    public List<MemberFavoriteCardVm> SuggestedProducts { get; set; } = new();
}

public class MemberOrderListItemVm
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusDisplay { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public int ItemsCount { get; set; }
}

public class MemberOrderDetailsVm
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusDisplay { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public List<MemberOrderItemVm> Items { get; set; } = new();
}

public class MemberOrderItemVm
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class MemberFavoriteCardVm
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsTracked { get; set; }
}

public class MemberProfileVm
{
    [Required(ErrorMessage = "الاسم مطلوب")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين 3 و120 حرفًا")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [StringLength(150)]
    public string Email { get; set; } = "";

    [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
    public string? ConfirmNewPassword { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record ToggleFavoriteResult(bool Success, bool IsTracked, string Message, int FavoritesCount);

public record UpdateMemberProfileResult(bool Success, string Message, int? MemberId, string? MemberEmail);
