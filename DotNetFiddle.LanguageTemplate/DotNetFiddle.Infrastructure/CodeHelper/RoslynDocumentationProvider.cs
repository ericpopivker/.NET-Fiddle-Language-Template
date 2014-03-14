using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using Roslyn.Compilers;

namespace DotNetFiddle.Infrastructure
{
	public class RoslynDocumentationProvider : DocumentationProvider
	{
		private string _xmlFilePath;

		public RoslynDocumentationProvider(string xmlFilePath)
		{
			_xmlFilePath = xmlFilePath;
		}


		protected override DocumentationComment GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture,
		                                                                  CancellationToken cancellationToken = new CancellationToken())
		{
			var membersToDocumentationComments = GetMembers();

			if (!membersToDocumentationComments.ContainsKey(documentationMemberID))
				return null;

			return membersToDocumentationComments[documentationMemberID];
		}


		public static ConcurrentDictionary<string, Dictionary<string, DocumentationComment>> _xmlFilePathToMembersToDocumentationComments 
												= new ConcurrentDictionary<string, Dictionary<string, DocumentationComment>>();
		private static object lockObj  =  new Object();

		private Dictionary<string, DocumentationComment> GetMembers()
		{
			if (_xmlFilePathToMembersToDocumentationComments.ContainsKey(_xmlFilePath))
				return _xmlFilePathToMembersToDocumentationComments[_xmlFilePath];

			var membersToDocumentationComments = new Dictionary<string, DocumentationComment>();
			
			//Allow loading just one file at a time
			lock (lockObj)
			{
				//Check again
				if (_xmlFilePathToMembersToDocumentationComments.ContainsKey(_xmlFilePath))
					return _xmlFilePathToMembersToDocumentationComments[_xmlFilePath];

				var xmlDocument = new XmlDocument();
				xmlDocument.Load(_xmlFilePath);

				XmlNode node = xmlDocument.SelectSingleNode("//members");

				if (node != null && node.HasChildNodes)
					foreach (XmlNode childNode in node.ChildNodes)
					{
						if (childNode.Name != "member")
							continue;

						var attribute = childNode.Attributes["name"];
						if (attribute == null)
							continue;

						string memberId = attribute.Value;
						DocumentationComment docComment = DocumentationComment.FromXmlFragment(childNode.InnerXml);

						membersToDocumentationComments[memberId] = docComment;
					}

				_xmlFilePathToMembersToDocumentationComments[_xmlFilePath] = membersToDocumentationComments;
			}

			return membersToDocumentationComments;
		}

		public override bool Equals(object obj)
		{
			return object.ReferenceEquals(this, obj);
		}

		public override int GetHashCode()
		{
			return RuntimeHelpers.GetHashCode(this);
		}
	}
}
