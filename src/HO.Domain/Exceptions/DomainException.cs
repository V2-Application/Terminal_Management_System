namespace HO.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class TerminalNotFoundException : DomainException
{
    public TerminalNotFoundException(Guid terminalId)
        : base($"Terminal '{terminalId}' was not found.") { }
}

public class StoreNotFoundException : DomainException
{
    public StoreNotFoundException(string storeCode)
        : base($"Store '{storeCode}' was not found.") { }
}

public class InvalidCommandStateException : DomainException
{
    public InvalidCommandStateException(string message) : base(message) { }
}

public class FYJobAlreadyActiveException : DomainException
{
    public FYJobAlreadyActiveException(string fyYear)
        : base($"A FY-Close job for '{fyYear}' is already active.") { }
}
