namespace MajdsApp.SharedKernel.Exceptions;

/// <summary>Maps to ResponseDto Code=NotFound / HTTP 404 (F-Errors).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Maps to ResponseDto Code=Forbidden / HTTP 403 (F-Errors).</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Maps to ResponseDto Code=Unauthorized / HTTP 401 (F-Errors).</summary>
public class UnauthorizedAppException(string message) : Exception(message);

/// <summary>Maps to ResponseDto Code=Conflict / HTTP 409 (F-Errors).</summary>
public class ConflictException(string message) : Exception(message);
