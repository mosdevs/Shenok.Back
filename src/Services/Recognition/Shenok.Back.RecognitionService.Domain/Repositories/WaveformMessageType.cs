namespace Shenok.Back.RecognitionService.Domain.Repositories;

/// <summary>
/// Inbox message names. The enum name is the value stored in <c>message_type</c>
/// and the only names the <c>ck_inbox_messages_type</c> check allows.
/// </summary>
public enum WaveformMessageType
{
    WaveformRegistered = 0,
    WaveformRevoked = 1
}
