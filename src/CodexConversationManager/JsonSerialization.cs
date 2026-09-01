using System.Web.Script.Serialization;

namespace CodexConversationManager;

internal static class JsonSerialization
{
	public static JavaScriptSerializer NewSerializer()
	{
		JavaScriptSerializer serializer = new JavaScriptSerializer();
		serializer.MaxJsonLength = int.MaxValue;
		serializer.RecursionLimit = 256;
		return serializer;
	}
}
