using System;
using System.Collections.Generic;

namespace Task.Areas.Admin.ViewModels
{
    public class DashboardVm
    {
        public int DesignsCount { get; set; }
        public int ProductsCount { get; set; }
        public int MembersCount { get; set; }
        public int ActiveMembersCount { get; set; }
        public int AdminsCount { get; set; }

        public string SelectedRange { get; set; } = "30d";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime AppliedFromDate { get; set; }
        public DateTime AppliedToDate { get; set; }

        public decimal SalesTotal { get; set; }
        public decimal SalesToday { get; set; }
        public int OrdersCount { get; set; }
        public int OrdersToday { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int PendingOrdersCount { get; set; }
        public int LowStockProductsCount { get; set; }
        public int NewMembersCount30d { get; set; }

        public decimal SalesTotalChangePercent { get; set; }
        public decimal SalesTodayChangePercent { get; set; }
        public decimal OrdersCountChangePercent { get; set; }
        public decimal OrdersTodayChangePercent { get; set; }
        public decimal AverageOrderValueChangePercent { get; set; }
        public decimal PendingOrdersChangePercent { get; set; }
        public decimal LowStockProductsChangePercent { get; set; }
        public decimal NewMembersChangePercent { get; set; }

        public int DelayedOrdersCount { get; set; }
        public int ComplaintsCount { get; set; }
        public int InactiveMembersCount { get; set; }

        public List<RecentOrderVm> RecentOrders { get; set; } = new();
        public List<RecentMemberVm> RecentMembers { get; set; } = new();
        public List<LowStockVm> LowStockItems { get; set; } = new();
        public List<OfferVm> Offers { get; set; } = new();
        public List<DailySalesPointVm> SalesLast30Days { get; set; } = new();
        public List<OrderStatusDistributionVm> OrderStatusDistribution { get; set; } = new();
        public List<TopProductSalesVm> TopProducts { get; set; } = new();
    }

    public class RecentOrderVm
    {
        public string OrderNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal Total { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string ViewUrl { get; set; } = "";
    }

    public class RecentMemberVm
    {
        public string FullName { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ManageUrl { get; set; } = "";
    }

    public class LowStockVm
    {
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public bool IsOutOfStock { get; set; }
        public string RefillUrl { get; set; } = "";
    }

    public class OfferVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Code { get; set; } = "";
        public decimal DiscountPercent { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }
    }

    public class DailySalesPointVm
    {
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
    }

    public class OrderStatusDistributionVm
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
        public decimal Percent { get; set; }
        public string ColorHex { get; set; } = "#cbd5e1";
    }

    public class TopProductSalesVm
    {
        public string ProductName { get; set; } = "";
        public int QuantitySold { get; set; }
        public decimal SalesAmount { get; set; }
        public decimal Percent { get; set; }
    }
}
