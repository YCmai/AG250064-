namespace WarehouseManagementSystem.Models.Rcs;

public class EventLog
{
    public int Counter { get; set; }
    public int EventCode { get; set; }
    public int Parameter1 { get; set; }
    public int Parameter2 { get; set; }
    public int Parameter3 { get; set; }
    public string? EventString { get; set; }
    public DateTime EventTime { get; set; }
}


