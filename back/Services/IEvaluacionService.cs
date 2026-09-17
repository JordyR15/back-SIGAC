using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IEvaluacionService
    {
        Task<EvaluacionDto> CreateEvaluacionAsync(CreateEvaluacionDto createEvaluacionDto);
        Task<IEnumerable<EvaluacionDto>> GetEvaluacionesByClaseAsync(int claseId);
        Task<EvaluacionDto> GetEvaluacionByIdAsync(int id);
        Task<ResultadoEvaluacionDiagnosticaDto> EjecutarEvaluacionDiagnosticaAsync(int evaluacionId, int estudianteId);
        Task<IEnumerable<ResultadoEvaluacionDiagnosticaDto>> GetResultadosEvaluacionAsync(int evaluacionId);
        Task<IndicadorCualitativoDto> CreateIndicadorCualitativoAsync(CreateIndicadorCualitativoDto createIndicadorCualitativoDto);
        Task<IEnumerable<IndicadorCualitativoDto>> GetIndicadoresByEvaluacionAsync(int evaluacionId);
        Task<bool> SetMinimoNotaAsync(SetMinimoNotaDto setMinimoNotaDto);
        Task<IEnumerable<AlertaTempranaDto>> DetectarAlertasTempranasAsync(int claseId);
    }
}
