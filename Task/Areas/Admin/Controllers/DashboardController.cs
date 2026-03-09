using Core.Application.Catalog.Contracts;
using Core.Application.Common.Activities;
using Core.Application.Common.Persistence;
using Core.Domain.Entities;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Admin.ViewModels;
using MemberEntity = Core.Domain.Entities.Member;

namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly IProductRepository _products;
        private readonly ICategoryRepository _categories;
        private readonly IRepository<MemberEntity> _members;
        private readonly IRepository<Order> _orders;
        private readonly IRepository<OrderItem> _orderItems;
        private readonly IRepository<Offer> _offers;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAdminActivityService _activity;

        public DashboardController(
            IProductRepository products,
            ICategoryRepository categories,
            IRepository<MemberEntity> members,
            IRepository<Order> orders,
            IRepository<OrderItem> orderItems,
            IRepository<Offer> offers,
            UserManager<ApplicationUser> userManager,
            IAdminActivityService activity)
        {
            _products = products;
            _categories = categories;
            _members = members;
            _orders = orders;
            _orderItems = orderItems;
            _offers = offers;
            _userManager = userManager;
            _activity = activity;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? range = null, DateTime? from = null, DateTime? to = null)
        {
            ViewData["Title"] = "لوحة التحكم";
            var vm = await BuildDashboardVmAsync(range, from, to);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Orders(string? range = null, DateTime? from = null, DateTime? to = null)
        {
            ViewData["Title"] = "إدارة الطلبات";
            var vm = await BuildDashboardVmAsync(range, from, to);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Reports(string? range = null, DateTime? from = null, DateTime? to = null)
        {
            ViewData["Title"] = "تقارير المبيعات";
            var vm = await BuildDashboardVmAsync(range, from, to);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Offers(string? range = null, DateTime? from = null, DateTime? to = null)
        {
            ViewData["Title"] = "العروض والكوبونات";
            var vm = await BuildDashboardVmAsync(range, from, to);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOffer(string title, string code, decimal discountPercent, DateTime? startsAtUtc, DateTime? endsAtUtc, bool isActive = true)
        {
            title = title?.Trim() ?? string.Empty;
            code = code?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(code))
            {
                TempData["DashboardError"] = "أدخل عنوان العرض وكود الخصم.";
                return RedirectToAction(nameof(Offers));
            }

            if (discountPercent <= 0 || discountPercent > 100)
            {
                TempData["DashboardError"] = "نسبة الخصم يجب أن تكون بين 0 و 100.";
                return RedirectToAction(nameof(Offers));
            }

            var existing = await _offers.FindAsync(x => x.Code == code);
            if (existing.Any())
            {
                TempData["DashboardError"] = "كود الخصم موجود بالفعل.";
                return RedirectToAction(nameof(Offers));
            }

            await _offers.AddAsync(new Offer
            {
                Title = title,
                Code = code.ToUpperInvariant(),
                DiscountPercent = Math.Round(discountPercent, 2),
                StartsAtUtc = startsAtUtc,
                EndsAtUtc = endsAtUtc,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _offers.SaveChangesAsync();

            TempData["DashboardAction"] = "تمت إضافة العرض بنجاح.";
            _activity.Add("العروض", $"تم إضافة عرض جديد ({code.ToUpperInvariant()}).");
            return RedirectToAction(nameof(Offers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleOffer(int id)
        {
            var offer = await _offers.GetByIdAsync(id);
            if (offer == null)
            {
                TempData["DashboardError"] = "العرض غير موجود.";
                return RedirectToAction(nameof(Offers));
            }

            offer.IsActive = !offer.IsActive;
            _offers.Update(offer);
            await _offers.SaveChangesAsync();

            TempData["DashboardAction"] = offer.IsActive ? "تم تفعيل العرض." : "تم إيقاف العرض.";
            _activity.Add("العروض", $"{(offer.IsActive ? "تفعيل" : "إيقاف")} العرض {offer.Code}.");
            return RedirectToAction(nameof(Offers));
        }

        private async Task<DashboardVm> BuildDashboardVmAsync(string? range, DateTime? from, DateTime? to)
        {
            var nowUtc = DateTime.UtcNow;
            var (fromUtc, toUtcExclusive, selectedRange, appliedFromDate, appliedToDate) = ResolvePeriod(range, from, to, nowUtc);
            var periodSpan = toUtcExclusive - fromUtc;
            var previousFromUtc = fromUtc - periodSpan;
            var previousToUtcExclusive = fromUtc;
            var todayFromUtc = nowUtc.Date;
            var todayToUtcExclusive = todayFromUtc.AddDays(1);
            var yesterdayFromUtc = todayFromUtc.AddDays(-1);

            var allProducts = await _products.GetAllWithCategoryAndImagesAsync();
            var allCategories = await _categories.GetAllAsync();
            var allMembers = await _members.GetAllAsync();
            var allOrders = await _orders.GetAllAsync();
            var allOrderItems = await _orderItems.GetAllAsync();
            var allOffers = await _offers.GetAllAsync();

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var adminEmails = admins
                .Select(x => (x.Email ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var membersOnly = allMembers
                .Where(x => !adminEmails.Contains((x.Email ?? string.Empty).Trim()))
                .ToList();

            var currentOrders = allOrders.Where(x => InRange(x.CreatedAtUtc, fromUtc, toUtcExclusive)).ToList();
            var previousOrders = allOrders.Where(x => InRange(x.CreatedAtUtc, previousFromUtc, previousToUtcExclusive)).ToList();
            var todayOrders = allOrders.Where(x => InRange(x.CreatedAtUtc, todayFromUtc, todayToUtcExclusive)).ToList();
            var yesterdayOrders = allOrders.Where(x => InRange(x.CreatedAtUtc, yesterdayFromUtc, todayFromUtc)).ToList();

            var currentPaidOrders = currentOrders.Where(IsPaidOrder).ToList();
            var previousPaidOrders = previousOrders.Where(IsPaidOrder).ToList();
            var todayPaidOrders = todayOrders.Where(IsPaidOrder).ToList();
            var yesterdayPaidOrders = yesterdayOrders.Where(IsPaidOrder).ToList();

            var salesTotal = currentPaidOrders.Sum(x => x.TotalAmount);
            var previousSalesTotal = previousPaidOrders.Sum(x => x.TotalAmount);
            var salesToday = todayPaidOrders.Sum(x => x.TotalAmount);
            var yesterdaySales = yesterdayPaidOrders.Sum(x => x.TotalAmount);

            var ordersCount = currentOrders.Count;
            var previousOrdersCount = previousOrders.Count;
            var ordersToday = todayOrders.Count;
            var yesterdayOrdersCount = yesterdayOrders.Count;

            var averageOrderValue = currentPaidOrders.Count > 0 ? Math.Round(salesTotal / currentPaidOrders.Count, 2) : 0m;
            var previousAverageOrderValue = previousPaidOrders.Count > 0 ? Math.Round(previousSalesTotal / previousPaidOrders.Count, 2) : 0m;

            var pendingOrdersCount = currentOrders.Count(x => IsPendingOrder(x.Status));
            var previousPendingOrdersCount = previousOrders.Count(x => IsPendingOrder(x.Status));
            var delayedOrdersCount = allOrders.Count(x => IsPendingOrder(x.Status) && x.CreatedAtUtc <= nowUtc.AddDays(-2));

            var activeMembersCount = membersOnly.Count(x => x.IsActive);
            var inactiveMembersCount = membersOnly.Count(x => !x.IsActive);

            var newMembersCutoff = nowUtc.AddDays(-30);
            var prevNewMembersCutoff = nowUtc.AddDays(-60);
            var newMembersCount30d = membersOnly.Count(x => x.CreatedAt >= newMembersCutoff);
            var previousNewMembers30d = membersOnly.Count(x => x.CreatedAt >= prevNewMembersCutoff && x.CreatedAt < newMembersCutoff);

            var lowStockItems = BuildLowStockItems(allProducts);
            var lowStockCount = lowStockItems.Count;
            var previousLowStockCount = lowStockCount;

            var complaintsCount = _activity.GetAll().Count(x =>
                ContainsAny(x.Message, "شكوى", "شكاوى", "مرتجع", "مرتجعات", "استرجاع"));

            var recentOrders = allOrders
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(10)
                .Select(x => new RecentOrderVm
                {
                    OrderNumber = x.OrderNumber,
                    CustomerName = string.IsNullOrWhiteSpace(x.CustomerName) ? x.CustomerEmail : x.CustomerName,
                    Total = x.TotalAmount,
                    Status = MapOrderStatus(x.Status),
                    CreatedAt = x.CreatedAtUtc,
                    ViewUrl = Url.Action(nameof(Orders), "Dashboard", new { area = "Admin" }) ?? "#"
                })
                .ToList();

            var recentMembers = membersOnly
                .OrderByDescending(x => x.CreatedAt)
                .Take(10)
                .Select(x => new RecentMemberVm
                {
                    FullName = string.IsNullOrWhiteSpace(x.FullName) ? "عضو" : x.FullName,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    ManageUrl = Url.Action("Members", "Users", new { area = "Admin" }) ?? "#"
                })
                .ToList();

            var offersVm = allOffers
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new OfferVm
                {
                    Id = x.Id,
                    Title = x.Title,
                    Code = x.Code,
                    DiscountPercent = x.DiscountPercent,
                    IsActive = x.IsActive,
                    StartsAtUtc = x.StartsAtUtc,
                    EndsAtUtc = x.EndsAtUtc
                })
                .ToList();

            var salesLast30Days = BuildSalesLast30Days(allOrders, nowUtc);
            var orderStatusDistribution = BuildOrderStatusDistribution(currentOrders);
            var topProducts = BuildTopProducts(currentPaidOrders, allOrderItems);

            return new DashboardVm
            {
                DesignsCount = allCategories.Count,
                ProductsCount = allProducts.Count,
                MembersCount = membersOnly.Count,
                ActiveMembersCount = activeMembersCount,
                AdminsCount = admins.Count,

                SelectedRange = selectedRange,
                FromDate = from?.Date,
                ToDate = to?.Date,
                AppliedFromDate = appliedFromDate,
                AppliedToDate = appliedToDate,

                SalesTotal = salesTotal,
                SalesToday = salesToday,
                OrdersCount = ordersCount,
                OrdersToday = ordersToday,
                AverageOrderValue = averageOrderValue,
                PendingOrdersCount = pendingOrdersCount,
                LowStockProductsCount = lowStockCount,
                NewMembersCount30d = newMembersCount30d,

                SalesTotalChangePercent = CalculateChangePercent(salesTotal, previousSalesTotal),
                SalesTodayChangePercent = CalculateChangePercent(salesToday, yesterdaySales),
                OrdersCountChangePercent = CalculateChangePercent(ordersCount, previousOrdersCount),
                OrdersTodayChangePercent = CalculateChangePercent(ordersToday, yesterdayOrdersCount),
                AverageOrderValueChangePercent = CalculateChangePercent(averageOrderValue, previousAverageOrderValue),
                PendingOrdersChangePercent = CalculateChangePercent(pendingOrdersCount, previousPendingOrdersCount),
                LowStockProductsChangePercent = CalculateChangePercent(lowStockCount, previousLowStockCount),
                NewMembersChangePercent = CalculateChangePercent(newMembersCount30d, previousNewMembers30d),

                DelayedOrdersCount = delayedOrdersCount,
                ComplaintsCount = complaintsCount,
                InactiveMembersCount = inactiveMembersCount,

                RecentOrders = recentOrders,
                RecentMembers = recentMembers,
                LowStockItems = lowStockItems,
                Offers = offersVm,
                SalesLast30Days = salesLast30Days,
                OrderStatusDistribution = orderStatusDistribution,
                TopProducts = topProducts
            };
        }

        private static (DateTime FromUtc, DateTime ToUtcExclusive, string SelectedRange, DateTime AppliedFromDate, DateTime AppliedToDate) ResolvePeriod(
            string? range,
            DateTime? from,
            DateTime? to,
            DateTime nowUtc)
        {
            var normalizedRange = string.IsNullOrWhiteSpace(range) ? "30d" : range.Trim().ToLowerInvariant();

            if (from.HasValue || to.HasValue)
            {
                var fromDate = (from ?? to ?? nowUtc).Date;
                var toDate = (to ?? from ?? nowUtc).Date;
                if (fromDate > toDate)
                {
                    (fromDate, toDate) = (toDate, fromDate);
                }

                var customFromUtc = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
                var customToUtcExclusive = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Utc);
                return (customFromUtc, customToUtcExclusive, normalizedRange, fromDate, toDate);
            }

            DateTime startUtc;
            DateTime endUtcExclusive;
            switch (normalizedRange)
            {
                case "today":
                    startUtc = nowUtc.Date;
                    endUtcExclusive = startUtc.AddDays(1);
                    break;
                case "week":
                    startUtc = nowUtc.Date.AddDays(-6);
                    endUtcExclusive = nowUtc.Date.AddDays(1);
                    break;
                case "month":
                    startUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    endUtcExclusive = startUtc.AddMonths(1);
                    break;
                default:
                    normalizedRange = "30d";
                    startUtc = nowUtc.Date.AddDays(-29);
                    endUtcExclusive = nowUtc.Date.AddDays(1);
                    break;
            }

            return (startUtc, endUtcExclusive, normalizedRange, startUtc.Date, endUtcExclusive.AddDays(-1).Date);
        }

        private static bool InRange(DateTime value, DateTime fromUtc, DateTime toUtcExclusive)
            => value >= fromUtc && value < toUtcExclusive;

        private static bool IsPaidOrder(Order order)
            => string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase)
               || string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase);

        private static bool IsPendingOrder(string? status)
            => string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "AwaitingPayment", StringComparison.OrdinalIgnoreCase);

        private static string MapOrderStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "معلّق";
            }

            if (string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return "مكتمل";
            }

            if (string.Equals(status, "Shipped", StringComparison.OrdinalIgnoreCase))
            {
                return "قيد الشحن";
            }

            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return "ملغي";
            }

            return "معلّق";
        }

        private static bool ContainsAny(string? source, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (var needle in needles)
            {
                if (!string.IsNullOrWhiteSpace(needle) &&
                    source.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static decimal CalculateChangePercent(decimal current, decimal previous)
        {
            if (previous == 0m)
            {
                return current > 0m ? 100m : 0m;
            }

            return Math.Round(((current - previous) / Math.Abs(previous)) * 100m, 1);
        }

        private static decimal CalculateChangePercent(int current, int previous)
            => CalculateChangePercent((decimal)current, previous);

        private static List<DailySalesPointVm> BuildSalesLast30Days(List<Order> orders, DateTime nowUtc)
        {
            var startDate = nowUtc.Date.AddDays(-29);
            var endExclusive = nowUtc.Date.AddDays(1);

            var dailyLookup = orders
                .Where(x => IsPaidOrder(x) && InRange(x.CreatedAtUtc, startDate, endExclusive))
                .GroupBy(x => x.CreatedAtUtc.Date)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.TotalAmount));

            var result = new List<DailySalesPointVm>(30);
            for (var i = 0; i < 30; i++)
            {
                var day = startDate.AddDays(i);
                dailyLookup.TryGetValue(day, out var total);
                result.Add(new DailySalesPointVm
                {
                    Date = day,
                    Total = Math.Round(total, 2)
                });
            }

            return result;
        }

        private static List<OrderStatusDistributionVm> BuildOrderStatusDistribution(List<Order> orders)
        {
            var total = orders.Count;
            var counts = orders
                .GroupBy(x => MapOrderStatus(x.Status))
                .ToDictionary(x => x.Key, x => x.Count());

            int ReadCount(string status) => counts.TryGetValue(status, out var value) ? value : 0;
            decimal CalcPercent(int count) => total <= 0 ? 0m : Math.Round((count * 100m) / total, 1);

            return new List<OrderStatusDistributionVm>
            {
                new()
                {
                    Status = "مكتمل",
                    Count = ReadCount("مكتمل"),
                    Percent = CalcPercent(ReadCount("مكتمل")),
                    ColorHex = "#22c55e"
                },
                new()
                {
                    Status = "معلّق",
                    Count = ReadCount("معلّق"),
                    Percent = CalcPercent(ReadCount("معلّق")),
                    ColorHex = "#f59e0b"
                },
                new()
                {
                    Status = "قيد الشحن",
                    Count = ReadCount("قيد الشحن"),
                    Percent = CalcPercent(ReadCount("قيد الشحن")),
                    ColorHex = "#0284c7"
                },
                new()
                {
                    Status = "ملغي",
                    Count = ReadCount("ملغي"),
                    Percent = CalcPercent(ReadCount("ملغي")),
                    ColorHex = "#f43f5e"
                }
            };
        }

        private static List<TopProductSalesVm> BuildTopProducts(List<Order> paidOrders, List<OrderItem> orderItems)
        {
            if (paidOrders.Count == 0 || orderItems.Count == 0)
            {
                return new List<TopProductSalesVm>();
            }

            var paidOrderIds = paidOrders.Select(x => x.Id).ToHashSet();
            var grouped = orderItems
                .Where(x => paidOrderIds.Contains(x.OrderId))
                .GroupBy(x => string.IsNullOrWhiteSpace(x.ProductName) ? $"منتج #{x.ProductId}" : x.ProductName.Trim())
                .Select(x => new
                {
                    ProductName = x.Key,
                    Quantity = x.Sum(y => y.Quantity),
                    Sales = x.Sum(y => y.UnitPrice * y.Quantity)
                })
                .OrderByDescending(x => x.Quantity)
                .ThenByDescending(x => x.Sales)
                .Take(6)
                .ToList();

            var totalQuantity = grouped.Sum(x => x.Quantity);

            return grouped
                .Select(x => new TopProductSalesVm
                {
                    ProductName = x.ProductName,
                    QuantitySold = x.Quantity,
                    SalesAmount = Math.Round(x.Sales, 2),
                    Percent = totalQuantity <= 0 ? 0m : Math.Round((x.Quantity * 100m) / totalQuantity, 1)
                })
                .ToList();
        }

        private List<LowStockVm> BuildLowStockItems(List<Product> products)
        {
            return products
                .Where(x => x.StockQuantity <= 5)
                .OrderBy(x => x.StockQuantity)
                .ThenBy(x => x.Name)
                .Take(10)
                .Select(x => new LowStockVm
                {
                    ProductName = x.Name,
                    Quantity = x.StockQuantity,
                    IsOutOfStock = x.StockQuantity <= 0,
                    RefillUrl = Url.Action("Edit", "Products", new { area = "Admin", id = x.Id }) ?? "#"
                })
                .ToList();
        }
    }
}
