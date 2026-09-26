namespace ZahSellerAI.Shared.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public ApiError? Error { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> ErrorResponse(string code, string message, List<string>? details = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details ?? new List<string>()
            }
        };
    }
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
}
