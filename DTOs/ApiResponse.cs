using System;
using System.Collections.Generic;
using System.Linq;

namespace crm_api.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ExceptionMessage { get; set; } = string.Empty;
        public T? Data { get; set; }
        public object? Details { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int StatusCode { get; set; } = 200;
        public string ClassName { get; set; } = string.Empty;
    
    //Veri tipini otomatik getirir
    private static string GetGenericTypeDisplayName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetGenericTypeDisplayName));
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{genericArgs}>";
    }

    public override string ToString()
    {
        return $"{ClassName} [Success={Success}, StatusCode={StatusCode}, Message={Message}]";
    }

        public static ApiResponse<T> SuccessResult(T? data, string message)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                StatusCode = 200,
                ClassName = $"ApiResponse<{(data == null ? "Null" : GetGenericTypeDisplayName(typeof(T)))}>"
  
            };
        }

        // Preferred signature (message, exceptionMessage, statusCode)
        public static ApiResponse<T> ErrorResult(string message, string? exceptionMessage = null,int statusCode = 500, object? details = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                StatusCode = statusCode,
                ExceptionMessage = exceptionMessage ?? string.Empty,
                Details = details,
                ClassName = $"ApiResponse<{GetGenericTypeDisplayName(typeof(T))}>"
            };
        }

    }

}
