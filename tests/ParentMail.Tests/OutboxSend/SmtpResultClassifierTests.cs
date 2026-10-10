using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using ParentMail.Core;
using ParentMail.Core.Features.OutboxSend;

namespace ParentMail.Tests.OutboxSend;

public sealed class SmtpResultClassifierTests
{
    [Fact]
    public void Authentication_failure_is_AuthFailed()
        => Assert.Equal(SendResult.AuthFailed, SmtpResultClassifier.Classify(new AuthenticationException("bad")));

    [Theory]
    [InlineData(SmtpStatusCode.MailboxBusy, SendResult.Transient)]
    [InlineData(SmtpStatusCode.InsufficientStorage, SendResult.Transient)]
    [InlineData(SmtpStatusCode.MailboxUnavailable, SendResult.Permanent)]
    [InlineData(SmtpStatusCode.ExceededStorageAllocation, SendResult.Permanent)]
    public void Command_error_is_split_by_status_class(SmtpStatusCode code, SendResult expected)
        => Assert.Equal(expected, SmtpResultClassifier.Classify(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, code, "x")));

    [Fact]
    public void Authentication_required_command_error_is_AuthFailed()
        => Assert.Equal(SendResult.AuthFailed,
            SmtpResultClassifier.Classify(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.AuthenticationRequired, "x")));

    [Fact]
    public void Network_errors_are_Transient()
    {
        Assert.Equal(SendResult.Transient, SmtpResultClassifier.Classify(new SocketException()));
        Assert.Equal(SendResult.Transient, SmtpResultClassifier.Classify(new IOException()));
        Assert.Equal(SendResult.Transient, SmtpResultClassifier.Classify(new TimeoutException()));
        Assert.Equal(SendResult.Transient, SmtpResultClassifier.Classify(new SmtpProtocolException("x")));
        Assert.Equal(SendResult.Transient, SmtpResultClassifier.Classify(new ServiceNotConnectedException("x")));
    }
}
