using System;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using Microsoft.Extensions.Logging;

namespace OoLunar.CookieClicker.GenHttp
{
    public sealed class SerilogCompanion : IServerCompanion
    {
        private readonly ILogger<SerilogCompanion> _logger;

        public SerilogCompanion(ILogger<SerilogCompanion> logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        public void OnServerError(ServerErrorScope scope, Exception error) => _logger.LogError(error, "A {Scope} error has occured:", scope);
        public void OnRequestHandled(IRequest request, IResponse response)
        {
            if (response.Status.RawStatus is >= 200 and < 300)
            {
                _logger.LogDebug("Handled {Method} request to {Path} with status {Status} {Response}", request.Method.RawMethod, request.Target.Path, response.Status.RawStatus, response.Status.Phrase);
            }
            else if (response.Status.RawStatus is >= 500 and < 600)
            {
                _logger.LogError("Handled {Method} request to {Path} with status {Status} {Response}", request.Method.RawMethod, request.Target.Path, response.Status.RawStatus, response.Status.Phrase);
            }
            else
            {
                _logger.LogInformation("Handled {Method} request to {Path} with status {Status} {Response}", request.Method.RawMethod, request.Target.Path, response.Status.RawStatus, response.Status.Phrase);
            }
        }
    }
}
