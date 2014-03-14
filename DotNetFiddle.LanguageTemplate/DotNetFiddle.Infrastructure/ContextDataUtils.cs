using System.Runtime.Remoting.Messaging;
using System.Web;

namespace DotNetFiddle.Infrastructure
{

	public static class ContextDataUtils
	{

		public static void SetData(string key, object value)
		{
			HttpContext httpContext = HttpContext.Current;
			if (httpContext == null)
				CallContext.SetData(key, value);
			else
				httpContext.Items[key] = value;
		}


		public static object GetData(string key)
		{

			HttpContext httpContext = HttpContext.Current;
			if (httpContext == null)
				return CallContext.GetData(key);
			else
				return httpContext.Items[key];
		}

		public static void RemoveData(string key)
		{
			HttpContext httpContext = HttpContext.Current;
			if (httpContext == null)
				CallContext.FreeNamedDataSlot(key);
			else
				httpContext.Items.Remove(key);
		}
	}
}
