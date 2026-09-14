namespace Enrichly.JobAutomation.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", context.TraceIdentifier);
            var status = exception is TimeoutException ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
            await Results.Problem(statusCode: status, title: "Request could not be completed.", extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }
    }
}
