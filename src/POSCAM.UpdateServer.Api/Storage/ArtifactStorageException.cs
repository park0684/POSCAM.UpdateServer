namespace POSCAM.UpdateServer.Api.Storage;

public sealed class ArtifactStorageException : Exception
{
    public ArtifactStorageFailureType FailureType { get; }

    public ArtifactStorageException(
        ArtifactStorageFailureType failureType,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureType = failureType;
    }
}
