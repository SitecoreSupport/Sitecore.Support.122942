using Newtonsoft.Json;
using Sitecore.Diagnostics;
using Sitecore.ExperienceEditor.Speak.Attributes;
using Sitecore.ExperienceEditor.Speak.Server.Requests;
using Sitecore.ExperienceEditor.Speak.Server.Responses;
using Sitecore.Globalization;
using Sitecore.Reflection;
using Sitecore.Web.Authentication;
using System;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Helpers;
using System.Web.SessionState;

namespace Sitecore.ExperienceEditor.Speak.Server
{
    public class RequestHandler : IHttpHandler, IRequiresSessionState
    {
        private readonly RequestArgsFactory requestArgsFactory = new RequestArgsFactory();

        private readonly RequestRepository requestRepository = new RequestRepository();

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                Assert.ArgumentNotNull(context, "context");
                Assert.IsNotNull(context.Request, "Request is null");
                Assert.IsNotNull(context.Response, "Response is null");
                context.Response.ContentType = "application/json";
                context.Response.Write(this.Process(context));
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Error processing command url:{0} error:{1}", context.Request.RawUrl, ex), ex);
                context.Response.Write(JsonConvert.SerializeObject(new Response
                {
                    Error = true,
                    ErrorMessage = ClientHost.Globalization.Translate("A serious error occurred please contact the administrator")
                }));
            }
            finally
            {
                context.Response.Flush();
                context.Response.End();
            }
        }

        protected virtual string Process(HttpContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.IsNotNull(context.Request, "Request is null");
            Assert.IsNotNull(context.Response, "Response is null");
            RequestArgs requestArgs = this.requestArgsFactory.Create(context.Request);
            Assert.IsNotNull(requestArgs, string.Format("Could not retrieve request arguments from url:{0}", context.Request.RawUrl));
            Request request = this.requestRepository.Get(requestArgs);
            Assert.IsNotNull(request, string.Format("Could not retrieve request class for url:{0}", context.Request.RawUrl));
            AntiForgery.Validate();
            if (!TicketManager.IsCurrentTicketValid())
            {
                string message = "User ticket is not valid";
                Log.Error(message, this);
                return JsonConvert.SerializeObject(RequestHandler.GetErrorResponse(Translate.Text("An error occurred.")));
            }
            MethodInfo method = ReflectionUtil.GetMethod(request, "ProcessRequest", new object[0]);
            if (method.GetCustomAttributes(typeof(SitecoreAuthorizeAttribute), true).Any<object>())
            {
                SitecoreAuthorizeAttribute sitecoreAuthorizeAttribute = (SitecoreAuthorizeAttribute)method.GetCustomAttributes(typeof(SitecoreAuthorizeAttribute), true).First<object>();
                if (sitecoreAuthorizeAttribute != null && !sitecoreAuthorizeAttribute.IsAllowed())
                {
                    string message2 = string.Format("User {0} does not have enough rights to run {1}. Roles that allow running this method: {2}.", Context.User.Name, (method.DeclaringType == null) ? method.Name : method.DeclaringType.FullName, sitecoreAuthorizeAttribute.Roles);
                    Log.Error(message2, this);
                    return JsonConvert.SerializeObject(RequestHandler.GetErrorResponse(Translate.Text("An error occurred.")));
                }
            }
            if (method.GetCustomAttributes(typeof(HasItemPermissionsAttribute), true).Any<object>())
            {
                HasItemPermissionsAttribute hasItemPermissionsAttribute = (HasItemPermissionsAttribute)method.GetCustomAttributes(typeof(HasItemPermissionsAttribute), true).First<object>();
                if (hasItemPermissionsAttribute != null && !hasItemPermissionsAttribute.IsAllowed())
                {
                    string message3 = string.Format("User {0} does not have enough rights to item {1} {2}.", Context.User.Name, hasItemPermissionsAttribute.Path, hasItemPermissionsAttribute.Id);
                    Log.Error(message3, this);
                    return JsonConvert.SerializeObject(RequestHandler.GetErrorResponse(Translate.Text("An error occurred.")));
                }
            }
            object value = ReflectionUtil.CallMethod(request, requestArgs.MethodName, new object[]
            {
                requestArgs
            });
            return HttpUtility.UrlDecode(JsonConvert.SerializeObject(value));
        }

        private static Response GetErrorResponse(string errorMessage)
        {
            return new Response
            {
                Error = true,
                ErrorMessage = errorMessage,
                PostScriptFunc = string.Empty
            };
        }
    }
}