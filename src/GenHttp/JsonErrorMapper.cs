using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion.Providers.Json;
using Microsoft.Extensions.Logging;
using OoLunar.CookieClicker.Entities;

namespace OoLunar.CookieClicker.GenHttp
{
    public sealed class JsonErrorMapper(ILogger<JsonErrorMapper> logger) : IErrorMapper<Exception>
    {
        private readonly ILogger<JsonErrorMapper> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public ValueTask<IResponse?> GetNotFound(IRequest request, IHandler handler) => new(GetResponse(request, ResponseStatus.NotFound, new("Not found")));
        public ValueTask<IResponse?> Map(IRequest request, IHandler handler, Exception error)
        {
            switch (error)
            {
                case ProviderException providerException:
                    if (providerException.Status != ResponseStatus.Forbidden)
                    {
                        HttpLogger.HttpHandleInternalError(_logger, request.Method.RawMethod, request.Target.Path, (int)providerException.Status, providerException.Message, error);
                    }
                    return new(GetResponse(request, providerException.Status, new(providerException.Message)));
                default:
                    HttpLogger.HttpHandleInternalError(_logger, request.Method.RawMethod, request.Target.Path, (int)ResponseStatus.InternalServerError, error.Message, error);
                    return new(GetResponse(request, ResponseStatus.InternalServerError, new(error.Message)));
            }
        }

        private static IResponse GetResponse(IRequest request, ResponseStatus status, HttpError error) => request.Respond()
            .Status(status)
            .Content(new JsonContent(error, new()))
            .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
            .Build();
    }
}
