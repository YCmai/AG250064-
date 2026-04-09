namespace WarehouseManagementSystem.Shared.Ndc;

public interface IGroupMaxTaskCountCategory
{
    int GetMaxTaskCount(string groupName);
}

public class DefaultGroupMaxTaskCountCategory : IGroupMaxTaskCountCategory
{
    public int GetMaxTaskCount(string groupName)
    {
        return 1000;
    }
}

