using Microsoft.AspNetCore.Mvc;
using SharedKernel.Results;

namespace Store.Web.Infrastructure.ExceptionHandling;

/// <summary>
/// Turns an Application-layer <see cref="Result"/>/<see cref="Result{TValue}"/> into the
/// controller response, using the same status-code mapping as <see cref="GlobalExceptionHandler"/>
/// so a client can't tell, from the response shape, whether a failure came back as a Result or
/// was thrown as an exception. Keeps controllers thin: an action is
/// <c>var result = await _sender.Send(command); return result.ToActionResult();</c>, not a
/// switch statement per action.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess
            ? new OkResult()
            : ToProblem(result.Error);

    public static IActionResult ToActionResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess
            ? new OkObjectResult(result.Value)
            : ToProblem(result.Error);

    private static ObjectResult ToProblem(Error error)
    {
        var statusCode = HttpStatusCodeMapper.FromErrorType(error.Type);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Type.ToString(),
            Detail = error.Message,
        };
        problemDetails.Extensions["code"] = error.Code;

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
