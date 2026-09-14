namespace Platform.Domain.Platform.Tvan.Abstractions;

/// <summary>
/// Sinh MTDiep theo QĐ 1450: "V" + MST + GUID viết hoa, bỏ dấu gạch ngang (tổng 46 ký tự).
/// </summary>
public interface ICorrelationKeyGenerator
{
    string Create(string taxCode);
}
