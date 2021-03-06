﻿
using IFramework.Infrastructure;
using IFramework.Infrastructure.Logging;
using IFramework.SysExceptions;
using IFramework.SysExceptions.ErrorCodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace IFramework.AspNet
{
    public class ApiResult
    {
        public bool success { get; set; }
        public int errorCode { get; set; }
        public string message { get; set; }

        public ApiResult()
        {
            success = true;
        }

        public ApiResult(int errorCode, string message = null)
        {
            this.errorCode = errorCode;
            this.message = message;
            success = false;
        }

    }

    public class ApiResult<TResult> : ApiResult
    {
        public TResult result { get; set; }

        public ApiResult()
        {
            success = true;
        }
        public ApiResult(TResult result)
            : this()
        {
            this.result = result;
        }

        public ApiResult(int errorCode, string message = null)
            : base(errorCode, message)
        {
           
        }
    }

    public static class ExceptionManager
    {
        static ILogger _logger = IoCFactory.Resolve<ILoggerFactory>().Create(typeof(ExceptionManager));
        public async static Task<ApiResult<T>> ProcessAsync<T>(Func<Task<T>> func, bool continueOnCapturedContext = false)
        {
            ApiResult<T> apiResult = null;
            try
            {
                var t = await func().ConfigureAwait(continueOnCapturedContext);
                apiResult = new ApiResult<T>(t);
            }
            catch (Exception ex)
            {
                var baseException = ex.GetBaseException();
                if (baseException is SysException)
                {
                    var sysException = baseException as SysException;
                    apiResult = new ApiResult<T>(sysException.ErrorCode, sysException.Message);
                }
                else
                {
                    apiResult = new ApiResult<T>(ErrorCode.UnknownError, baseException.Message);
                    _logger.Error(ex);
                }
            }
            return apiResult;
        }

        public async static Task<ApiResult> ProcessAsync(Func<Task> func, bool continueOnCapturedContext = false)
        {
            ApiResult apiResult = null;
            try
            {
                await func().ConfigureAwait(continueOnCapturedContext);
                apiResult = new ApiResult();
            }
            catch (Exception ex)
            {
                var baseException = ex.GetBaseException();
                if (baseException is SysException)
                {
                    var sysException = baseException as SysException;
                    apiResult = new ApiResult(sysException.ErrorCode, sysException.Message);
                }
                else
                {
                    apiResult = new ApiResult(ErrorCode.UnknownError, baseException.Message);
                    _logger.Error(ex);
                }
            }
            return apiResult;
        }

        public static ApiResult Process(Action action)
        {
            ApiResult apiResult = null;
            try
            {
                action();
                apiResult = new ApiResult();
            }
            catch (Exception ex)
            {
                var baseException = ex.GetBaseException();
                if (baseException is SysException)
                {
                    var sysException = baseException as SysException;
                    apiResult = new ApiResult(sysException.ErrorCode, sysException.Message);
                }
                else
                {
                    apiResult = new ApiResult(ErrorCode.UnknownError,baseException.Message);
                    _logger.Error(ex);
                }
            }
            return apiResult;
        }

        public static ApiResult<T> Process<T>(Func<T> func)
        {
            ApiResult<T> apiResult = null;
            try
            {
                var result = func();
                if (result != null)
                {
                    apiResult = new ApiResult<T>(result);
                }
                else
                {
                    apiResult = new ApiResult<T>();
                }
            }
            catch (Exception ex)
            {
                var baseException = ex.GetBaseException();
                if (baseException is SysException)
                {
                    var sysException = baseException as SysException;
                    apiResult = new ApiResult<T>(sysException.ErrorCode, sysException.Message);
                }
                else
                {
                    apiResult = new ApiResult<T>(ErrorCode.UnknownError, baseException.Message);
                    _logger.Error(ex);
                }
            }
            return apiResult;
        }
    }
}