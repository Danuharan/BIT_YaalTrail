namespace YaalTrail.Api.Services;

// Thrown by the admin services when the submitted data breaks a rule
// (FR-42 radius, FR-43 opening hours, BR-05 category, ...). The controller returns 400.
public class AdminRequestException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public AdminRequestException(IReadOnlyList<string> errors) : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public AdminRequestException(string error) : this(new[] { error })
    {
    }
}

// FR-46: the action conflicts with existing data (e.g. deleting a category that has places).
// The controller returns 409.
public class AdminConflictException : Exception
{
    public AdminConflictException(string message) : base(message)
    {
    }
}
