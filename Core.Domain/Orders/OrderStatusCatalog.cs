namespace Core.Domain.Orders;

public static class OrderStatusCatalog
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Paid = "Paid";
    public const string Completed = "Completed";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";

    public static bool IsPaid(string? status)
        => string.Equals(status, Paid, StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);

    public static bool IsPending(string? status)
        => string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, Processing, StringComparison.OrdinalIgnoreCase);

    public static string ToArabicDisplay(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "غير معروف";
        }

        if (IsPaid(status))
        {
            return "مكتمل";
        }

        if (IsPending(status))
        {
            return "قيد المراجعة";
        }

        if (string.Equals(status, Shipped, StringComparison.OrdinalIgnoreCase))
        {
            return "قيد الشحن";
        }

        if (string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase))
        {
            return "ملغي";
        }

        return status;
    }
}
