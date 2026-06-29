using System.ComponentModel.DataAnnotations;

public class Folder
{
    [Key]
    public string Schema { get; set; }
    public bool HasData { get; set; }
}
