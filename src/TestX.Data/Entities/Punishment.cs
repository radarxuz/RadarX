using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using TestX.Core.Enums;
using TestX.Data.Common;

namespace TestX.Data.Entities;

public class Punishment : Auditable
{

    [Required]
    public string Uid { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public string Link { get; set; }

    [Required]
    public UrlType UrlType { get; set; }

    [Required]
    public long CameraId { get; set; }

    [ForeignKey("CameraId")]
    public Camera Camera { get; set; }
}
