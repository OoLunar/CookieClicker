using System;
using OoLunar.CookieClicker.Entities.CommandFramework;

namespace OoLunar.CookieClicker.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class CommandAttribute(string name, string description, Type? translationProvider = null) : Attribute
    {
        public string Name { get; init; } = name;
        public string Description { get; init; } = description;
        public Type? TranslationProvider { get; init; } = translationProvider;
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CommandAttribute<T>(string name, string description) : CommandAttribute(name, description, typeof(T)) where T : ITranslationProvider?;
}
