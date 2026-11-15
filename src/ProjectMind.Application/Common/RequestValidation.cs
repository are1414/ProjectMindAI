using System.ComponentModel.DataAnnotations;

namespace ProjectMind.Application.Common;

/// <summary>
/// İstek DTO'larının DataAnnotations kurallarını (zorunlu alan, aralık, uzunluk) çalıştırır; mesajlar DTO'daki Türkçe
/// metinlerdir. Elle yönetim formları kullanır (D33): formda görünmeyen alanlar (ör. üst işin eforu) atlanabilir.
/// İş kuralları (döngü, tek beceri, %100 kuralı…) servislerde kalır.
/// </summary>
public static class RequestValidation
{
    public static IReadOnlyList<string> Errors(object request, params string[] ignoredMembers)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results
            .Where(r => !r.MemberNames.Any(ignoredMembers.Contains))
            .Select(r => r.ErrorMessage ?? "Geçersiz değer.")
            .ToList();
    }
}
