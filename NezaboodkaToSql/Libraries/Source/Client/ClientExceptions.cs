using System;

namespace Nezaboodka
{
    public static class ErrorStatus
    {
        public const int Success = 0;
        public const int Retry = 1;
        public const int Timeout = 2;
        public const int SecurityError = 3;
        public const int AvailabilityError = 4;
        public const int ClientError = 5;
    }

    public class NezaboodkaException : Exception
    {
        public NezaboodkaException(string message) : base(message)
        {
        }
    }

    public class NezaboodkaRetryException : NezaboodkaException
    {
        public NezaboodkaRetryException(string message) : base(message)
        {
        }
    }

    public class NezaboodkaTimeoutException : NezaboodkaException
    {
        public NezaboodkaTimeoutException(string message) : base(message)
        {
        }
    }

    public class NezaboodkaAvailabilityException : NezaboodkaException
    {
        public NezaboodkaAvailabilityException(string message) : base(message)
        {
        }
    }

    public class NezaboodkaSecurityException : NezaboodkaException
    {
        public NezaboodkaSecurityException(string message) : base(message)
        {
        }
    }
}
