namespace YaalTrail.Api.Services;

// Thrown by the plan services when the visitor's request is invalid
// (bad time format, unknown place, wrong manual order). The controller returns 400.
public class PlanRequestException : Exception
{
    public PlanRequestException(string message) : base(message)
    {
    }
}
