namespace PlataformaCreditos.Models;

public static class ReglasCredito
{
    public const decimal MultiplicadorMaximoAprobacion = 5m;

    public const decimal MultiplicadorMaximoSolicitud = 10m;

    public static bool EsAprobable(decimal montoSolicitado, decimal ingresosMensuales)
    {
        return montoSolicitado <= ingresosMensuales * MultiplicadorMaximoAprobacion;
    }
}