using Newtonsoft.Json;
using Sitecore.ExperienceEditor.Speak.Server.Responses;
using Sitecore.Security.Authentication;
using Sitecore.Web.Authentication;
using System.Web;

namespace Sitecore.Support.ExperienceEditor.Speak.Server
{
    public class RequestHandler : Sitecore.ExperienceEditor.Speak.Server.RequestHandler
    {
        protected override string Process(HttpContext context)
        {
            string result;
            if (!TicketManager.IsCurrentTicketValid() || AuthenticationManager.IsAuthenticationTicketExpired() || HttpContext.Current.Request.Cookies["__RequestVerificationToken"] == null)
            {
                Response value = new Response
                {
                    Error = true,
                    ErrorMessage = "Session Expired",
                    PostScriptFunc = "location.reload()"
                };
                result = JsonConvert.SerializeObject(value);
            }
            else
            {
                result = base.Process(context);
            }
            return result;
        }
    }
}