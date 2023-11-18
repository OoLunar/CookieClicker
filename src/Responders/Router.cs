using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HyperSharp.Protocol;
using HyperSharp.Responders;
using HyperSharp.Results;
using Markdig;
using Nodsoft.Markdig.SyntaxHighlighting;

namespace OoLunar.CookieClicker.Responders
{
    public sealed class Router : IValueTaskResponder<HyperContext, HyperStatus>
    {
        public static Type[] Needs { get; } = Array.Empty<Type>();
        private static readonly FrozenDictionary<string, Result<HyperStatus>> _routes;

        static Router()
        {
            MarkdownPipeline markdown = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .UseSyntaxHighlighting()
                .Build();

            Dictionary<string, Result<HyperStatus>> routes = new() { { "/cookie_clicker_dev/api", Result.Success<HyperStatus>(default) } };

            Assembly assembly = typeof(Router).Assembly;
            foreach (string resourceName in assembly.GetManifestResourceNames())
            {
                // Resource names are in namespace.file.name.extension format. Instead of manually parsing the filename,
                // Let's just remove the namespace and convert the rest to a path.
                StringBuilder urlName = new();

                for (int i = "CookieClicker.".Length; i < resourceName.Length; i++)
                {
                    char c = resourceName[i];
                    if (c == '.' && resourceName.LastIndexOf('.') != i)
                    {
                        urlName.Append('/');
                    }
                    else
                    {
                        urlName.Append(c);
                    }
                }

                string resourceContent = new StreamReader(assembly.GetManifestResourceStream(resourceName)!).ReadToEnd();
                if (urlName.ToString().EndsWith(".md"))
                {
                    resourceContent = Markdown.ToHtml(resourceContent, markdown);
                }

                routes.Add($"/cookie_clicker/{urlName}", Result.Success(new HyperStatus(HttpStatusCode.OK, new(), resourceContent)));
            }

            _routes = routes.ToFrozenDictionary();
        }

        public async ValueTask<Result<HyperStatus>> RespondAsync(HyperContext context, CancellationToken cancellationToken = default)
        {
            if (_routes.TryGetValue(context.Route.AbsolutePath, out Result<HyperStatus> result))
            {
                HyperSerializerDelegate serializerDelegate = HyperSerializers.GetSerializerFromFileExtension(context.Route.AbsolutePath[(context.Route.AbsolutePath.LastIndexOf('.') + 1)..]);
                await context.RespondAsync(result.Value, serializerDelegate, cancellationToken);
            }

            return Result.Success<HyperStatus>();
        }
    }
}
