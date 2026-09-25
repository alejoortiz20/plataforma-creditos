using System.ComponentModel.DataAnnotations;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Models.ViewModels;

public class SolicitudesViewModel : IValidatableObject
{
    [Display(Name = "Estado")]
    public EstadoSolicitud? Estado { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El monto mínimo no puede ser negativo.")]
    [Display(Name = "Monto mínimo")]
    public decimal? MontoMinimo { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El monto máximo no puede ser negativo.")]
    [Display(Name = "Monto máximo")]
    public decimal? MontoMaximo { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha desde")]
    public DateTime? FechaDesde { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha hasta")]
    public DateTime? FechaHasta { get; set; }

    public List<SolicitudCredito> Solicitudes { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MontoMinimo.HasValue && MontoMaximo.HasValue && MontoMinimo.Value > MontoMaximo.Value)
        {
            yield return new ValidationResult(
                "El monto mínimo no puede ser mayor que el monto máximo.",
                new[] { nameof(MontoMinimo) });
        }

        if (FechaDesde.HasValue && FechaHasta.HasValue && FechaDesde.Value.Date > FechaHasta.Value.Date)
        {
            yield return new ValidationResult(
                "La fecha inicial no puede ser posterior a la fecha final.",
                new[] { nameof(FechaDesde) });
        }
    }
}