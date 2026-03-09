using Core.Application.Members.Contracts;
using Core.Application.Members.Models;
using Core.Domain.Common;
using Core.Domain.Orders;
using DataAccess.Data;
using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Services;

public class MemberPanelService : IMemberPanelService
{
    private readonly ApplicationDbContext _db;

    public MemberPanelService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MemberPanelVm?> BuildDashboardAsync(int memberId, int cartItemsCount)
    {
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == memberId);

        if (member == null)
        {
            return null;
        }

        var orders = await BuildOrdersQuery(memberId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var orderIds = orders.Select(x => x.Id).ToList();
        var itemCounts = await _db.OrderItems
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .GroupBy(x => x.OrderId)
            .Select(x => new { OrderId = x.Key, Count = x.Sum(v => v.Quantity) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count);

        var favoriteProducts = await GetFavoriteProductsQuery(memberId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(4)
            .ToListAsync();

        var favoriteIds = favoriteProducts
            .Select(x => x.ProductId)
            .ToHashSet();

        var suggestedProducts = await _db.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Images)
            .Where(x => !favoriteIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Take(4)
            .ToListAsync();

        var paidOrders = orders.Where(x => OrderStatusCatalog.IsPaid(x.Status)).ToList();

        return new MemberPanelVm
        {
            FullName = member.FullName,
            Email = member.Email,
            CreatedAt = member.CreatedAt,
            IsActive = member.IsActive,
            OrdersCount = orders.Count,
            PendingOrdersCount = orders.Count(x => OrderStatusCatalog.IsPending(x.Status)),
            PaidOrdersCount = paidOrders.Count,
            FavoritesCount = await _db.MemberFavorites.CountAsync(x => x.MemberId == memberId),
            CartItemsCount = cartItemsCount,
            TotalSpent = paidOrders.Sum(x => x.TotalAmount),
            LastOrderDateUtc = orders.OrderByDescending(x => x.CreatedAtUtc).Select(x => (DateTime?)x.CreatedAtUtc).FirstOrDefault(),
            RecentOrders = orders
                .Take(5)
                .Select(x => new MemberOrderListItemVm
                {
                    Id = x.Id,
                    OrderNumber = x.OrderNumber,
                    Status = x.Status,
                    StatusDisplay = OrderStatusCatalog.ToArabicDisplay(x.Status),
                    TotalAmount = x.TotalAmount,
                    CreatedAtUtc = x.CreatedAtUtc,
                    PaidAtUtc = x.PaidAtUtc,
                    ItemsCount = itemCounts.TryGetValue(x.Id, out var count) ? count : 0
                })
                .ToList(),
            FavoriteProducts = favoriteProducts.Select(MapFavoriteCard).ToList(),
            SuggestedProducts = suggestedProducts.Select(x => new MemberFavoriteCardVm
            {
                ProductId = x.Id,
                Name = x.Name,
                CategoryName = x.Category?.Name ?? StoreBranding.BrandName,
                ImagePath = x.Images.FirstOrDefault()?.Path ?? string.Empty,
                Price = x.Price,
                StockQuantity = x.StockQuantity,
                IsTracked = false
            }).ToList()
        };
    }

    public async Task<List<MemberOrderListItemVm>> GetOrdersAsync(int memberId)
    {
        var orders = await BuildOrdersQuery(memberId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var orderIds = orders.Select(x => x.Id).ToList();
        var itemCounts = await _db.OrderItems
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .GroupBy(x => x.OrderId)
            .Select(x => new { OrderId = x.Key, Count = x.Sum(v => v.Quantity) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count);

        return orders.Select(x => new MemberOrderListItemVm
        {
            Id = x.Id,
            OrderNumber = x.OrderNumber,
            Status = x.Status,
            StatusDisplay = OrderStatusCatalog.ToArabicDisplay(x.Status),
            TotalAmount = x.TotalAmount,
            CreatedAtUtc = x.CreatedAtUtc,
            PaidAtUtc = x.PaidAtUtc,
            ItemsCount = itemCounts.TryGetValue(x.Id, out var count) ? count : 0
        }).ToList();
    }

    public async Task<MemberOrderDetailsVm?> GetOrderDetailsAsync(int memberId, int orderId)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId && x.MemberId == memberId);

        if (order == null)
        {
            return null;
        }

        var items = await _db.OrderItems
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return new MemberOrderDetailsVm
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Status = order.Status,
            StatusDisplay = OrderStatusCatalog.ToArabicDisplay(order.Status),
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAtUtc,
            PaidAtUtc = order.PaidAtUtc,
            Items = items.Select(x => new MemberOrderItemVm
            {
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity
            }).ToList()
        };
    }

    public async Task<List<MemberFavoriteCardVm>> GetFavoritesAsync(int memberId)
    {
        var favorites = await GetFavoriteProductsQuery(memberId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return favorites.Select(MapFavoriteCard).ToList();
    }

    public async Task<HashSet<int>> GetFavoriteProductIdsAsync(int memberId)
    {
        return (await _db.MemberFavorites
                .AsNoTracking()
                .Where(x => x.MemberId == memberId)
                .Select(x => x.ProductId)
                .ToListAsync())
            .ToHashSet();
    }

    public async Task<ToggleFavoriteResult> ToggleFavoriteAsync(int memberId, int productId)
    {
        var productExists = await _db.Products.AnyAsync(x => x.Id == productId);
        if (!productExists)
        {
            return new ToggleFavoriteResult(false, false, "المنتج غير موجود.", 0);
        }

        var existing = await _db.MemberFavorites
            .FirstOrDefaultAsync(x => x.MemberId == memberId && x.ProductId == productId);

        bool isTracked;
        string message;

        if (existing == null)
        {
            await _db.MemberFavorites.AddAsync(new MemberFavorite
            {
                MemberId = memberId,
                ProductId = productId,
                CreatedAtUtc = DateTime.UtcNow
            });
            isTracked = true;
            message = "تمت إضافة المنتج إلى المتابعة.";
        }
        else
        {
            _db.MemberFavorites.Remove(existing);
            isTracked = false;
            message = "تمت إزالة المنتج من المتابعة.";
        }

        await _db.SaveChangesAsync();
        var favoritesCount = await _db.MemberFavorites.CountAsync(x => x.MemberId == memberId);
        return new ToggleFavoriteResult(true, isTracked, message, favoritesCount);
    }

    public async Task<MemberProfileVm?> GetProfileAsync(int memberId)
    {
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == memberId);

        if (member == null)
        {
            return null;
        }

        return new MemberProfileVm
        {
            FullName = member.FullName,
            Email = member.Email,
            IsActive = member.IsActive,
            CreatedAt = member.CreatedAt
        };
    }

    public async Task<UpdateMemberProfileResult> UpdateProfileAsync(int memberId, string fullName, string email)
    {
        fullName = (fullName ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim().ToLowerInvariant();

        if (fullName.Length < 3)
        {
            return new UpdateMemberProfileResult(false, "الاسم يجب أن يكون 3 أحرف على الأقل.", null, null);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return new UpdateMemberProfileResult(false, "البريد الإلكتروني مطلوب.", null, null);
        }

        var member = await _db.Members.FirstOrDefaultAsync(x => x.Id == memberId);
        if (member == null)
        {
            return new UpdateMemberProfileResult(false, "الحساب غير موجود.", null, null);
        }

        var emailUsedByMember = await _db.Members.AnyAsync(x => x.Id != memberId && x.Email == email);
        if (emailUsedByMember)
        {
            return new UpdateMemberProfileResult(false, "هذا البريد مستخدم بالفعل.", null, null);
        }

        var emailUsedByAdmin = await _db.Users.AnyAsync(x => x.Email != null && x.Email.ToLower() == email);
        if (emailUsedByAdmin)
        {
            return new UpdateMemberProfileResult(false, "هذا البريد محجوز لحساب آخر.", null, null);
        }

        member.FullName = fullName;
        member.Email = email;
        await _db.SaveChangesAsync();

        return new UpdateMemberProfileResult(true, "تم تحديث بيانات الحساب بنجاح.", member.Id, member.Email);
    }

    private IQueryable<Order> BuildOrdersQuery(int memberId)
    {
        return _db.Orders
            .AsNoTracking()
            .Where(x => x.MemberId == memberId);
    }

    private IQueryable<MemberFavorite> GetFavoriteProductsQuery(int memberId)
    {
        return _db.MemberFavorites
            .AsNoTracking()
            .Include(x => x.Product)!
                .ThenInclude(x => x!.Category)
            .Include(x => x.Product)!
                .ThenInclude(x => x!.Images)
            .Where(x => x.MemberId == memberId && x.Product != null);
    }

    private static MemberFavoriteCardVm MapFavoriteCard(MemberFavorite favorite)
    {
        var product = favorite.Product!;
        return new MemberFavoriteCardVm
        {
            ProductId = product.Id,
            Name = product.Name,
            CategoryName = product.Category?.Name ?? StoreBranding.BrandName,
            ImagePath = product.Images.FirstOrDefault()?.Path ?? string.Empty,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsTracked = true
        };
    }
}
