﻿using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

namespace Valour.Shared
{
    public struct TaskResult : ITaskResult
    {
        public static readonly TaskResult SuccessResult = new(true, "Success");

        [JsonInclude]
        [JsonPropertyName("Message")]
        public string Message { get; set; }
        
        [JsonInclude]
        [JsonPropertyName("Details")]
        public string Details { get; set; }

        [JsonInclude]
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        public TaskResult(bool success, string message, string details = null)
        {
            Success = success;
            Message = message;
            Details = details;
        }

        public static TaskResult FromError(ITaskResult error) => new(false, error.Message);

        public static TaskResult FromError(string error) => new(false, error);

        public override string ToString()
        {
            if (Success)
            {
                return $"[SUCC] {Message}";
            }

            return $"[FAIL] {Message}";
        }

    }
    public struct TaskResult<T> : ITaskResult
    {
        [JsonInclude]
        [JsonPropertyName("Message")]
        public string Message { get; set; }
        
        [JsonInclude]
        [JsonPropertyName("Details")]
        public string Details { get; set; }

        [JsonInclude]
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonInclude]
        [JsonPropertyName("Data")]
        public T Data { get; set; }

        public TaskResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public TaskResult(bool success, string message, T data, string details = null)
        {
            Success = success;
            Message = message;
            Data = data;
            Details = details;
        }

        public static TaskResult<T> FromData(T data) => new(true, "Success", data);

        public static TaskResult<T> FromError(ITaskResult error) => new(false, error.Message, default(T), error.Details);

        public static TaskResult<T> FromError(string error, string details = null) => new(false, error, default(T), details);

        public bool IsSuccessful(out T value)
        {
            value = Data;
            return Success;
        }

        public override string ToString()
        {
            if (Success)
            {
                return $"[SUCC] {Message}";
            }

            return $"[FAIL] {Message}";
        }
    }

    public interface ITaskResult
    {
        string Message { get; set; }
        
        string Details { get; set; }

        bool Success { get; set; }
    }

    public struct HttpResult
    {
        [JsonInclude]
        [JsonPropertyName("Message")]
        public string Message { get; set; }

        [JsonInclude]
        [JsonPropertyName("Status")]
        public int Status { get; set; }

        public bool Success
        {
            get
            {
                int x = Status - 200;
                if (x < 0) return false;
                if (x > 99) return false;
                return true;
            }
        }

        public HttpResult(string message, int status)
        {
            Message = message;
            Status = status;
        }

        public override string ToString()
        {
            return $"[{Status}]: {Message}";
        }
    }

    public struct HttpResult<T>
    {
        [JsonInclude]
        [JsonPropertyName("Message")]
        public string Message { get; set; }

        [JsonInclude]
        [JsonPropertyName("Status")]
        public int Status { get; set; }


        [JsonInclude]
        [JsonPropertyName("Result")]
        public T Result { get; set; }

        public bool Success
        {
            get
            {
                int x = Status - 200;
                if (x < 0) return false;
                if (x > 99) return false;
                return true;
            }
        }

        public HttpResult(string message, int status, T result)
        {
            Message = message;
            Status = status;
            Result = result;
        }

        public override string ToString()
        {
            return $"[{Status}]: {Message}";
        }
    }
}
