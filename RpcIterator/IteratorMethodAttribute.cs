using System;

namespace Neo.Plugins
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class IteratorMethodAttribute : Attribute
    {
        public string Name { get; set; }
    }
}
