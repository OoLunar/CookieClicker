using System;
using OoLunar.CookieClicker.Entities.CommandFramework;

namespace OoLunar.CookieClicker.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class AutoCompleteAttribute<T> : Attribute where T : IAutoCompleteProvider
    {
        public Type Provider { get; }
        public AutoCompleteAttribute() => Provider = typeof(T);
    }
}
