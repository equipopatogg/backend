namespace Pactogg.Api.Common;

public static class ErrorCodes
{
    public const string InvalidInput = "INVALID_INPUT";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string NicknameAlreadyExists = "NICKNAME_ALREADY_EXISTS";
    public const string GroupLimitReached = "GROUP_LIMIT_REACHED";
    public const string NotGroupMember = "NOT_GROUP_MEMBER";
    public const string NotContractTarget = "NOT_CONTRACT_TARGET";
    public const string EvidenceLimitReached = "EVIDENCE_LIMIT_REACHED";
    public const string InvalidContractState = "INVALID_CONTRACT_STATE";
    public const string ContentRejected = "CONTENT_REJECTED";
    public const string InvalidEvidenceUrl = "INVALID_EVIDENCE_URL";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string ImageTooLarge = "IMAGE_TOO_LARGE";
    public const string InvalidBond = "INVALID_BOND";
    public const string InvalidTarget = "INVALID_TARGET";
    public const string RateLimited = "RATE_LIMITED";
    public const string ModerationUnavailable = "MODERATION_UNAVAILABLE";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
}
