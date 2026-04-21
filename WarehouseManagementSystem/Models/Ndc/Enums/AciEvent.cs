namespace WarehouseManagementSystem.Models.Enums;

public class AciEvent
{
    private static int _idSeed;
    private readonly int _id = _idSeed++;
    private readonly DateTime _time = DateTime.Now;
    private bool _overDate;

    public int ID => _id;

    public int Index { get; set; }

    public AciHostEventTypeEnum Type { get; set; }

    public int Parameter1 { get; set; }

    public int Parameter2 { get; set; }

    public DateTime Time => _time;

    public bool OverDate => _overDate;

    public void SetOverDate()
    {
        _overDate = true;
    }
}

