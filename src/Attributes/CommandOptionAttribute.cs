using System;
using OoLunar.CookieClicker.Entities.CommandFramework;
using Remora.Discord.API.Abstractions.Objects;

namespace OoLunar.CookieClicker.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CommandOptionAttribute : Attribute
    {
        public ApplicationCommandOptionType OptionType { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public bool IsRequired { get; init; }
        public Type? TranslationProvider { get; init; }
        public Type? AutoCompleteProvider { get; init; }
        public int Order { get; init; }

        public CommandOptionAttribute(ApplicationCommandOptionType optionType, string name, string description, bool required, int order = 0)
        {
            OptionType = optionType;
            Name = name;
            Description = description;
            IsRequired = required;
            Order = order;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandOptionAttribute<TAutoCompleteProvider> : CommandOptionAttribute where TAutoCompleteProvider : IAutoCompleteProvider
    {
        public CommandOptionAttribute(ApplicationCommandOptionType optionType, string name, string description, bool required, int order = 0) : base(optionType, name, description, required, order) => AutoCompleteProvider = typeof(TAutoCompleteProvider);
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandOptionAttribute<TTranslationProvider, TAutoCompleteProvider> : CommandOptionAttribute
        where TTranslationProvider : ITranslationProvider
        where TAutoCompleteProvider : IAutoCompleteProvider
    {
        public CommandOptionAttribute(ApplicationCommandOptionType optionType, string name, string description, bool required, int order = 0) : base(optionType, name, description, required, order)
        {
            TranslationProvider = typeof(TTranslationProvider);
            AutoCompleteProvider = typeof(TAutoCompleteProvider);
        }
    }
}
