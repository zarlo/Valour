using IdGen;
using StackExchange.Redis;
using Valour.Server.Database;
using Valour.Server.Requests;
using Valour.Shared;
using Valour.Shared.Authorization;

namespace Valour.Server.Services;

public class PlanetCategoryService
{
    private readonly ValourDB _db;
    private readonly PlanetMemberService _planetMemberService;
    private readonly PlanetChannelService _planetChannelService;
    private readonly PlanetRoleService _planetRoleService;
    private readonly CoreHubService _coreHub;
    private readonly ILogger<PlanetCategoryService> _logger;

    public PlanetCategoryService(
        ValourDB db, 
        PlanetMemberService planetMemberService,
        CoreHubService coreHub,
        PlanetChannelService planetChannelService,
        PlanetRoleService planetRoleService,
        ILogger<PlanetCategoryService> logger)
    {
        _db = db;
        _planetMemberService = planetMemberService;
        _coreHub = coreHub;
        _logger = logger;
        _planetChannelService = planetChannelService;
        _planetRoleService = planetRoleService;
    }

    /// <summary>
    /// Returns the category with the given id
    /// </summary>
    public async ValueTask<PlanetCategory> GetAsync(long id) =>
        (await _db.PlanetCategories.FindAsync(id)).ToModel();

    /// <summary>
    /// Creates the given planet category
    /// </summary>
    public async Task<TaskResult<PlanetCategory>> CreateAsync(PlanetCategory category)
    {
        var baseValid = await ValidateBasic(category);
        if (!baseValid.Success)
            return new(false, baseValid.Message);

        await using var tran = await _db.Database.BeginTransactionAsync();

        try
        {
            await _db.PlanetCategories.AddAsync(category.ToDatabase());
            await _db.SaveChangesAsync();

            await tran.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create planet category");
            await tran.RollbackAsync();
            return new(false, "Failed to create category");
        }

        _coreHub.NotifyPlanetItemChange(category);

        return new(true, "PlanetCategory created successfully", category);
    }

    /// <summary>
    /// Creates the given planet category (detailed)
    /// </summary>
    public async Task<TaskResult<PlanetCategory>> CreateDetailedAsync(CreatePlanetCategoryChannelRequest request, PlanetMember member)
    {
        var category = request.Category;
        var baseValid = await ValidateBasic(category);
        if (!baseValid.Success)
            return new(false, baseValid.Message);

        List<PermissionsNode> nodes = new();

        // Create nodes
        foreach (var nodeReq in request.Nodes)
        {
            var node = nodeReq;
            node.TargetId = category.Id;
            node.PlanetId = category.PlanetId;

            var role = await _planetRoleService.GetAsync(node.RoleId);
            if (role.GetAuthority() > await _planetMemberService.GetAuthorityAsync(member))
                return new(false, "A permission node's role has higher authority than you.");

            node.Id = IdManager.Generate();

            nodes.Add(node);
        }

        var tran = await _db.Database.BeginTransactionAsync();

        try
        {
            await _db.PlanetCategories.AddAsync(category.ToDatabase());
            await _db.SaveChangesAsync();

            await _db.PermissionsNodes.AddRangeAsync(nodes.Select(x => x.ToDatabase()));
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await tran.RollbackAsync();
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }

        await tran.CommitAsync();

        _coreHub.NotifyPlanetItemChange(category);

        return new(true, "Success");
    }

    public async Task<TaskResult<PlanetCategory>> PutAsync(PlanetCategory updatedcategory)
    {
        var old = await _db.PlanetCategories.FindAsync(updatedcategory.Id);
        if (old is null) return new(false, $"PlanetCategory not found");

        // Validation
        if (old.Id != updatedcategory.Id)
            return new(false, "Cannot change Id.");

        if (old.PlanetId != updatedcategory.PlanetId)
            return new(false, "Cannot change PlanetId.");

        var baseValid = await ValidateBasic(updatedcategory);
        if (!baseValid.Success)
            return new(false, baseValid.Message);

        // Update
        try
        {
            _db.Entry(old).CurrentValues.SetValues(updatedcategory);
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }

        _coreHub.NotifyPlanetItemChange(updatedcategory);

        return new(true, "Success", updatedcategory);
    }

    /// <summary>
    /// Returns the children of the category with the given id
    /// </summary>
    public async Task<List<PlanetChannel>> GetChildrenAsync(long id) =>
        await _db.PlanetChannels.Where(x => x.ParentId == id)
            .Select(x => x.ToModel()).ToListAsync();
    
    /// <summary>
    /// Returns the number of children in the category with the given id
    /// </summary>
    public async Task<int> GetChildCountAsync(long id) =>
        await _db.PlanetChannels.Where(x => x.ParentId == id)
            .CountAsync();

    /// <summary>
    /// Returns if a category is the last category in a planet
    /// </summary>
    public async Task<bool> IsLastCategory(PlanetCategory category) =>
        (await _db.PlanetCategories.Where(x => x.PlanetId == category.PlanetId).CountAsync()) < 2;

    /// <summary>
    /// Returns the ids of the children of the category with the given id
    /// </summary>
    public async Task<List<long>> GetChildrenIdsAsync(long id) =>
        await _db.PlanetChannels.Where(x => x.ParentId == id).Select(x => x.Id).ToListAsync();

    /// <summary>
    /// Returns the children of the category with the given id
    /// </summary>
    public async Task<List<PlanetChannel>> GetChildrenAsync(PlanetCategory category) =>
        await GetChildrenAsync(category.Id);

    /// <summary>
    /// Returns if a given member has a channel permission
    /// </summary>
    public async Task<bool> HasPermissionAsync(PlanetCategory channel, PlanetMember member, CategoryPermission permission) =>
        await _planetMemberService.HasPermissionAsync(member, channel, permission);
    
    /// <summary>
    /// Returns if a given member has a channel permission
    /// </summary>
    public async Task<bool> HasPermissionAsync(PlanetCategory channel, PlanetMember member, ChatChannelPermission permission) =>
        await _planetMemberService.HasPermissionAsync(member, channel, permission);

    /// <summary>
    /// Deletes the given category
    /// </summary>
    public async Task DeleteAsync(PlanetCategory category)
    {
        var dbcategory = await _db.PlanetCategories.FindAsync(category.Id);
        dbcategory.IsDeleted = true;
        _db.PlanetCategories.Update(dbcategory);
        await _db.SaveChangesAsync();

        _coreHub.NotifyPlanetItemDelete(category);
    }
    
    public static readonly Regex nameRegex = new Regex(@"^[a-zA-Z0-9 _-]+$");

    /// <summary>
    /// Common basic validation for categories
    /// </summary>
    private async Task<TaskResult> ValidateBasic(PlanetCategory category)
    {
        var nameValid = ValidateName(category.Name);
        if (!nameValid.Success)
            return new TaskResult(false, nameValid.Message);

        var descValid = ValidateDescription(category.Description);
        if (!descValid.Success)
            return new TaskResult(false, nameValid.Message);

        var positionValid = await ValidateParentAndPosition(category);
        if (!positionValid.Success)
            return new TaskResult(false, nameValid.Message);

        return TaskResult.SuccessResult;
    }

    /// <summary>
    /// Validates that a given name is allowable
    /// </summary>
    public static TaskResult ValidateName(string name)
    {
        if (name.Length > 32)
        {
            return new TaskResult(false, "Planet names must be 32 characters or less.");
        }

        if (!nameRegex.IsMatch(name))
        {
            return new TaskResult(false, "Planet names may only include letters, numbers, dashes, and underscores.");
        }

        return TaskResult.SuccessResult;
    }

    /// <summary>
    /// Validates that a given description is allowable
    /// </summary>
    public static TaskResult ValidateDescription(string desc)
    {
        if (desc.Length > 500)
        {
            return new TaskResult(false, "Planet descriptions must be 500 characters or less.");
        }

        return TaskResult.SuccessResult;
    }

    public async Task<TaskResult> SetChildrensOrderAsync(PlanetCategory category, long[] order)
    {
        var totalChildren = await _db.PlanetChannels.CountAsync(x => x.ParentId == category.Id);

        if (totalChildren != order.Length)
            return new(false, "Your order does not contain all the children.");

        // Use transaction so we can stop at any failure
        await using var tran = await _db.Database.BeginTransactionAsync();

        List<PlanetChannel> children = new();

        try
        {
            var pos = 0;
            foreach (var child_id in order)
            {
                var child = await _planetChannelService.GetAsync(child_id);
                if (child is null)
                {
                    return new(false, $"Child with id {child_id} does not exist!");
                }

                if (child.ParentId != category.Id)
                    return new(false, $"Category {child_id} is not a child of {category.Id}.");

                child.Position = pos;

                // child.TimeLastActive = DateTime.SpecifyKind(child.TimeLastActive, DateTimeKind.Utc);

                _db.PlanetChannels.Update(child.ToDatabase());

                children.Add(child);

                pos++;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await tran.RollbackAsync();
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }

        await tran.CommitAsync();

        foreach (var child in children)
        {
            _coreHub.NotifyPlanetItemChange(child);
        }

        return new(true, "Success");
    }

    /// <summary>
    /// Validates the parent and position of this category
    /// </summary>
    public async Task<TaskResult> ValidateParentAndPosition(PlanetCategory category)
    {
        if (category.ParentId != null)
        {
            var parent = await _db.PlanetCategories.FindAsync(category.ParentId);
            if (parent == null) return new TaskResult(false, "Could not find parent");
            if (parent.PlanetId != category.PlanetId) return new TaskResult(false, "Parent category belongs to a different planet");
            if (parent.Id == category.Id) return new TaskResult(false, "Cannot be own parent");

            // Automatically determine position in this case
            if (category.Position < 0)
            {
                category.Position = (ushort)await _db.PlanetChannels.CountAsync(x => x.ParentId == category.ParentId);
            }
            else
            {
                // Ensure position is not already taken
                if (!await HasUniquePosition(category))
                    return new TaskResult(false, "The position is already taken.");
            }

            // Ensure this category does not contain itself
            var loop_parent = parent;

            while (loop_parent.ParentId != null)
            {
                if (loop_parent.ParentId == category.Id)
                {
                    return new TaskResult(false, "Cannot create parent loop.");
                }

                loop_parent = await _db.PlanetCategories.FindAsync(loop_parent.ParentId);
            }
        }
        else
        {
            if (category.Position < 0)
            {
                category.Position = (ushort)await _db.PlanetChannels.CountAsync(x => x.PlanetId == category.PlanetId && x.ParentId == null);
            }
            else
            {
                // Ensure position is not already taken
                if (!await HasUniquePosition(category))
                    return new TaskResult(false, "The position is already taken.");
            }
        }

        return TaskResult.SuccessResult;
    }

    public async Task<bool> HasUniquePosition(PlanetChannel channel) =>
        // Ensure position is not already taken
        !await _db.PlanetChannels.AnyAsync(x => x.ParentId == channel.ParentId && // Same parent
                                                x.Position == channel.Position && // Same position
                                                x.Id != channel.Id); // Not self
}