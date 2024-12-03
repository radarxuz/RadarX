using System.ComponentModel.DataAnnotations;
using TestX.Core.Enums;
using TestX.Data.Common;

namespace TestX.Data.Entities;

public class Data : Auditable
{
    [Required]
    public string Uid { get; set; }

    [Required]
    public string Link { get; set; }

    [Required]
    public UrlType UrlType { get; set; }
    
    
    [Required]
    public bool IsCompleted { get; set; } = false;
}