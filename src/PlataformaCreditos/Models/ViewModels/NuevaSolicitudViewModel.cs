using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models.ViewModels;

public class NuevaSolicitudViewModel
{
    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor que cero.")]
    [Display(Name = "Monto solicitado")]
    public decimal MontoSolicitado { get; set; }

    public decimal IngresosMensuales { get; set; }

    public decimal MontoMaximoSolicitable { get; set; }

    public bool MaximoDefinido { get; set; }
}