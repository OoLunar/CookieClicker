using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OoLunar.CookieClicker.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace OoLunar.CookieClicker.Entities.CommandFramework
{
    public sealed class Command
    {
        private static readonly CultureInfo EnglishCultureInfo = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");

        public FrozenDictionary<CultureInfo, string> Names { get; init; } = FrozenDictionary<CultureInfo, string>.Empty;
        public FrozenDictionary<CultureInfo, string> Descriptions { get; init; } = FrozenDictionary<CultureInfo, string>.Empty;
        public List<Command> Subcommands { get; init; } = new();

        public Func<Interaction, Task<InteractionResponse>>? MethodPointer { get; init; }
        public List<ApplicationCommandOption> Options { get; init; } = new();
        public DiscordPermissionSet? RequiredPermissions { get; init; }

        public string Name => Names[EnglishCultureInfo];
        public string Description => Descriptions[EnglishCultureInfo];
        public bool IsGroupCommand => Subcommands.Count != 0;

        public Command(MemberInfo memberInfo, CommandAttribute commandAttribute, IServiceProvider serviceProvider)
        {
            Dictionary<CultureInfo, string> names;
            Dictionary<CultureInfo, string> descriptions;
            if (commandAttribute.TranslationProvider is not null && ActivatorUtilities.CreateInstance(serviceProvider, commandAttribute.TranslationProvider) is ITranslationProvider translationProvider)
            {
                names = translationProvider.Translate(commandAttribute.Name);
                descriptions = translationProvider.Translate(commandAttribute.Description);
            }
            else
            {
                names = new() { [EnglishCultureInfo] = commandAttribute.Name };
                descriptions = new() { [EnglishCultureInfo] = commandAttribute.Description };
            }

            Names = names.ToFrozenDictionary();
            Descriptions = descriptions.ToFrozenDictionary();
            RequiredPermissions = memberInfo.GetCustomAttribute<RequirePermissionAttribute>() is RequirePermissionAttribute requirePermissionAttribute ? new DiscordPermissionSet(requirePermissionAttribute.Permission) : null;
        }

        public Command(CommandAttribute commandAttribute, MethodInfo methodInfo, IServiceProvider serviceProvider) : this(methodInfo, commandAttribute, serviceProvider)
        {
            List<ApplicationCommandOption> options = new();
            foreach (CommandOptionAttribute methodOption in methodInfo.GetCustomAttributes<CommandOptionAttribute>().OrderBy(x => x.Order))
            {
                Dictionary<CultureInfo, string> optionNames;
                Dictionary<CultureInfo, string> optionDescriptions;
                if (methodOption.TranslationProvider is not null && ActivatorUtilities.CreateInstance(serviceProvider, methodOption.TranslationProvider) is ITranslationProvider translationProvider)
                {
                    optionNames = translationProvider.Translate($"{commandAttribute.Name}.{methodOption.Name}");
                    optionDescriptions = translationProvider.Translate($"{commandAttribute.Description}.{methodOption.Description}");
                }
                else
                {
                    optionNames = new() { [EnglishCultureInfo] = methodOption.Name };
                    optionDescriptions = new() { [EnglishCultureInfo] = methodOption.Description };
                }

                options.Add(new ApplicationCommandOption(
                    methodOption.OptionType,
                    optionNames[EnglishCultureInfo],
                    optionDescriptions[EnglishCultureInfo],
                    default,
                    methodOption.IsRequired,
                    EnableAutocomplete: methodOption.AutoCompleteProvider is not null,
                    NameLocalizations: optionNames.ToFrozenDictionary(x => x.Key.Name, x => x.Value),
                    DescriptionLocalizations: optionDescriptions.ToFrozenDictionary(x => x.Key.Name, x => x.Value)
                ));
            }

            MethodPointer = methodInfo.CreateDelegate<Func<Interaction, Task<InteractionResponse>>>();
            Options = options.ToList();
        }

        public Command(CommandAttribute commandAttribute, Type type, IServiceProvider serviceProvider) : this(type, commandAttribute, serviceProvider)
        {
            List<Command> subcommands = new();
            foreach (MethodInfo methodInfo in type.GetMethods())
            {
                if (methodInfo.GetCustomAttribute<CommandAttribute>() is CommandAttribute methodCommandAttribute)
                {
                    subcommands.Add(new Command(methodCommandAttribute, methodInfo, serviceProvider));
                }
            }

            if (subcommands.Count != 0)
            {
                Subcommands = subcommands.ToList();
                return;
            }

            foreach (Type subcommand in type.GetNestedTypes())
            {
                if (subcommand.GetCustomAttribute<CommandAttribute>() is CommandAttribute subcommandAttribute)
                {
                    subcommands.Add(new Command(subcommandAttribute, subcommand, serviceProvider));
                }
            }

            Subcommands = subcommands.Count == 0
                ? throw new InvalidOperationException($"Command {commandAttribute.Name} has no subcommands or methods.")
                : subcommands.ToList();
        }

        public static explicit operator BulkApplicationCommandData(Command command) => new(
            command.Names[EnglishCultureInfo],
            command.Descriptions[EnglishCultureInfo],
            default,
            command.Subcommands.Count == 0 ? command.Options : command.Subcommands.Select(subcommand => (ApplicationCommandOption)subcommand).ToList(),
            ApplicationCommandType.ChatInput,
            command.Names.ToFrozenDictionary(x => x.Key.Name, x => x.Value),
            command.Descriptions.ToFrozenDictionary(x => x.Key.Name, x => x.Value)
        );

        public static explicit operator ApplicationCommandOption(Command command) => new(
            command.Subcommands.Count == 0 ? ApplicationCommandOptionType.SubCommand : ApplicationCommandOptionType.SubCommandGroup,
            command.Names[EnglishCultureInfo],
            command.Descriptions[EnglishCultureInfo],
            Options: command.Options,
            NameLocalizations: command.Names.ToFrozenDictionary(x => x.Key.Name, x => x.Value),
            DescriptionLocalizations: command.Descriptions.ToFrozenDictionary(x => x.Key.Name, x => x.Value)
        );
    }
}
