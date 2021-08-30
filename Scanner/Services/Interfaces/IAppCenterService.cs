using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;

namespace Scanner.Services
{
    public interface IAppCenterService
    {
        void TrackEvent(AppCenterEvent appCenterEvent, IDictionary<string, string> properties = null);
        void TrackError(Exception exception, IDictionary<string, string> properties = null,
            params ErrorAttachmentLog[] attachments);
    }
}
