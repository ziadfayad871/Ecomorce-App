using Core.Application.Members.Models;

namespace Core.Application.Members.Contracts;

public interface IMemberPanelService
{
    Task<MemberPanelVm?> BuildDashboardAsync(int memberId, int cartItemsCount);
    Task<List<MemberOrderListItemVm>> GetOrdersAsync(int memberId);
    Task<MemberOrderDetailsVm?> GetOrderDetailsAsync(int memberId, int orderId);
    Task<List<MemberFavoriteCardVm>> GetFavoritesAsync(int memberId);
    Task<HashSet<int>> GetFavoriteProductIdsAsync(int memberId);
    Task<ToggleFavoriteResult> ToggleFavoriteAsync(int memberId, int productId);
    Task<MemberProfileVm?> GetProfileAsync(int memberId);
    Task<UpdateMemberProfileResult> UpdateProfileAsync(int memberId, string fullName, string email);
}
