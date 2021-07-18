using System;

namespace Neo.Plugins
{
    public class IteratorException : Exception
    {
        public IteratorException(int code, string message) : base(message)
        {
            HResult = code;
        }
    }
}
