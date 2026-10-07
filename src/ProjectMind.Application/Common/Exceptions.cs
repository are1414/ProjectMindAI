namespace ProjectMind.Application.Common;

/// <summary>İstenen kayıt bulunamadı (HTTP 404).</summary>
public sealed class NotFoundException(string entityName, int id)
    : Exception($"{entityName} bulunamadı (Id: {id}).");

/// <summary>Bir iş kuralı ihlal edildi (HTTP 400).</summary>
public sealed class BusinessRuleException(string message) : Exception(message);
