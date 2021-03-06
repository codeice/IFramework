﻿using IFramework.SysExceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Sample.Command
{
    public enum ErrorCode
    {
        NoError,
        UsernameAlreadyExists,
        WrongUsernameOrPassword,
        UserNotExists,
        CommandInvalid = 0x7ffffffe,
        UnknownError = 0x7fffffff
    }

    public class SysException : DomainException
    {
        public ErrorCode ErrorCode { get; set; }
        public SysException() { }
        protected SysException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = (ErrorCode)info.GetValue("ErrorCode", typeof(ErrorCode));
        }
        public SysException(ErrorCode errorCode, string message = null)
            : base(message ?? errorCode.ToString())
        {
            ErrorCode = errorCode;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ErrorCode", this.ErrorCode);
            base.GetObjectData(info, context);
        }
    }
}
