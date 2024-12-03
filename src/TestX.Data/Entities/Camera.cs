using TestX.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using TestX.Data.Common;

namespace TestX.Data.Entities;

/// <summary>
/// 
/// </summary>
public class Camera : Auditable
{

    [Required]
    public CameraType CameraType { get; set; }

    [Required]
    public double Latitude { get; set; }

    [Required]
    public double Longitude { get; set; }
}