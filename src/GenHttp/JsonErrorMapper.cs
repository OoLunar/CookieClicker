using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion.Providers.Json;
using Microsoft.Extensions.Logging;
using OoLunar.CookieClicker.Entities;

namespace OoLunar.CookieClicker.GenHttp
{
    public sealed class JsonErrorMapper : IErrorMapper<Exception>
    {
        private readonly ILogger<JsonErrorMapper> _logger;
        public JsonErrorMapper(ILogger<JsonErrorMapper> logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public ValueTask<IResponse?> GetNotFound(IRequest request, IHandler handler) => new(GetResponse(request, ResponseStatus.NotFound, new("Not found")));
        public ValueTask<IResponse?> Map(IRequest request, IHandler handler, Exception error)
        {
            _logger.LogError(error, "An unhandled exception occured while handling a {Method} request to {Path}:", request.Method.RawMethod, request.Target.Path);
            return error is ProviderException providerException
                ? new(GetResponse(request, providerException.Status, new(error.Message)))
                : new(GetResponse(request, ResponseStatus.InternalServerError, new(error.Message)));
        }

        private static IResponse GetResponse(IRequest request, ResponseStatus status, HttpError error) => request.Respond()
            .Status(status)
            .Content(new JsonContent(error, new()))
            .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
            .Build();
    }
}
