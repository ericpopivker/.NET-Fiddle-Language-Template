using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DotNetFiddle.Infrastructure.Extensions
{
	public class Dumper
	{
		private const string ToStringName = "ToString()";


		private readonly TextWriter _outWriter;
		private int _level;

		private int _currentObjIndent = 0;
		private readonly int _maxDepth;

		private bool _arePropertiesIterated = false;

		public Dumper(int maxDepth, TextWriter outWriter)
		{
			_maxDepth = maxDepth;
			_outWriter = outWriter;
		}

		public void Write(object obj)
		{
			Write("Dumping object");
			WriteType(obj);
			WriteLine();
			WriteObject(obj);

			if (!_arePropertiesIterated)
			{
				WriteLine();
			}
		}

		private void Write(string s)
		{
			if (s != null)
			{
				this._outWriter.Write(s);
			}
		}

		private void WriteIndent()
		{
			int val;
			if (_currentObjIndent <= 0 || this._level <= 0)
				val = this._level * 2 + 1;
			else
				val = _currentObjIndent;

			// we display two spaces fur usual indent
			WriteIndent(val);
		}

		private void WriteIndent(int count)
		{
			for (int i = 0; i < count; i++)
				this._outWriter.Write(" ");
		}

		private void WriteLine()
		{
			this._outWriter.WriteLine();
		}


		private void WriteObject(object element, bool shouldMoveLine = true)
		{
			if (IsSimpleType(element))
			{
				WriteIndent();
				WriteValue(element);

				if (this._level != 0 && shouldMoveLine)
					WriteLine();
			}
			else
			{
				IEnumerable enumerableElement = element as IEnumerable;
				if (enumerableElement != null)
				{
					if (this._level <= _maxDepth)
					{
						this.WriteEnumerable(enumerableElement, 0);
					}
				}
				else
				{
					this.WriteProperties(element);
				}
			}
		}

		private bool IsSimpleType(object element)
		{
			return element == null || element is ValueType || element is string;
		}

		private void WriteProperties(object element)
		{
			MemberInfo[] members = element.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance);
			// 
			members =
				members.Where(m => m is FieldInfo || (m is PropertyInfo && ((PropertyInfo)m).GetIndexParameters().Length == 0))
					.OrderBy(m => m.Name)
					.ToArray();

			int maxIndent = members.Length != 0 ? members.Max(m => m.Name.Length) + 2 : ToStringName.Length;


			_arePropertiesIterated = true;
			foreach (MemberInfo m in members)
			{
				FieldInfo f = m as FieldInfo;
				PropertyInfo p = m as PropertyInfo;
				if (f != null || p != null)
				{
					this.WriteIndent();
					this.Write(m.Name);
					this.WriteIndent(maxIndent - m.Name.Length);

					// write simple types
					Type t = f != null ? f.FieldType : p.PropertyType;
					if (t.IsValueType || t == typeof(string))
					{
						WriteSeparator();
						this.WriteValue(f != null ? f.GetValue(element) : p.GetValue(element, null));
					}
					else
					{
						WriteSeparator();
						if (this._level < this._maxDepth)
						{
							object value = f != null ? f.GetValue(element) : p.GetValue(element, null);
							if (value != null)
							{
								var indentLevel = _currentObjIndent + maxIndent;
								_currentObjIndent = indentLevel + 2;

								if (value is IEnumerable)
								{
									WriteEnumerable((IEnumerable)value, m.Name.Length);
								}
								else
								{

									this.WriteLine();
									this._level++;
									this.WriteIndent();
									this.Write("{");
									this.WriteLine();
									this._level++;

									_currentObjIndent = indentLevel + 2;

									this.WriteObject(value);
									this._level--;
									_currentObjIndent = indentLevel + 2;

									this.WriteIndent();
									this.Write("}");
									this._level--;
								}
								_currentObjIndent = indentLevel;

								_currentObjIndent = _currentObjIndent - maxIndent;
							}
							else
							{
								WriteValue(null);
							}
						}
						else
						{
							this.Write("Too many references");
						}
					}
					this.WriteLine();
				}
			}

			// if it differs, then we display it
			var toStringResult = element.ToString();
			if (!string.Equals(toStringResult, element.GetType().ToString()))
			{
				this.WriteIndent();
				this.Write(ToStringName);
				this.WriteIndent(maxIndent - ToStringName.Length);
				this.WriteSeparator();
				this.Write(toStringResult);
				this.WriteLine();
			}
			_currentObjIndent -= maxIndent;
		}

		private void WriteEnumerable(IEnumerable enumerableElement, int indentLength)
		{
			//if (this._level != 0)
			//{
			//	this.WriteLine();
			//}

			//this.WriteIndent();
			this.Write("[");

			// if list is empty, then we don't display it
			int count = enumerableElement.Cast<object>().Count();
			if (count != 0)
			{
				this.WriteLine();
				this._level++;
				int index = 0;
				foreach (object item in enumerableElement)
				{
					bool isSimpleType = IsSimpleType(item);
					if (!isSimpleType)
					{
						this.WriteIndent();
						this.Write("{");
						this.WriteLine();	
					}
					

					if (item is IEnumerable && !(item is string))
					{
						if (this._level < this._maxDepth)
						{
							this._level++;
							this._currentObjIndent += indentLength;
							this.WriteObject(item);
							this._currentObjIndent -= indentLength;
							this._level--;							
						}
						else
						{
							this.Write("Too many references");
						}

					}
					else
					{
						this.WriteObject(item, false);
					}

					if (index != count - 1)
					{
						if (isSimpleType)
						{
							this.WriteLine();
							this.WriteIndent();
						}
					}

					if (!isSimpleType)
					{
						this.WriteIndent();
						this.Write("}");
					}
					

					if (index != count -1)
					{
						this.Write(",");
					}

					this.WriteLine();
					index++;
				}
				this._level--;
			}
			this.Write("]");
		}


		private StringBuilder FormatTypeName(Type type, StringBuilder builder)
		{
			if (builder == null)
				builder = new StringBuilder();

			if (type != null)
			{
				string typeNamespace = type.Namespace;

				if (typeNamespace == "System")
				{
					typeNamespace = null;
				}

				if (typeNamespace != null)
				{
					builder.Append(typeNamespace);
					builder.Append(".");
				}

				builder.Append(type.Name);

				if (type.IsGenericType)
				{
					builder.Append("[");

					var types = type.GetGenericArguments();
					var length = types.Length;
					for (int i = 0; i < length; i++)
					{
						var arg = types[i];
						FormatTypeName(arg, builder);
						if (i != length - 1)
							builder.Append(",");
					}

					builder.Append("]");
				}
			}
			else
			{
				builder.Append("Unknown");
			}

			return builder;
		}


        private void WriteType(object o, Type type = null)
        {
            if (type == null)
            {
                if (o != null)
                {
                    type = o.GetType();
                }
            }

            string typeName = FormatTypeName(type, null).ToString();

            Write("(");
            Write(typeName);
            Write(")");
        }

		private void WriteSeparator()
		{
			Write(": ");
		}

		private void WriteValue(object o)
		{
			if (o == null)
			{
				Write("null");
			}
			else if (o is DateTime)
			{
				Write(((DateTime)o).ToShortDateString());
			}
			else if (o is ValueType || o is string)
			{
				Write(o.ToString());
			}
			else
			{
				WriteObject(o);
			}
		}
	}
}