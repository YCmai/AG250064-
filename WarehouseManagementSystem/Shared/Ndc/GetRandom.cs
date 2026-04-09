using System.Text.RegularExpressions;

namespace WarehouseManagementSystem.Shared.Ndc;

public static class GetRandom
{
    public static int getIds(List<int> idHasUsed, int min, int max)
    {
        for (var i = min; i < max; i++)
        {
            if (!idHasUsed.Contains(i))
            {
                return i;
            }
        }

        return 0;
    }

    public static int GenerateRandomSeed()
    {
        var num = Convert.ToInt32(Regex.Match(Guid.NewGuid().ToString(), @"\d+").Value);

        while (num <= 0 || num >= 65535)
        {
            num = Convert.ToInt32(Regex.Match(Guid.NewGuid().ToString(), @"\d+").Value);
        }

        return num;
    }
}

