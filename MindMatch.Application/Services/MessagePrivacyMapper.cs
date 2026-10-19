using MindMatch.Application.Dtos.Message;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public static class MessagePrivacyMapper
{
    public static void ApplyReadReceiptPreference(MessageResponse message, Guid viewerId,
        bool recipientShowsReadReceipts)
    {
        if (message.SenderId == viewerId && !recipientShowsReadReceipts && message.Status == MessageStatus.Read)
        {
            message.Status = MessageStatus.Delivered;
            message.ReadAtUtc = null;
        }
    }
}
