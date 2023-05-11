using System;
using OoLunar.CookieClicker.Entities.CommandFramework;

namespace OoLunar.CookieClicker.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class CommandAttribute : Attribute
    {
        public string Name { get; init; }
        public string Description { get; init; }
        public Type? TranslationProvider { get; init; }

        public CommandAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CommandAttribute<T> : CommandAttribute where T : ITranslationProvider?
    {
        public CommandAttribute(string name, string description) : base(name, description) => TranslationProvider = typeof(T);
    }
}
