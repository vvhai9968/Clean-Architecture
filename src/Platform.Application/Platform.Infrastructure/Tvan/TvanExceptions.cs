namespace Platform.Infrastructure.Tvan;

/// <summary>Metadata trong DB thiếu hoặc sai — lỗi cấu hình, không phải lỗi mạng. Không retry.</summary>
public sealed class TvanConfigurationException(string message) : Exception(message);

/// <summary>Không lấy được token từ T-VAN.</summary>
public sealed class TvanAuthenticationException(string message) : Exception(message);

/// <summary>Không bóc được response theo ResponseMap đã khai báo.</summary>
public sealed class TvanProtocolException(string message, Exception? inner = null) : Exception(message, inner);
